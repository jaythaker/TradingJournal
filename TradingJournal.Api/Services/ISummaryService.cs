namespace TradingJournal.Api.Services;

public interface ISummaryService
{
    Task<TradingSummary> GetTradingSummaryAsync(string userId, string? accountId = null, DateTime? startDate = null, DateTime? endDate = null);
}

public class TradingSummary
{
    // Filter info
    public DateTime? FilterStartDate { get; set; }
    public DateTime? FilterEndDate { get; set; }
    public string? FilterAccountId { get; set; }
    public string? FilterAccountName { get; set; }

    // PnL Statistics
    public PnLStatistics PnL { get; set; } = new();
    
    // Performance Scores
    public PerformanceScores Scores { get; set; } = new();
    
    // Trade Statistics
    public TradeStatistics TradeStats { get; set; } = new();
    
    // Symbol Statistics
    public SymbolStatistics SymbolStats { get; set; } = new();
    
    // Time-based Statistics
    public TimeStatistics TimeStats { get; set; } = new();
    
    // Commission Statistics
    public CommissionStatistics CommissionStats { get; set; } = new();
    
    // Dividend Statistics
    public DividendStatistics DividendStats { get; set; } = new();
}

public class PnLStatistics
{
    public double TotalPnL { get; set; }
    public double TotalProfit { get; set; }
    public double TotalLoss { get; set; }
    public double MaxProfit { get; set; }
    public double MaxLoss { get; set; }
    public double AvgProfit { get; set; }
    public double AvgLoss { get; set; }
    public double AvgPnL { get; set; }
    
    // Period breakdowns
    public List<PeriodPnL> YearlyPnL { get; set; } = new();
    public List<PeriodPnL> MonthlyPnL { get; set; } = new();
    public List<PeriodPnL> WeeklyPnL { get; set; } = new();
}

public class PeriodPnL
{
    public string Period { get; set; } = string.Empty;
    public double PnL { get; set; }
    public int Trades { get; set; }
    public double WinRate { get; set; }
}

public class PerformanceScores
{
    public double ProfitFactor { get; set; }
    public double GainToPainRatio { get; set; }
    public double KellyCriterion { get; set; }
    public double SystemQualityNumber { get; set; }
    public double TradingExpectancy { get; set; }
    public double MaxDrawdown { get; set; }
    public double MaxDrawdownPercent { get; set; }
    public int MaxConsecutiveWins { get; set; }
    public int MaxConsecutiveLosses { get; set; }
    public double WinLossRatio { get; set; }
    public double AdjustedWinLossRatio { get; set; }
    public double WinRate { get; set; }
    public double LossRate { get; set; }
    public double StandardDeviationPnL { get; set; }
    public double StandardDeviationProfit { get; set; }
    public double StandardDeviationLoss { get; set; }
    public double SharpeRatio { get; set; }
}

public class TradeStatistics
{
    public int TotalTrades { get; set; }
    public int WinningTrades { get; set; }
    public int LosingTrades { get; set; }
    public int BreakEvenTrades { get; set; }
    public int BuyTrades { get; set; }
    public int SellTrades { get; set; }
    public double TotalVolume { get; set; }
    public double AvgTradesPerDay { get; set; }
    public double AvgTradesPerWeek { get; set; }
    public double AvgTradesPerMonth { get; set; }
    public int TradingDays { get; set; }
    public int TotalDaysInPeriod { get; set; }
}

public class SymbolStatistics
{
    public int UniqueSymbols { get; set; }
    public SymbolStat? BestSymbol { get; set; }
    public SymbolStat? WorstSymbol { get; set; }
    public SymbolStat? MostTraded { get; set; }
    public List<SymbolStat> TopProfitable { get; set; } = new();
    public List<SymbolStat> TopLosing { get; set; } = new();
    public List<SymbolStat> MostActive { get; set; } = new();
}

public class SymbolStat
{
    public string Symbol { get; set; } = string.Empty;
    public double PnL { get; set; }
    public int TradeCount { get; set; }
    public double Volume { get; set; }
    public double WinRate { get; set; }
}

public class TimeStatistics
{
    public string? BestTradingDay { get; set; }
    public double BestTradingDayPnL { get; set; }
    public string? WorstTradingDay { get; set; }
    public double WorstTradingDayPnL { get; set; }
    public string? BestMonth { get; set; }
    public double BestMonthPnL { get; set; }
    public string? WorstMonth { get; set; }
    public double WorstMonthPnL { get; set; }
    public List<DayOfWeekStat> DayOfWeekStats { get; set; } = new();
}

public class DayOfWeekStat
{
    public string DayName { get; set; } = string.Empty;
    public double TotalPnL { get; set; }
    public int TradeCount { get; set; }
    public double WinRate { get; set; }
    public double AvgPnL { get; set; }
}

public class CommissionStatistics
{
    public double TotalCommissions { get; set; }
    public double AvgCommissionPerTrade { get; set; }
    public double AvgCommissionPerDay { get; set; }
    public double CommissionAsPercentOfProfit { get; set; }
}

public class DividendStatistics
{
    public double TotalDividends { get; set; }
    public int DividendPayments { get; set; }
    public double AvgDividendPerPayment { get; set; }
    public int DividendSymbols { get; set; }
    public string? TopDividendSymbol { get; set; }
    public double TopDividendAmount { get; set; }
}
