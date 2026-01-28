using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using TradingJournal.Api.Data;
using TradingJournal.Api.Models;

namespace TradingJournal.Api.Services.Import;

public class FidelityImporter : ITradeImporter
{
    private readonly ApplicationDbContext _context;
    
    // Regex to parse OCC option symbol format: SYMBOL + YYMMDD + C/P + Strike*1000 (8 digits)
    // Example: AAPL250117C00150000 = AAPL Jan 17, 2025 $150 Call
    // Or with dash: -AAPL250117C00150000
    private static readonly Regex OptionSymbolRegex = new Regex(
        @"^-?([A-Z]+)(\d{6})([CP])(\d{8})$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public FidelityImporter(ApplicationDbContext context)
    {
        _context = context;
    }

    private async Task<bool> IsDuplicateTradeAsync(string userId, string accountId, string symbol, DateTime date, string type, double quantity, double price)
    {
        // Check if a trade with the same key attributes already exists
        return await _context.Trades.AnyAsync(t =>
            t.UserId == userId &&
            t.AccountId == accountId &&
            t.Symbol == symbol &&
            t.Date.Date == date.Date &&
            t.Type == type &&
            Math.Abs(t.Quantity - quantity) < 0.001 &&
            Math.Abs(t.Price - price) < 0.001
        );
    }
    
    /// <summary>
    /// Parses an OCC option symbol to extract option details
    /// </summary>
    private static (bool IsOption, string UnderlyingSymbol, DateTime? Expiration, OptionType? Type, double? Strike) ParseOptionSymbol(string symbol)
    {
        if (string.IsNullOrEmpty(symbol))
            return (false, symbol, null, null, null);
            
        var match = OptionSymbolRegex.Match(symbol);
        if (!match.Success)
            return (false, symbol, null, null, null);
            
        var underlying = match.Groups[1].Value;
        var dateStr = match.Groups[2].Value; // YYMMDD
        var typeChar = match.Groups[3].Value.ToUpper();
        var strikeStr = match.Groups[4].Value; // Strike * 1000
        
        // Parse expiration date
        if (!DateTime.TryParseExact(dateStr, "yyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var expiration))
            return (false, symbol, null, null, null);
            
        // Parse option type
        var optionType = typeChar == "C" ? Models.OptionType.Call : Models.OptionType.Put;
        
        // Parse strike (divide by 1000)
        if (!double.TryParse(strikeStr, out var strikeRaw))
            return (false, symbol, null, null, null);
        var strike = strikeRaw / 1000.0;
        
        return (true, underlying, expiration, optionType, strike);
    }
    
    /// <summary>
    /// Determines if the trade is opening or closing based on action text
    /// </summary>
    private static (bool IsOpening, string NormalizedType) ParseOptionsAction(string action)
    {
        action = action.ToUpper();
        
        if (action.Contains("YOU BOUGHT OPENING") || action.Contains("BOUGHT TO OPEN"))
            return (true, "BUY_TO_OPEN");
        if (action.Contains("YOU SOLD OPENING") || action.Contains("SOLD TO OPEN"))
            return (true, "SELL_TO_OPEN");
        if (action.Contains("YOU BOUGHT CLOSING") || action.Contains("BOUGHT TO CLOSE"))
            return (false, "BUY_TO_CLOSE");
        if (action.Contains("YOU SOLD CLOSING") || action.Contains("SOLD TO CLOSE"))
            return (false, "SELL_TO_CLOSE");
        if (action.Contains("ASSIGNED") || action.Contains("EXERCISED") || action.Contains("EXPIRED"))
            return (false, action.Contains("ASSIGNED") ? "ASSIGNED" : action.Contains("EXERCISED") ? "EXERCISED" : "EXPIRED");
            
        // Default: treat as opening for buys, closing for sells
        if (action.Contains("BOUGHT"))
            return (true, "BUY_TO_OPEN");
        if (action.Contains("SOLD"))
            return (true, "SELL_TO_OPEN");
            
        return (true, "BUY");
    }

    private async Task<bool> IsDuplicateDividendAsync(string userId, string accountId, string symbol, DateTime date, double amount)
    {
        return await _context.Dividends.AnyAsync(d =>
            d.UserId == userId &&
            d.AccountId == accountId &&
            d.Symbol == symbol &&
            d.PaymentDate.Date == date.Date &&
            Math.Abs(d.Amount - amount) < 0.01
        );
    }

    public string FormatName => "Fidelity";
    public string FormatDescription => "Fidelity Brokerage Account History Export (CSV) - imports trades and dividends";

    public bool CanParse(string[] headers)
    {
        // Check for Fidelity-specific headers
        return headers.Any(h => h.Contains("Run Date", StringComparison.OrdinalIgnoreCase)) &&
               headers.Any(h => h.Contains("Action", StringComparison.OrdinalIgnoreCase)) &&
               headers.Any(h => h.Contains("Symbol", StringComparison.OrdinalIgnoreCase));
    }

    public async Task<ImportResult> ImportAsync(Stream csvStream, string userId, string accountId)
    {
        var result = new ImportResult { Success = true };
        
        using var reader = new StreamReader(csvStream);
        var content = await reader.ReadToEndAsync();
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // Find header line (skip empty lines at start)
        int headerIndex = -1;
        string[]? headers = null;
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.StartsWith("Run Date", StringComparison.OrdinalIgnoreCase))
            {
                headerIndex = i;
                headers = ParseCsvLine(line);
                break;
            }
        }

        if (headerIndex == -1 || headers == null)
        {
            result.Success = false;
            result.Errors.Add("Could not find header row. Expected 'Run Date' column.");
            return result;
        }

        // Get column indices
        int dateIndex = Array.FindIndex(headers, h => h.Contains("Run Date", StringComparison.OrdinalIgnoreCase));
        int actionIndex = Array.FindIndex(headers, h => h.Equals("Action", StringComparison.OrdinalIgnoreCase));
        int symbolIndex = Array.FindIndex(headers, h => h.Equals("Symbol", StringComparison.OrdinalIgnoreCase));
        int priceIndex = Array.FindIndex(headers, h => h.Contains("Price", StringComparison.OrdinalIgnoreCase));
        int quantityIndex = Array.FindIndex(headers, h => h.Equals("Quantity", StringComparison.OrdinalIgnoreCase));
        int commissionIndex = Array.FindIndex(headers, h => h.Contains("Commission", StringComparison.OrdinalIgnoreCase));
        int feesIndex = Array.FindIndex(headers, h => h.Contains("Fees", StringComparison.OrdinalIgnoreCase));
        int descriptionIndex = Array.FindIndex(headers, h => h.Equals("Description", StringComparison.OrdinalIgnoreCase));
        int amountIndex = Array.FindIndex(headers, h => h.Contains("Amount", StringComparison.OrdinalIgnoreCase));

        int dividendsImported = 0;

        // Process data rows
        for (int i = headerIndex + 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            
            // Skip empty lines and disclaimer text
            if (string.IsNullOrEmpty(line) || line.StartsWith("\"The data") || line.StartsWith("\"informational") ||
                line.StartsWith("\"exported") || line.StartsWith("\"purposes") || line.StartsWith("\"Brokerage") ||
                line.StartsWith("\"Financial") || line.StartsWith("\"Fidelity") || line.StartsWith("Date downloaded"))
            {
                continue;
            }

            try
            {
                var fields = ParseCsvLine(line);
                if (fields.Length < Math.Max(Math.Max(dateIndex, symbolIndex), quantityIndex) + 1)
                {
                    result.SkippedCount++;
                    continue;
                }

                var action = actionIndex >= 0 && actionIndex < fields.Length ? fields[actionIndex] : "";
                var dateStr = fields[dateIndex].Trim();
                var symbol = symbolIndex >= 0 && symbolIndex < fields.Length ? fields[symbolIndex].Trim() : "";
                var description = descriptionIndex >= 0 && descriptionIndex < fields.Length ? fields[descriptionIndex].Trim() : "";
                
                // Parse date first (needed for all types)
                if (!DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                {
                    result.SkippedCount++;
                    continue;
                }

                // Check for dividend transactions (Action contains "DIVIDEND RECEIVED" or "REINVESTMENT")
                if (action.Contains("DIVIDEND", StringComparison.OrdinalIgnoreCase) ||
                    action.Contains("REINVESTMENT", StringComparison.OrdinalIgnoreCase))
                {
                    // Process dividend
                    var amountStr = amountIndex >= 0 && amountIndex < fields.Length ? fields[amountIndex].Trim() : "0";
                    if (!double.TryParse(amountStr.Replace("$", "").Replace(",", ""), out var amount) || amount == 0)
                    {
                        result.SkippedCount++;
                        continue;
                    }

                    // Skip if no symbol for dividend
                    if (string.IsNullOrEmpty(symbol))
                    {
                        result.SkippedCount++;
                        continue;
                    }

                    var dividendDate = DateTime.SpecifyKind(date, DateTimeKind.Utc);

                    // Check for duplicate dividend (using actual amount including sign)
                    if (await IsDuplicateDividendAsync(userId, accountId, symbol, dividendDate, amount))
                    {
                        result.SkippedCount++;
                        continue;
                    }

                    // Determine dividend type from Type column or action
                    var typeColumn = fields.Length > 4 ? fields[4].Trim() : "";
                    var dividendType = "CASH";
                    
                    if (action.Contains("REINVEST", StringComparison.OrdinalIgnoreCase) || 
                        typeColumn.Contains("Reinvest", StringComparison.OrdinalIgnoreCase))
                    {
                        dividendType = "REINVESTED";
                    }
                    else if (action.Contains("QUALIFIED", StringComparison.OrdinalIgnoreCase) && 
                             !action.Contains("NON-QUALIFIED", StringComparison.OrdinalIgnoreCase))
                    {
                        dividendType = "QUALIFIED";
                    }
                    else if (action.Contains("NON-QUALIFIED", StringComparison.OrdinalIgnoreCase) ||
                             action.Contains("NONQUALIFIED", StringComparison.OrdinalIgnoreCase))
                    {
                        dividendType = "NON_QUALIFIED";
                    }

                    // Handle negative amounts as adjustments
                    var notes = amount < 0 
                        ? $"Imported from Fidelity (Adjustment): {action}" 
                        : $"Imported from Fidelity: {action}";

                    var dividend = new Dividend
                    {
                        Symbol = symbol,
                        Amount = amount, // Keep original sign for adjustments
                        Type = dividendType,
                        Currency = "USD",
                        PaymentDate = dividendDate,
                        Notes = notes,
                        AccountId = accountId,
                        UserId = userId,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    _context.Dividends.Add(dividend);
                    dividendsImported++;
                    continue;
                }
                
                // Process BUY, SELL, and options-specific actions
                bool isOptionsAction = action.Contains("OPENING", StringComparison.OrdinalIgnoreCase) ||
                                       action.Contains("CLOSING", StringComparison.OrdinalIgnoreCase) ||
                                       action.Contains("TO OPEN", StringComparison.OrdinalIgnoreCase) ||
                                       action.Contains("TO CLOSE", StringComparison.OrdinalIgnoreCase) ||
                                       action.Contains("ASSIGNED", StringComparison.OrdinalIgnoreCase) ||
                                       action.Contains("EXERCISED", StringComparison.OrdinalIgnoreCase) ||
                                       action.Contains("EXPIRED", StringComparison.OrdinalIgnoreCase);
                                       
                if (!action.Contains("BOUGHT", StringComparison.OrdinalIgnoreCase) && 
                    !action.Contains("SOLD", StringComparison.OrdinalIgnoreCase) &&
                    !isOptionsAction)
                {
                    result.SkippedCount++;
                    continue;
                }

                var priceStr = priceIndex >= 0 && priceIndex < fields.Length ? fields[priceIndex].Trim() : "0";
                var quantityStr = quantityIndex >= 0 && quantityIndex < fields.Length ? fields[quantityIndex].Trim() : "0";
                var commissionStr = commissionIndex >= 0 && commissionIndex < fields.Length ? fields[commissionIndex].Trim() : "0";
                var feesStr = feesIndex >= 0 && feesIndex < fields.Length ? fields[feesIndex].Trim() : "0";

                // Skip if no symbol
                if (string.IsNullOrEmpty(symbol))
                {
                    result.SkippedCount++;
                    continue;
                }

                if (!double.TryParse(priceStr.Replace("$", "").Replace(",", ""), out var price))
                {
                    price = 0;
                }

                if (!double.TryParse(quantityStr.Replace(",", ""), out var quantity))
                {
                    result.Errors.Add($"Row {i + 1}: Invalid quantity '{quantityStr}'");
                    result.ErrorCount++;
                    continue;
                }

                double.TryParse(commissionStr.Replace("$", "").Replace(",", ""), out var commission);
                double.TryParse(feesStr.Replace("$", "").Replace(",", ""), out var fees);

                // Parse option symbol if applicable
                var (isOption, underlyingSymbol, expiration, optionType, strikePrice) = ParseOptionSymbol(symbol);
                
                // Determine trade type and opening/closing
                string tradeType;
                bool isOpening = true;
                
                if (isOption || isOptionsAction)
                {
                    var (opening, normalizedType) = ParseOptionsAction(action);
                    isOpening = opening;
                    tradeType = normalizedType;
                }
                else
                {
                    tradeType = action.Contains("BOUGHT", StringComparison.OrdinalIgnoreCase) ? "BUY" : "SELL";
                }
                
                // Make quantity positive (Fidelity uses negative for sells)
                quantity = Math.Abs(quantity);

                // Check for duplicate trade
                var tradeDate = DateTime.SpecifyKind(date, DateTimeKind.Utc);
                if (await IsDuplicateTradeAsync(userId, accountId, symbol, tradeDate, tradeType, quantity, price))
                {
                    result.SkippedCount++;
                    result.Errors.Add($"Row {i + 1}: Duplicate trade skipped ({symbol} {tradeType} {quantity} @ {price:C2} on {date:d})");
                    continue;
                }

                var trade = new Trade
                {
                    Symbol = symbol,
                    Type = tradeType,
                    Quantity = quantity,
                    Price = price,
                    Fee = commission + fees,
                    Currency = "USD",
                    Date = tradeDate,
                    Notes = $"Imported from Fidelity: {description}",
                    AccountId = accountId,
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    // Options fields
                    InstrumentType = isOption ? InstrumentType.Option : InstrumentType.Stock,
                    OptionType = optionType,
                    StrikePrice = strikePrice,
                    ExpirationDate = expiration.HasValue ? DateTime.SpecifyKind(expiration.Value, DateTimeKind.Utc) : null,
                    UnderlyingSymbol = isOption ? underlyingSymbol : null,
                    ContractMultiplier = isOption ? 100 : 1,
                    IsOpeningTrade = isOption ? isOpening : null
                };

                _context.Trades.Add(trade);
                result.ImportedTrades.Add(new ImportedTradeDto
                {
                    Symbol = symbol,
                    Type = tradeType,
                    Quantity = quantity,
                    Price = price,
                    Fee = commission + fees,
                    Date = date,
                    Notes = description,
                    InstrumentType = isOption ? "Option" : "Stock",
                    OptionType = optionType?.ToString(),
                    StrikePrice = strikePrice,
                    ExpirationDate = expiration,
                    UnderlyingSymbol = isOption ? underlyingSymbol : null,
                    IsOpeningTrade = isOption ? isOpening : null
                });
                result.ImportedCount++;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Row {i + 1}: {ex.Message}");
                result.ErrorCount++;
            }
        }

        result.DividendsImportedCount = dividendsImported;

        if (result.ImportedCount > 0 || dividendsImported > 0)
        {
            await _context.SaveChangesAsync();
            
            // After saving, detect and update spreads for options trades
            await DetectAndUpdateSpreadsAsync(userId, accountId);
        }

        return result;
    }
    
    /// <summary>
    /// Detects credit/debit spreads by analyzing options trades on the same day with same underlying
    /// </summary>
    private async Task DetectAndUpdateSpreadsAsync(string userId, string accountId)
    {
        // Get all options trades without a spread group
        var optionsTrades = await _context.Trades
            .Where(t => t.UserId == userId && 
                        t.AccountId == accountId && 
                        t.InstrumentType == InstrumentType.Option &&
                        t.SpreadGroupId == null)
            .OrderBy(t => t.Date)
            .ToListAsync();
            
        if (!optionsTrades.Any())
            return;
            
        // Group by date and underlying symbol
        var groups = optionsTrades
            .Where(t => t.UnderlyingSymbol != null && t.ExpirationDate.HasValue)
            .GroupBy(t => new { Date = t.Date.Date, t.UnderlyingSymbol, t.ExpirationDate })
            .Where(g => g.Count() >= 2) // Need at least 2 legs for a spread
            .ToList();
            
        foreach (var group in groups)
        {
            var trades = group.OrderBy(t => t.StrikePrice).ToList();
            
            // Analyze the spread type
            var spreadType = AnalyzeSpreadType(trades);
            if (spreadType == SpreadType.Single)
                continue;
                
            // Create a spread group ID
            var spreadGroupId = Guid.NewGuid().ToString();
            
            // Update all trades in the spread
            int legNumber = 1;
            foreach (var trade in trades)
            {
                trade.SpreadType = spreadType;
                trade.SpreadGroupId = spreadGroupId;
                trade.SpreadLegNumber = legNumber++;
                trade.UpdatedAt = DateTime.UtcNow;
            }
        }
        
        await _context.SaveChangesAsync();
    }
    
    /// <summary>
    /// Analyzes a group of options trades to determine the spread type
    /// </summary>
    private static SpreadType AnalyzeSpreadType(List<Trade> trades)
    {
        if (trades.Count < 2)
            return SpreadType.Single;
            
        // Calculate net premium (positive = credit, negative = debit)
        double netPremium = 0;
        int buyCount = 0;
        int sellCount = 0;
        int callCount = 0;
        int putCount = 0;
        
        foreach (var trade in trades)
        {
            var premium = trade.Price * trade.Quantity * trade.ContractMultiplier;
            
            if (trade.Type.Contains("BUY", StringComparison.OrdinalIgnoreCase))
            {
                netPremium -= premium; // Buying costs money
                buyCount++;
            }
            else if (trade.Type.Contains("SELL", StringComparison.OrdinalIgnoreCase))
            {
                netPremium += premium; // Selling receives money
                sellCount++;
            }
            
            if (trade.OptionType == Models.OptionType.Call)
                callCount++;
            else if (trade.OptionType == Models.OptionType.Put)
                putCount++;
        }
        
        // 4 legs with calls and puts = Iron Condor
        if (trades.Count == 4 && callCount == 2 && putCount == 2)
            return SpreadType.IronCondor;
            
        // 3 legs with same type = Butterfly
        if (trades.Count == 3 && (callCount == 3 || putCount == 3))
            return SpreadType.Butterfly;
            
        // Same strike, different type = Straddle
        if (trades.Count == 2 && callCount == 1 && putCount == 1)
        {
            if (trades[0].StrikePrice == trades[1].StrikePrice)
                return SpreadType.Straddle;
            else
                return SpreadType.Strangle;
        }
        
        // 2 legs, same type = Vertical spread
        if (trades.Count == 2 && (callCount == 2 || putCount == 2))
        {
            if (buyCount == 1 && sellCount == 1)
            {
                // Credit spread: receive net premium (sell more expensive option)
                // Debit spread: pay net premium (buy more expensive option)
                return netPremium > 0 ? SpreadType.CreditSpread : SpreadType.DebitSpread;
            }
        }
        
        // More than 2 legs, mixed = Custom
        if (trades.Count > 2)
            return SpreadType.Custom;
            
        return SpreadType.Single;
    }

    private static string[] ParseCsvLine(string line)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        result.Add(current.ToString().Trim());
        return result.ToArray();
    }
}
