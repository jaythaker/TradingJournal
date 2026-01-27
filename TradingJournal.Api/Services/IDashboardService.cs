namespace TradingJournal.Api.Services;

public interface IDashboardService
{
    Task<DashboardMetrics> GetDashboardMetricsAsync(string userId, string? accountId = null, DateTime? startDate = null, DateTime? endDate = null);
}

public class DashboardMetrics
{
    // Summary stats
    public int TotalTrades { get; set; }
    public int WinningTrades { get; set; }
    public int LosingTrades { get; set; }
    public double WinRate { get; set; }
    public double TotalRealizedPnL { get; set; }
    public double AvgWinsPerDay { get; set; }
    public double AvgPnLPerDay { get; set; }
    public int TradingDays { get; set; }
    public double TotalDividends { get; set; }
    
    // Portfolio value
    public double PortfolioValue { get; set; }
    public double PortfolioCost { get; set; }
    public double UnrealizedPnL { get; set; }
    
    // Time series data for charts
    public List<DateValue> DailyPnL { get; set; } = new();
    public List<DateValue> CumulativePnL { get; set; } = new();
    public List<DateValue> EquityCurve { get; set; } = new();
    public List<DateValue> DailyDividends { get; set; } = new();
    public List<DateValue> CumulativeDividends { get; set; } = new();
    
    // Date range info
    public DateTime? FilterStartDate { get; set; }
    public DateTime? FilterEndDate { get; set; }
    public string? FilterAccountId { get; set; }
    public string? FilterAccountName { get; set; }
}

public class DateValue
{
    public DateTime Date { get; set; }
    public double Value { get; set; }
    public string? Label { get; set; }
}
