using System.Globalization;
using Microsoft.EntityFrameworkCore;
using TradingJournal.Api.Data;
using TradingJournal.Api.Models;

namespace TradingJournal.Api.Services.Import;

public class FidelityImporter : ITradeImporter
{
    private readonly ApplicationDbContext _context;

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
                
                // Only process BUY and SELL actions for trades
                if (!action.Contains("BOUGHT", StringComparison.OrdinalIgnoreCase) && 
                    !action.Contains("SOLD", StringComparison.OrdinalIgnoreCase))
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

                // Determine trade type
                var tradeType = action.Contains("BOUGHT", StringComparison.OrdinalIgnoreCase) ? "BUY" : "SELL";
                
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
                    UpdatedAt = DateTime.UtcNow
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
                    Notes = description
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
        }

        return result;
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
