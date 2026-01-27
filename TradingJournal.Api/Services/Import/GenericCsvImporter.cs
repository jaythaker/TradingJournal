using System.Globalization;
using Microsoft.EntityFrameworkCore;
using TradingJournal.Api.Data;
using TradingJournal.Api.Models;

namespace TradingJournal.Api.Services.Import;

public class GenericCsvImporter : ITradeImporter
{
    private readonly ApplicationDbContext _context;

    public GenericCsvImporter(ApplicationDbContext context)
    {
        _context = context;
    }

    public string FormatName => "Generic";
    public string FormatDescription => "Generic CSV (Symbol, Type, Quantity, Price, Fee, Date)";

    private async Task<bool> IsDuplicateTradeAsync(string userId, string accountId, string symbol, DateTime date, string type, double quantity, double price)
    {
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

    public bool CanParse(string[] headers)
    {
        // Generic format requires specific columns
        var lowerHeaders = headers.Select(h => h.ToLowerInvariant()).ToArray();
        return lowerHeaders.Contains("symbol") && 
               lowerHeaders.Contains("quantity") && 
               lowerHeaders.Contains("price");
    }

    public async Task<ImportResult> ImportAsync(Stream csvStream, string userId, string accountId)
    {
        var result = new ImportResult { Success = true };
        
        using var reader = new StreamReader(csvStream);
        var content = await reader.ReadToEndAsync();
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length < 2)
        {
            result.Success = false;
            result.Errors.Add("CSV file must have a header row and at least one data row.");
            return result;
        }

        var headers = ParseCsvLine(lines[0].Trim());
        var lowerHeaders = headers.Select(h => h.ToLowerInvariant().Trim()).ToArray();

        // Get column indices
        int symbolIndex = Array.IndexOf(lowerHeaders, "symbol");
        int typeIndex = Array.IndexOf(lowerHeaders, "type");
        int quantityIndex = Array.IndexOf(lowerHeaders, "quantity");
        int priceIndex = Array.IndexOf(lowerHeaders, "price");
        int feeIndex = Array.IndexOf(lowerHeaders, "fee");
        int dateIndex = Array.IndexOf(lowerHeaders, "date");
        int notesIndex = Array.IndexOf(lowerHeaders, "notes");

        if (symbolIndex == -1 || quantityIndex == -1 || priceIndex == -1)
        {
            result.Success = false;
            result.Errors.Add("CSV must have 'Symbol', 'Quantity', and 'Price' columns.");
            return result;
        }

        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            try
            {
                var fields = ParseCsvLine(line);

                var symbol = fields[symbolIndex].Trim().ToUpperInvariant();
                var type = typeIndex >= 0 && typeIndex < fields.Length ? fields[typeIndex].Trim().ToUpperInvariant() : "BUY";
                
                if (!double.TryParse(fields[quantityIndex], out var quantity))
                {
                    result.Errors.Add($"Row {i + 1}: Invalid quantity");
                    result.ErrorCount++;
                    continue;
                }

                if (!double.TryParse(fields[priceIndex], out var price))
                {
                    result.Errors.Add($"Row {i + 1}: Invalid price");
                    result.ErrorCount++;
                    continue;
                }

                double fee = 0;
                if (feeIndex >= 0 && feeIndex < fields.Length)
                {
                    double.TryParse(fields[feeIndex], out fee);
                }

                var date = DateTime.UtcNow;
                if (dateIndex >= 0 && dateIndex < fields.Length)
                {
                    DateTime.TryParse(fields[dateIndex], out date);
                }

                var notes = notesIndex >= 0 && notesIndex < fields.Length ? fields[notesIndex] : null;

                // Normalize type
                if (type != "BUY" && type != "SELL")
                {
                    type = quantity >= 0 ? "BUY" : "SELL";
                }

                var absQuantity = Math.Abs(quantity);
                var tradeDate = DateTime.SpecifyKind(date, DateTimeKind.Utc);

                // Check for duplicate trade
                if (await IsDuplicateTradeAsync(userId, accountId, symbol, tradeDate, type, absQuantity, price))
                {
                    result.SkippedCount++;
                    result.Errors.Add($"Row {i + 1}: Duplicate trade skipped ({symbol} {type} {absQuantity} @ {price:C2} on {date:d})");
                    continue;
                }

                var trade = new Trade
                {
                    Symbol = symbol,
                    Type = type,
                    Quantity = absQuantity,
                    Price = price,
                    Fee = fee,
                    Currency = "USD",
                    Date = tradeDate,
                    Notes = notes,
                    AccountId = accountId,
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Trades.Add(trade);
                result.ImportedTrades.Add(new ImportedTradeDto
                {
                    Symbol = symbol,
                    Type = type,
                    Quantity = absQuantity,
                    Price = price,
                    Fee = fee,
                    Date = date,
                    Notes = notes
                });
                result.ImportedCount++;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Row {i + 1}: {ex.Message}");
                result.ErrorCount++;
            }
        }

        if (result.ImportedCount > 0)
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
