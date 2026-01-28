namespace TradingJournal.Web.Models;

public class AccountDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
}

public class TradeDto
{
    public string Id { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public double Quantity { get; set; }
    public double Price { get; set; }
    public double Fee { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string? Notes { get; set; }
    public string AccountId { get; set; } = string.Empty;
    
    // Options fields
    public string InstrumentType { get; set; } = "Stock";
    public string? OptionType { get; set; }
    public double? StrikePrice { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public string? UnderlyingSymbol { get; set; }
    public int ContractMultiplier { get; set; } = 100;
    public string SpreadType { get; set; } = "Single";
    public string? SpreadGroupId { get; set; }
    public int? SpreadLegNumber { get; set; }
    public bool? IsOpeningTrade { get; set; }
    
    // Computed properties for display
    public bool IsOption => InstrumentType == "Option";
    public double NotionalValue => IsOption ? Price * Quantity * ContractMultiplier : Price * Quantity;
    public string OptionDescription => IsOption && StrikePrice.HasValue && ExpirationDate.HasValue
        ? $"{UnderlyingSymbol ?? Symbol} {StrikePrice:F0}{(OptionType == "Call" ? "C" : "P")} {ExpirationDate:MM/dd/yyyy}"
        : Symbol;
}

public class PortfolioDto
{
    public string Id { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public double Quantity { get; set; }
    public double AveragePrice { get; set; }
    public double? CurrentPrice { get; set; }
    public string AccountId { get; set; } = string.Empty;
    public AccountDto? Account { get; set; }
}

public class PortfolioPerformanceDto
{
    public double TotalValue { get; set; }
    public double TotalCost { get; set; }
    public double TotalPnL { get; set; }
    public double TotalPnLPercent { get; set; }
    public int Holdings { get; set; }
}

// Dividend DTOs
public class DividendDto
{
    public string Id { get; set; } = string.Empty;
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
    public double TaxWithheld { get; set; }
    public string AccountId { get; set; } = string.Empty;
    public AccountDto? Account { get; set; }
}

public class CreateDividendDto
{
    public string Symbol { get; set; } = string.Empty;
    public double Amount { get; set; }
    public double? Quantity { get; set; }
    public double? PerShareAmount { get; set; }
    public string Type { get; set; } = "CASH";
    public string Currency { get; set; } = "USD";
    public DateTime PaymentDate { get; set; } = DateTime.Now;
    public DateTime? ExDividendDate { get; set; }
    public DateTime? RecordDate { get; set; }
    public string? Notes { get; set; }
    public double TaxWithheld { get; set; }
    public string AccountId { get; set; } = string.Empty;
}

public class DividendSummaryDto
{
    public double TotalDividends { get; set; }
    public double TotalTaxWithheld { get; set; }
    public double NetDividends { get; set; }
    public int TotalPayments { get; set; }
    public int UniqueSymbols { get; set; }
    public double YtdDividends { get; set; }
    public double LastMonthDividends { get; set; }
    public List<MonthlyDividendDto> MonthlyBreakdown { get; set; } = new();
    public List<DividendBySymbolDto> TopSymbols { get; set; } = new();
}

public class MonthlyDividendDto
{
    public string Month { get; set; } = string.Empty;
    public int Year { get; set; }
    public double Amount { get; set; }
    public int PaymentCount { get; set; }
}

public class DividendBySymbolDto
{
    public string Symbol { get; set; } = string.Empty;
    public double TotalAmount { get; set; }
    public double TotalTaxWithheld { get; set; }
    public double NetAmount { get; set; }
    public int PaymentCount { get; set; }
    public DateTime? LastPaymentDate { get; set; }
    public double? LastPaymentAmount { get; set; }
}

public class ClearTradesResultDto
{
    public bool Success { get; set; }
    public int DeletedCount { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string? Error { get; set; }
}

public class PortfolioWithQuoteDto
{
    public string Id { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public double Quantity { get; set; }
    public double AveragePrice { get; set; }
    public double CostBasis { get; set; }
    public double CurrentPrice { get; set; }
    public double MarketValue { get; set; }
    public double UnrealizedPnL { get; set; }
    public double UnrealizedPnLPercent { get; set; }
    public double DayChange { get; set; }
    public double DayChangePercent { get; set; }
    public double DayHigh { get; set; }
    public double DayLow { get; set; }
    public long Volume { get; set; }
    public string MarketState { get; set; } = string.Empty;
    public DateTime LastUpdated { get; set; }
    public string? AccountId { get; set; }
    public string? AccountName { get; set; }
    
    // Options fields
    public string InstrumentType { get; set; } = "Stock";
    public string? OptionType { get; set; }
    public double? StrikePrice { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public string? UnderlyingSymbol { get; set; }
    public string? SpreadType { get; set; }
    public string? SpreadGroupId { get; set; }
    public int? SpreadLegNumber { get; set; }
}

/// <summary>
/// Open options position grouped by spread strategy
/// </summary>
public class OptionSpreadGroupDto
{
    public string SpreadGroupId { get; set; } = string.Empty;
    public string SpreadType { get; set; } = string.Empty;
    public string StrategyName { get; set; } = string.Empty;
    public string UnderlyingSymbol { get; set; } = string.Empty;
    public DateTime? ExpirationDate { get; set; }
    public DateTime TradeDate { get; set; }
    public double NetPremium { get; set; }
    public int LegCount { get; set; }
    public bool IsOpen { get; set; }
    public string? AccountId { get; set; }
    public string? AccountName { get; set; }
    public List<OptionLegDto> Legs { get; set; } = new();
}

public class OptionLegDto
{
    public string Id { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string OptionType { get; set; } = string.Empty;
    public double StrikePrice { get; set; }
    public double Quantity { get; set; }
    public double Price { get; set; }
    public double Premium { get; set; }
    public int LegNumber { get; set; }
    public DateTime? ExpirationDate { get; set; }
}

public class CreateAccountDto
{
    public string Name { get; set; } = string.Empty;
    public string Currency { get; set; } = "USD";
}

public class CreateTradeDto
{
    public string Symbol { get; set; } = string.Empty;
    public string Type { get; set; } = "BUY";
    public double Quantity { get; set; }
    public double Price { get; set; }
    public double Fee { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTime Date { get; set; } = DateTime.Now;
    public string? Notes { get; set; }
    public string AccountId { get; set; } = string.Empty;
    
    // Options fields
    public string InstrumentType { get; set; } = "Stock";
    public string? OptionType { get; set; }
    public double? StrikePrice { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public string? UnderlyingSymbol { get; set; }
    public int ContractMultiplier { get; set; } = 100;
    public string SpreadType { get; set; } = "Single";
    public string? SpreadGroupId { get; set; }
    public int? SpreadLegNumber { get; set; }
    public bool? IsOpeningTrade { get; set; }
}

// Dashboard DTOs
public class DashboardMetricsDto
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
    public List<DateValueDto> DailyPnL { get; set; } = new();
    public List<DateValueDto> CumulativePnL { get; set; } = new();
    public List<DateValueDto> EquityCurve { get; set; } = new();
    public List<DateValueDto> DailyDividends { get; set; } = new();
    public List<DateValueDto> CumulativeDividends { get; set; } = new();
    
    // Date range info
    public DateTime? FilterStartDate { get; set; }
    public DateTime? FilterEndDate { get; set; }
    public string? FilterAccountId { get; set; }
    public string? FilterAccountName { get; set; }
}

public class DateValueDto
{
    public DateTime Date { get; set; }
    public double Value { get; set; }
    public string? Label { get; set; }
}

// Summary DTOs
public class TradingSummaryDto
{
    public DateTime? FilterStartDate { get; set; }
    public DateTime? FilterEndDate { get; set; }
    public string? FilterAccountId { get; set; }
    public string? FilterAccountName { get; set; }
    public PnLStatisticsDto PnL { get; set; } = new();
    public PerformanceScoresDto Scores { get; set; } = new();
    public TradeStatisticsDto TradeStats { get; set; } = new();
    public SymbolStatisticsDto SymbolStats { get; set; } = new();
    public TimeStatisticsDto TimeStats { get; set; } = new();
    public CommissionStatisticsDto CommissionStats { get; set; } = new();
    public DividendStatisticsDto DividendStats { get; set; } = new();
}

public class PnLStatisticsDto
{
    public double TotalPnL { get; set; }
    public double TotalProfit { get; set; }
    public double TotalLoss { get; set; }
    public double MaxProfit { get; set; }
    public double MaxLoss { get; set; }
    public double AvgProfit { get; set; }
    public double AvgLoss { get; set; }
    public double AvgPnL { get; set; }
    public List<PeriodPnLDto> YearlyPnL { get; set; } = new();
    public List<PeriodPnLDto> MonthlyPnL { get; set; } = new();
    public List<PeriodPnLDto> WeeklyPnL { get; set; } = new();
}

public class PeriodPnLDto
{
    public string Period { get; set; } = string.Empty;
    public double PnL { get; set; }
    public int Trades { get; set; }
    public double WinRate { get; set; }
}

public class PerformanceScoresDto
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

public class TradeStatisticsDto
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

public class SymbolStatisticsDto
{
    public int UniqueSymbols { get; set; }
    public SymbolStatDto? BestSymbol { get; set; }
    public SymbolStatDto? WorstSymbol { get; set; }
    public SymbolStatDto? MostTraded { get; set; }
    public List<SymbolStatDto> TopProfitable { get; set; } = new();
    public List<SymbolStatDto> TopLosing { get; set; } = new();
    public List<SymbolStatDto> MostActive { get; set; } = new();
}

public class SymbolStatDto
{
    public string Symbol { get; set; } = string.Empty;
    public double PnL { get; set; }
    public int TradeCount { get; set; }
    public double Volume { get; set; }
    public double WinRate { get; set; }
}

public class TimeStatisticsDto
{
    public string? BestTradingDay { get; set; }
    public double BestTradingDayPnL { get; set; }
    public string? WorstTradingDay { get; set; }
    public double WorstTradingDayPnL { get; set; }
    public string? BestMonth { get; set; }
    public double BestMonthPnL { get; set; }
    public string? WorstMonth { get; set; }
    public double WorstMonthPnL { get; set; }
    public List<DayOfWeekStatDto> DayOfWeekStats { get; set; } = new();
}

public class DayOfWeekStatDto
{
    public string DayName { get; set; } = string.Empty;
    public double TotalPnL { get; set; }
    public int TradeCount { get; set; }
    public double WinRate { get; set; }
    public double AvgPnL { get; set; }
}

public class CommissionStatisticsDto
{
    public double TotalCommissions { get; set; }
    public double AvgCommissionPerTrade { get; set; }
    public double AvgCommissionPerDay { get; set; }
    public double CommissionAsPercentOfProfit { get; set; }
}

public class DividendStatisticsDto
{
    public double TotalDividends { get; set; }
    public int DividendPayments { get; set; }
    public double AvgDividendPerPayment { get; set; }
    public int DividendSymbols { get; set; }
    public string? TopDividendSymbol { get; set; }
    public double TopDividendAmount { get; set; }
}
