using Microsoft.EntityFrameworkCore;
using TradingJournal.Api.Data;
using TradingJournal.Api.Services;

namespace TradingJournal.Api.Services.Import;

public interface IImportService
{
    IEnumerable<ImportFormatInfo> GetAvailableFormats();
    Task<ImportResult> ImportTradesAsync(Stream csvStream, string userId, string accountId, string? formatName = null);
}

public class ImportFormatInfo
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class ImportService : IImportService
{
    private readonly ApplicationDbContext _context;
    private readonly IPortfolioService _portfolioService;
    private readonly List<ITradeImporter> _importers;

    public ImportService(ApplicationDbContext context, IPortfolioService portfolioService)
    {
        _context = context;
        _portfolioService = portfolioService;
        _importers = new List<ITradeImporter>
        {
            new FidelityImporter(context),
            new GenericCsvImporter(context)
        };
    }

    public IEnumerable<ImportFormatInfo> GetAvailableFormats()
    {
        return _importers.Select(i => new ImportFormatInfo
        {
            Name = i.FormatName,
            Description = i.FormatDescription
        });
    }

    public async Task<ImportResult> ImportTradesAsync(Stream csvStream, string userId, string accountId, string? formatName = null)
    {
        // Verify account belongs to user
        var account = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == userId);
        if (account == null)
        {
            return new ImportResult
            {
                Success = false,
                Errors = new List<string> { "Account not found or access denied" }
            };
        }

        // Read the stream into memory so we can read it multiple times
        using var memoryStream = new MemoryStream();
        await csvStream.CopyToAsync(memoryStream);
        memoryStream.Position = 0;

        // Read first few lines to detect format
        using var reader = new StreamReader(memoryStream, leaveOpen: true);
        var firstLines = new List<string>();
        for (int i = 0; i < 5 && !reader.EndOfStream; i++)
        {
            var line = await reader.ReadLineAsync();
            if (!string.IsNullOrEmpty(line))
            {
                firstLines.Add(line);
            }
        }

        // Find headers
        string[] headers = Array.Empty<string>();
        foreach (var line in firstLines)
        {
            if (line.Contains("Run Date") || line.ToLower().Contains("symbol"))
            {
                headers = ParseCsvLine(line);
                break;
            }
        }

        // Find appropriate importer
        ITradeImporter? importer = null;

        if (!string.IsNullOrEmpty(formatName))
        {
            importer = _importers.FirstOrDefault(i => i.FormatName.Equals(formatName, StringComparison.OrdinalIgnoreCase));
        }

        if (importer == null && headers.Length > 0)
        {
            importer = _importers.FirstOrDefault(i => i.CanParse(headers));
        }

        if (importer == null)
        {
            return new ImportResult
            {
                Success = false,
                Errors = new List<string> { "Could not detect CSV format. Please specify a format or ensure the CSV has the correct headers." }
            };
        }

        // Reset stream and import
        memoryStream.Position = 0;
        var result = await importer.ImportAsync(memoryStream, userId, accountId);

        // Recalculate portfolio after successful import
        if (result.Success && result.ImportedCount > 0)
        {
            await _portfolioService.RecalculateAllAsync(userId);
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
