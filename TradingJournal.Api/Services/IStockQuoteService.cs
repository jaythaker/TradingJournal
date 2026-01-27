using YahooFinanceApi;

namespace TradingJournal.Api.Services;

public interface IStockQuoteService
{
    Task<Dictionary<string, StockQuote>> GetQuotesAsync(IEnumerable<string> symbols);
    Task<StockQuote?> GetQuoteAsync(string symbol);
}

public class StockQuote
{
    public string Symbol { get; set; } = string.Empty;
    public double Price { get; set; }
    public double Change { get; set; }
    public double ChangePercent { get; set; }
    public double PreviousClose { get; set; }
    public double DayHigh { get; set; }
    public double DayLow { get; set; }
    public long Volume { get; set; }
    public string MarketState { get; set; } = string.Empty;
    public DateTime LastUpdated { get; set; }
}

public class StockQuoteService : IStockQuoteService
{
    private readonly ILogger<StockQuoteService> _logger;
    private readonly Dictionary<string, (StockQuote Quote, DateTime CachedAt)> _cache = new();
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(1); // Cache for 1 minute

    public StockQuoteService(ILogger<StockQuoteService> logger)
    {
        _logger = logger;
    }

    public async Task<StockQuote?> GetQuoteAsync(string symbol)
    {
        var quotes = await GetQuotesAsync(new[] { symbol });
        return quotes.TryGetValue(symbol, out var quote) ? quote : null;
    }

    public async Task<Dictionary<string, StockQuote>> GetQuotesAsync(IEnumerable<string> symbols)
    {
        var result = new Dictionary<string, StockQuote>();
        var symbolsToFetch = new List<string>();

        // Check cache first
        foreach (var symbol in symbols)
        {
            if (_cache.TryGetValue(symbol.ToUpper(), out var cached) && 
                DateTime.UtcNow - cached.CachedAt < _cacheDuration)
            {
                result[symbol.ToUpper()] = cached.Quote;
            }
            else
            {
                symbolsToFetch.Add(symbol.ToUpper());
            }
        }

        if (symbolsToFetch.Count == 0)
        {
            return result;
        }

        try
        {
            _logger.LogInformation("Fetching quotes for: {Symbols}", string.Join(", ", symbolsToFetch));

            var securities = await Yahoo.Symbols(symbolsToFetch.ToArray())
                .Fields(
                    Field.Symbol,
                    Field.RegularMarketPrice,
                    Field.RegularMarketChange,
                    Field.RegularMarketChangePercent,
                    Field.RegularMarketPreviousClose,
                    Field.RegularMarketDayHigh,
                    Field.RegularMarketDayLow,
                    Field.RegularMarketVolume,
                    Field.MarketState
                )
                .QueryAsync();

            foreach (var security in securities)
            {
                var quote = new StockQuote
                {
                    Symbol = security.Key,
                    Price = GetFieldValue<double>(security.Value, Field.RegularMarketPrice),
                    Change = GetFieldValue<double>(security.Value, Field.RegularMarketChange),
                    ChangePercent = GetFieldValue<double>(security.Value, Field.RegularMarketChangePercent),
                    PreviousClose = GetFieldValue<double>(security.Value, Field.RegularMarketPreviousClose),
                    DayHigh = GetFieldValue<double>(security.Value, Field.RegularMarketDayHigh),
                    DayLow = GetFieldValue<double>(security.Value, Field.RegularMarketDayLow),
                    Volume = GetFieldValue<long>(security.Value, Field.RegularMarketVolume),
                    MarketState = GetFieldValue<string>(security.Value, Field.MarketState) ?? "UNKNOWN",
                    LastUpdated = DateTime.UtcNow
                };

                result[security.Key] = quote;
                _cache[security.Key] = (quote, DateTime.UtcNow);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching stock quotes from Yahoo Finance");
        }

        return result;
    }

    private static T? GetFieldValue<T>(Security security, Field field)
    {
        try
        {
            // Use the indexer property directly
            var value = security[field];
            if (value != null)
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
        }
        catch
        {
            // Field not available or conversion failed
        }
        return default;
    }
}
