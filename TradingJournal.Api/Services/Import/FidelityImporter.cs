using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using TradingJournal.Api.Data;
using TradingJournal.Api.Models;

namespace TradingJournal.Api.Services.Import;

public class FidelityImporter : ITradeImporter
{
    private readonly ApplicationDbContext _context;
    
    // Regex to parse OCC option symbol format with 8-digit strike: SYMBOL + YYMMDD + C/P + Strike*1000 (8 digits)
    // Example: AAPL250117C00150000 = AAPL Jan 17, 2025 $150 Call
    private static readonly Regex OptionSymbolRegexStandard = new Regex(
        @"^-?([A-Z]+)(\d{6})([CP])(\d{8})$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    
    // Regex for Fidelity's shorter format: SYMBOL + YYMMDD + C/P + Strike (variable digits, no padding)
    // Example: -SPXW260130C6985 = SPXW Jan 30, 2026 $6985 Call
    private static readonly Regex OptionSymbolRegexShort = new Regex(
        @"^-?([A-Z]+\d?)(\d{6})([CP])(\d+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    
    // Regex to parse option details from description field
    // Example: "CALL (SPXW) NEW S & P 500 INDEX JAN 30 26 $6985 (100 SHS)"
    private static readonly Regex DescriptionRegex = new Regex(
        @"(CALL|PUT)\s*\(([A-Z0-9]+)\).*?([A-Z]{3})\s+(\d{1,2})\s+(\d{2})\s+\$?([\d,]+(?:\.\d+)?)",
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
    /// Parses an OCC option symbol to extract option details.
    /// Supports both standard (8-digit strike) and Fidelity short format.
    /// </summary>
    private static (bool IsOption, string UnderlyingSymbol, DateTime? Expiration, OptionType? Type, double? Strike) ParseOptionSymbol(string symbol)
    {
        if (string.IsNullOrEmpty(symbol))
            return (false, symbol, null, null, null);
        
        // Try standard OCC format first (8-digit strike with decimal encoding)
        var match = OptionSymbolRegexStandard.Match(symbol);
        if (match.Success)
        {
            var underlying = match.Groups[1].Value;
            var dateStr = match.Groups[2].Value;
            var typeChar = match.Groups[3].Value.ToUpper();
            var strikeStr = match.Groups[4].Value;
            
            if (DateTime.TryParseExact(dateStr, "yyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var expiration))
            {
                var optionType = typeChar == "C" ? Models.OptionType.Call : Models.OptionType.Put;
                if (double.TryParse(strikeStr, out var strikeRaw))
                {
                    // Standard OCC: divide by 1000 (e.g., 00150000 = $150.00)
                    return (true, underlying, expiration, optionType, strikeRaw / 1000.0);
                }
            }
        }
        
        // Try Fidelity short format (variable-length strike, no decimal encoding)
        match = OptionSymbolRegexShort.Match(symbol);
        if (match.Success)
        {
            var underlying = match.Groups[1].Value;
            var dateStr = match.Groups[2].Value;
            var typeChar = match.Groups[3].Value.ToUpper();
            var strikeStr = match.Groups[4].Value;
            
            if (DateTime.TryParseExact(dateStr, "yyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var expiration))
            {
                var optionType = typeChar == "C" ? Models.OptionType.Call : Models.OptionType.Put;
                if (double.TryParse(strikeStr, out var strike))
                {
                    // Fidelity short format: strike is the actual dollar amount (e.g., 6985 = $6985)
                    return (true, underlying, expiration, optionType, strike);
                }
            }
        }
            
        return (false, symbol, null, null, null);
    }
    
    /// <summary>
    /// Parses option details from the Description field as a fallback.
    /// Example: "CALL (SPXW) NEW S & P 500 INDEX JAN 30 26 $6985 (100 SHS)"
    /// </summary>
    private static (bool IsOption, string UnderlyingSymbol, DateTime? Expiration, OptionType? Type, double? Strike) ParseOptionFromDescription(string description)
    {
        if (string.IsNullOrEmpty(description))
            return (false, "", null, null, null);
            
        var match = DescriptionRegex.Match(description);
        if (!match.Success)
            return (false, "", null, null, null);
            
        var optionTypeStr = match.Groups[1].Value.ToUpper();
        var underlying = match.Groups[2].Value;
        var monthStr = match.Groups[3].Value.ToUpper();
        var dayStr = match.Groups[4].Value;
        var yearStr = match.Groups[5].Value;
        var strikeStr = match.Groups[6].Value.Replace(",", "");
        
        // Parse month
        var months = new Dictionary<string, int>
        {
            {"JAN", 1}, {"FEB", 2}, {"MAR", 3}, {"APR", 4}, {"MAY", 5}, {"JUN", 6},
            {"JUL", 7}, {"AUG", 8}, {"SEP", 9}, {"OCT", 10}, {"NOV", 11}, {"DEC", 12}
        };
        
        if (!months.TryGetValue(monthStr, out var month))
            return (false, "", null, null, null);
            
        if (!int.TryParse(dayStr, out var day) || !int.TryParse(yearStr, out var year))
            return (false, "", null, null, null);
            
        // Handle 2-digit year
        year = year < 100 ? (year > 50 ? 1900 + year : 2000 + year) : year;
        
        var optionType = optionTypeStr == "CALL" ? Models.OptionType.Call : Models.OptionType.Put;
        
        if (!double.TryParse(strikeStr, out var strike))
            return (false, "", null, null, null);
            
        try
        {
            var expiration = new DateTime(year, month, day);
            return (true, underlying, expiration, optionType, strike);
        }
        catch
        {
            return (false, "", null, null, null);
        }
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
                
                // If symbol parsing failed but action indicates options, try parsing from description
                if (!isOption && isOptionsAction && !string.IsNullOrEmpty(description))
                {
                    var descResult = ParseOptionFromDescription(description);
                    if (descResult.IsOption)
                    {
                        isOption = true;
                        underlyingSymbol = descResult.UnderlyingSymbol;
                        expiration = descResult.Expiration;
                        optionType = descResult.Type;
                        strikePrice = descResult.Strike;
                    }
                }
                
                // Also detect options from action text containing CALL or PUT
                if (!isOption && (action.Contains("CALL", StringComparison.OrdinalIgnoreCase) || 
                                   action.Contains("PUT", StringComparison.OrdinalIgnoreCase)))
                {
                    var descResult = ParseOptionFromDescription(description);
                    if (descResult.IsOption)
                    {
                        isOption = true;
                        underlyingSymbol = descResult.UnderlyingSymbol;
                        expiration = descResult.Expiration;
                        optionType = descResult.Type;
                        strikePrice = descResult.Strike;
                    }
                    else
                    {
                        // Try parsing from action text as last resort
                        descResult = ParseOptionFromDescription(action);
                        if (descResult.IsOption)
                        {
                            isOption = true;
                            underlyingSymbol = descResult.UnderlyingSymbol;
                            expiration = descResult.Expiration;
                            optionType = descResult.Type;
                            strikePrice = descResult.Strike;
                        }
                    }
                }
                
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
    /// Detects options strategies by analyzing trades on the same day with same underlying.
    /// Handles vertical spreads, iron condors, butterflies, calendars, and more.
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
        
        // PASS 1: Iron Condors (4 legs: 2 calls + 2 puts, same expiry, 1 buy + 1 sell each)
        await DetectIronCondorsAsync(optionsTrades);
        
        // PASS 2: Vertical Spreads (2 legs: same type, same expiry, different strikes)
        await DetectVerticalSpreadsAsync(optionsTrades);
        
        // PASS 3: Straddles and Strangles (2 legs: 1 call + 1 put, same expiry)
        await DetectStraddlesAndStranglesAsync(optionsTrades);
        
        // PASS 4: Butterflies (3 legs: same type, same expiry)
        await DetectButterfliesAsync(optionsTrades);
        
        // PASS 5: Calendar/Diagonal Spreads (same underlying, same strike, different expiry)
        await DetectCalendarSpreadsAsync(optionsTrades);
        
        // PASS 6: Remaining multi-leg trades as Custom
        await DetectCustomStrategiesAsync(optionsTrades);
        
        await _context.SaveChangesAsync();
    }
    
    /// <summary>
    /// Detects Iron Condors: 4 legs with 2 calls + 2 puts, 1 buy + 1 sell for each type
    /// </summary>
    private Task DetectIronCondorsAsync(List<Trade> trades)
    {
        var candidates = trades
            .Where(t => t.SpreadGroupId == null && t.UnderlyingSymbol != null && t.ExpirationDate.HasValue)
            .GroupBy(t => new { Date = t.Date.Date, t.UnderlyingSymbol, t.ExpirationDate, t.IsOpeningTrade })
            .Where(g => g.Count() == 4)
            .ToList();
            
        foreach (var group in candidates)
        {
            var legs = group.ToList();
            var calls = legs.Where(t => t.OptionType == Models.OptionType.Call).ToList();
            var puts = legs.Where(t => t.OptionType == Models.OptionType.Put).ToList();
            
            // Must have exactly 2 calls and 2 puts
            if (calls.Count != 2 || puts.Count != 2)
                continue;
                
            // Each side should have 1 buy and 1 sell
            var callBuys = calls.Count(t => t.Type.Contains("BUY", StringComparison.OrdinalIgnoreCase));
            var callSells = calls.Count(t => t.Type.Contains("SELL", StringComparison.OrdinalIgnoreCase));
            var putBuys = puts.Count(t => t.Type.Contains("BUY", StringComparison.OrdinalIgnoreCase));
            var putSells = puts.Count(t => t.Type.Contains("SELL", StringComparison.OrdinalIgnoreCase));
            
            if (callBuys != 1 || callSells != 1 || putBuys != 1 || putSells != 1)
                continue;
            
            // This is an Iron Condor
            AssignSpreadGroup(legs, SpreadType.IronCondor, "Iron Condor");
        }
        
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// Detects Vertical Spreads: 2 legs with same type, same expiry, different strikes
    /// </summary>
    private Task DetectVerticalSpreadsAsync(List<Trade> trades)
    {
        var candidates = trades
            .Where(t => t.SpreadGroupId == null && t.UnderlyingSymbol != null && t.ExpirationDate.HasValue)
            .GroupBy(t => new { Date = t.Date.Date, t.UnderlyingSymbol, t.ExpirationDate, t.OptionType, t.IsOpeningTrade })
            .Where(g => g.Count() == 2)
            .ToList();
            
        foreach (var group in candidates)
        {
            var legs = group.OrderBy(t => t.StrikePrice).ToList();
            
            // Must have 1 buy and 1 sell
            var buyCount = legs.Count(t => t.Type.Contains("BUY", StringComparison.OrdinalIgnoreCase));
            var sellCount = legs.Count(t => t.Type.Contains("SELL", StringComparison.OrdinalIgnoreCase));
            
            if (buyCount != 1 || sellCount != 1)
                continue;
                
            // Different strikes
            if (legs[0].StrikePrice == legs[1].StrikePrice)
                continue;
            
            // Calculate net premium to determine credit vs debit
            var netPremium = CalculateNetPremium(legs);
            var spreadType = netPremium > 0 ? SpreadType.CreditSpread : SpreadType.DebitSpread;
            var typeName = legs[0].OptionType == Models.OptionType.Call ? "Call" : "Put";
            var spreadName = netPremium > 0 ? $"Credit {typeName} Spread" : $"Debit {typeName} Spread";
            
            AssignSpreadGroup(legs, spreadType, spreadName);
        }
        
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// Detects Straddles (same strike) and Strangles (different strikes)
    /// </summary>
    private Task DetectStraddlesAndStranglesAsync(List<Trade> trades)
    {
        var candidates = trades
            .Where(t => t.SpreadGroupId == null && t.UnderlyingSymbol != null && t.ExpirationDate.HasValue)
            .GroupBy(t => new { Date = t.Date.Date, t.UnderlyingSymbol, t.ExpirationDate, t.IsOpeningTrade })
            .Where(g => g.Count() == 2)
            .ToList();
            
        foreach (var group in candidates)
        {
            var legs = group.ToList();
            var calls = legs.Where(t => t.OptionType == Models.OptionType.Call).ToList();
            var puts = legs.Where(t => t.OptionType == Models.OptionType.Put).ToList();
            
            // Must have exactly 1 call and 1 put
            if (calls.Count != 1 || puts.Count != 1)
                continue;
            
            // Both should be the same direction (both buys or both sells)
            var buyCount = legs.Count(t => t.Type.Contains("BUY", StringComparison.OrdinalIgnoreCase));
            var sellCount = legs.Count(t => t.Type.Contains("SELL", StringComparison.OrdinalIgnoreCase));
            
            if (buyCount != 2 && sellCount != 2)
                continue;
            
            var isSameStrike = calls[0].StrikePrice == puts[0].StrikePrice;
            var spreadType = isSameStrike ? SpreadType.Straddle : SpreadType.Strangle;
            var direction = sellCount == 2 ? "Short" : "Long";
            var spreadName = isSameStrike ? $"{direction} Straddle" : $"{direction} Strangle";
            
            AssignSpreadGroup(legs, spreadType, spreadName);
        }
        
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// Detects Butterfly Spreads: 3 legs with same type
    /// </summary>
    private Task DetectButterfliesAsync(List<Trade> trades)
    {
        var candidates = trades
            .Where(t => t.SpreadGroupId == null && t.UnderlyingSymbol != null && t.ExpirationDate.HasValue)
            .GroupBy(t => new { Date = t.Date.Date, t.UnderlyingSymbol, t.ExpirationDate, t.OptionType, t.IsOpeningTrade })
            .Where(g => g.Count() == 3)
            .ToList();
            
        foreach (var group in candidates)
        {
            var legs = group.OrderBy(t => t.StrikePrice).ToList();
            
            // Butterfly: buy 1, sell 2 (middle), buy 1 OR sell 1, buy 2 (middle), sell 1
            var typeName = legs[0].OptionType == Models.OptionType.Call ? "Call" : "Put";
            AssignSpreadGroup(legs, SpreadType.Butterfly, $"{typeName} Butterfly");
        }
        
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// Detects Calendar and Diagonal Spreads: same underlying and strike, different expiry
    /// </summary>
    private Task DetectCalendarSpreadsAsync(List<Trade> trades)
    {
        var candidates = trades
            .Where(t => t.SpreadGroupId == null && t.UnderlyingSymbol != null && t.ExpirationDate.HasValue && t.StrikePrice.HasValue)
            .GroupBy(t => new { Date = t.Date.Date, t.UnderlyingSymbol, t.StrikePrice, t.OptionType })
            .Where(g => g.Count() == 2 && g.Select(t => t.ExpirationDate).Distinct().Count() == 2)
            .ToList();
            
        foreach (var group in candidates)
        {
            var legs = group.OrderBy(t => t.ExpirationDate).ToList();
            
            // Should have 1 buy and 1 sell
            var buyCount = legs.Count(t => t.Type.Contains("BUY", StringComparison.OrdinalIgnoreCase));
            var sellCount = legs.Count(t => t.Type.Contains("SELL", StringComparison.OrdinalIgnoreCase));
            
            if (buyCount != 1 || sellCount != 1)
                continue;
            
            var typeName = legs[0].OptionType == Models.OptionType.Call ? "Call" : "Put";
            AssignSpreadGroup(legs, SpreadType.Calendar, $"{typeName} Calendar Spread");
        }
        
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// Groups remaining unassigned multi-leg trades as Custom strategies
    /// </summary>
    private Task DetectCustomStrategiesAsync(List<Trade> trades)
    {
        var candidates = trades
            .Where(t => t.SpreadGroupId == null && t.UnderlyingSymbol != null && t.ExpirationDate.HasValue)
            .GroupBy(t => new { Date = t.Date.Date, t.UnderlyingSymbol, t.ExpirationDate, t.IsOpeningTrade })
            .Where(g => g.Count() >= 2)
            .ToList();
            
        foreach (var group in candidates)
        {
            var legs = group.OrderBy(t => t.StrikePrice).ToList();
            
            // Try to identify the strategy
            var strategyName = IdentifyCustomStrategy(legs);
            AssignSpreadGroup(legs, SpreadType.Custom, strategyName);
        }
        
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// Attempts to identify a custom strategy based on leg composition
    /// </summary>
    private static string IdentifyCustomStrategy(List<Trade> legs)
    {
        var callCount = legs.Count(t => t.OptionType == Models.OptionType.Call);
        var putCount = legs.Count(t => t.OptionType == Models.OptionType.Put);
        var buyCount = legs.Count(t => t.Type.Contains("BUY", StringComparison.OrdinalIgnoreCase));
        var sellCount = legs.Count(t => t.Type.Contains("SELL", StringComparison.OrdinalIgnoreCase));
        
        // Ratio spreads
        if (legs.Count == 3 && (callCount == 3 || putCount == 3))
        {
            if (buyCount == 1 && sellCount == 2)
                return "1x2 Ratio Spread (Short)";
            if (buyCount == 2 && sellCount == 1)
                return "1x2 Ratio Spread (Long)";
        }
        
        // Iron Butterfly (4 legs, straddle + wings)
        if (legs.Count == 4 && callCount == 2 && putCount == 2)
        {
            var strikes = legs.Select(t => t.StrikePrice).Distinct().Count();
            if (strikes == 3) // Center strike shared
                return "Iron Butterfly";
        }
        
        // Jade Lizard / Twisted Sister
        if (legs.Count == 3 && callCount == 2 && putCount == 1)
            return "Jade Lizard";
        if (legs.Count == 3 && callCount == 1 && putCount == 2)
            return "Twisted Sister";
        
        // Default
        return $"Custom ({legs.Count}-leg)";
    }
    
    /// <summary>
    /// Assigns a spread group to a list of trades
    /// </summary>
    private static void AssignSpreadGroup(List<Trade> trades, SpreadType spreadType, string strategyName)
    {
        var spreadGroupId = Guid.NewGuid().ToString();
        var netPremium = CalculateNetPremium(trades);
        var direction = netPremium > 0 ? "Credit" : "Debit";
        
        int legNumber = 1;
        foreach (var trade in trades.OrderBy(t => t.StrikePrice).ThenBy(t => t.OptionType))
        {
            trade.SpreadType = spreadType;
            trade.SpreadGroupId = spreadGroupId;
            trade.SpreadLegNumber = legNumber++;
            trade.UpdatedAt = DateTime.UtcNow;
            
            // Append strategy info to notes
            if (!string.IsNullOrEmpty(trade.Notes) && !trade.Notes.Contains("Strategy:"))
            {
                trade.Notes += $" | Strategy: {strategyName} ({direction} ${Math.Abs(netPremium):N2})";
            }
        }
    }
    
    /// <summary>
    /// Calculates net premium for a group of trades (positive = credit, negative = debit)
    /// </summary>
    private static double CalculateNetPremium(List<Trade> trades)
    {
        double netPremium = 0;
        
        foreach (var trade in trades)
        {
            var premium = trade.Price * trade.Quantity * trade.ContractMultiplier;
            
            if (trade.Type.Contains("SELL", StringComparison.OrdinalIgnoreCase))
                netPremium += premium;
            else if (trade.Type.Contains("BUY", StringComparison.OrdinalIgnoreCase))
                netPremium -= premium;
        }
        
        return netPremium;
    }
    
    /// <summary>
    /// Analyzes a group of options trades to determine the spread type (legacy method for compatibility)
    /// </summary>
    private static SpreadType AnalyzeSpreadType(List<Trade> trades)
    {
        if (trades.Count < 2)
            return SpreadType.Single;
            
        int buyCount = 0;
        int sellCount = 0;
        int callCount = 0;
        int putCount = 0;
        
        foreach (var trade in trades)
        {
            if (trade.Type.Contains("BUY", StringComparison.OrdinalIgnoreCase))
                buyCount++;
            else if (trade.Type.Contains("SELL", StringComparison.OrdinalIgnoreCase))
                sellCount++;
            
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
            
        // Same strike, different type = Straddle/Strangle
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
                var netPremium = CalculateNetPremium(trades);
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
