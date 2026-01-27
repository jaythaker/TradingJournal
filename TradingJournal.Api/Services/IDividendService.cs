using TradingJournal.Api.Models;

namespace TradingJournal.Api.Services;

public interface IDividendService
{
    Task<IEnumerable<Dividend>> GetDividendsByUserIdAsync(string userId, string? accountId = null);
    Task<Dividend?> GetDividendByIdAsync(string dividendId, string userId);
    Task<Dividend> CreateDividendAsync(CreateDividendRequest request, string userId);
    Task<Dividend> UpdateDividendAsync(string dividendId, UpdateDividendRequest request, string userId);
    Task DeleteDividendAsync(string dividendId, string userId);
    Task<DividendSummary> GetDividendSummaryAsync(string userId, string? accountId = null);
    Task<IEnumerable<DividendBySymbol>> GetDividendsBySymbolAsync(string userId, string? accountId = null);
}

public class DividendSummary
{
    public double TotalDividends { get; set; }
    public double TotalTaxWithheld { get; set; }
    public double NetDividends { get; set; }
    public int TotalPayments { get; set; }
    public int UniqueSymbols { get; set; }
    public double YtdDividends { get; set; }
    public double LastMonthDividends { get; set; }
    public List<MonthlyDividend> MonthlyBreakdown { get; set; } = new();
    public List<DividendBySymbol> TopSymbols { get; set; } = new();
}

public class MonthlyDividend
{
    public string Month { get; set; } = string.Empty;
    public int Year { get; set; }
    public double Amount { get; set; }
    public int PaymentCount { get; set; }
}

public class DividendBySymbol
{
    public string Symbol { get; set; } = string.Empty;
    public double TotalAmount { get; set; }
    public double TotalTaxWithheld { get; set; }
    public double NetAmount { get; set; }
    public int PaymentCount { get; set; }
    public DateTime? LastPaymentDate { get; set; }
    public double? LastPaymentAmount { get; set; }
}

public class CreateDividendRequest
{
    public string Symbol { get; set; } = string.Empty;
    public double Amount { get; set; }
    public double? Quantity { get; set; }
    public double? PerShareAmount { get; set; }
    public string Type { get; set; } = "CASH";
    public string Currency { get; set; } = "USD";
    public DateTime PaymentDate { get; set; }
    public DateTime? ExDividendDate { get; set; }
    public DateTime? RecordDate { get; set; }
    public string? Notes { get; set; }
    public double TaxWithheld { get; set; } = 0;
    public string AccountId { get; set; } = string.Empty;
}

public class UpdateDividendRequest
{
    public string? Symbol { get; set; }
    public double? Amount { get; set; }
    public double? Quantity { get; set; }
    public double? PerShareAmount { get; set; }
    public string? Type { get; set; }
    public string? Currency { get; set; }
    public DateTime? PaymentDate { get; set; }
    public DateTime? ExDividendDate { get; set; }
    public DateTime? RecordDate { get; set; }
    public string? Notes { get; set; }
    public double? TaxWithheld { get; set; }
    public string? AccountId { get; set; }
}
