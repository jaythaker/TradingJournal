namespace TradingJournal.Api.Services.Import;

public interface ITradeImporter
{
    string FormatName { get; }
    string FormatDescription { get; }
    Task<ImportResult> ImportAsync(Stream csvStream, string userId, string accountId);
    bool CanParse(string[] headers);
}

public class ImportResult
{
    public bool Success { get; set; }
    public int ImportedCount { get; set; }
    public int DividendsImportedCount { get; set; }
    public int SkippedCount { get; set; }
    public int ErrorCount { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<ImportedTradeDto> ImportedTrades { get; set; } = new();
}

public class ImportedTradeDto
{
    public string Symbol { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public double Quantity { get; set; }
    public double Price { get; set; }
    public double Fee { get; set; }
    public DateTime Date { get; set; }
    public string? Notes { get; set; }
}
