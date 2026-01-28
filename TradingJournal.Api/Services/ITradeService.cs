using TradingJournal.Api.Models;

namespace TradingJournal.Api.Services;

public interface ITradeService
{
    Task<IEnumerable<Trade>> GetTradesByUserIdAsync(string userId, string? accountId = null, DateTime? startDate = null, DateTime? endDate = null);
    Task<Trade?> GetTradeByIdAsync(string tradeId, string userId);
    Task<Trade> CreateTradeAsync(CreateTradeRequest request, string userId);
    Task<Trade> UpdateTradeAsync(string tradeId, UpdateTradeRequest request, string userId);
    Task DeleteTradeAsync(string tradeId, string userId);
    Task<IEnumerable<SymbolTradeSummary>> GetTradeSummaryBySymbolAsync(string userId, string? accountId = null, DateTime? startDate = null, DateTime? endDate = null);
    Task<TimeAnalysisSummary> GetTimeAnalysisAsync(string userId, string? accountId = null, DateTime? startDate = null, DateTime? endDate = null);
    Task<DuplicateCleanupResult> FindAndRemoveDuplicatesAsync(string userId, string? accountId = null);
    Task<DeleteAllTradesResult> DeleteAllTradesForAccountAsync(string userId, string accountId);
}

public class DeleteAllTradesResult
{
    public bool Success { get; set; }
    public int DeletedCount { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string? Error { get; set; }
}

public class DuplicateCleanupResult
{
    public int DuplicatesFound { get; set; }
    public int DuplicatesRemoved { get; set; }
    public List<DuplicateGroup> Groups { get; set; } = new();
}

public class DuplicateGroup
{
    public string Symbol { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public double Quantity { get; set; }
    public double Price { get; set; }
    public int Count { get; set; }
    public int Removed { get; set; }
}

public class SymbolTradeSummary
{
    public string Symbol { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    
    // Buy stats
    public int BuyCount { get; set; }
    public double TotalBuyQuantity { get; set; }
    public double TotalBuyAmount { get; set; }
    public double AverageBuyPrice { get; set; }
    
    // Sell stats
    public int SellCount { get; set; }
    public double TotalSellQuantity { get; set; }
    public double TotalSellAmount { get; set; }
    public double AverageSellPrice { get; set; }
    
    // Current position
    public double CurrentQuantity { get; set; }
    public double CurrentCostBasis { get; set; }
    
    // P&L
    public double RealizedPnL { get; set; }
    public double RealizedPnLPercent { get; set; }
    
    // Trade details
    public List<TradeDetail> Trades { get; set; } = new();
}

public class TradeDetail
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public double Quantity { get; set; }
    public double Price { get; set; }
    public double Fee { get; set; }
    public double Total { get; set; }
    public DateTime Date { get; set; }
    public string? Notes { get; set; }
}

public class TimePeriodSummary
{
    public string Period { get; set; } = string.Empty; // "2026-01" for month, "2026-W01" for week
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TotalTrades { get; set; }
    public int BuyCount { get; set; }
    public int SellCount { get; set; }
    public double TotalBuyAmount { get; set; }
    public double TotalSellAmount { get; set; }
    public double RealizedPnL { get; set; }
    public double RealizedPnLPercent { get; set; }
    public int WinningTrades { get; set; }
    public int LosingTrades { get; set; }
    public double WinRate { get; set; }
    public List<SymbolPnL> SymbolBreakdown { get; set; } = new();
}

public class SymbolPnL
{
    public string Symbol { get; set; } = string.Empty;
    public int TradeCount { get; set; }
    public double RealizedPnL { get; set; }
}

public class TimeAnalysisSummary
{
    public List<TimePeriodSummary> Monthly { get; set; } = new();
    public List<TimePeriodSummary> Weekly { get; set; } = new();
    public double TotalRealizedPnL { get; set; }
    public int TotalTrades { get; set; }
    public double OverallWinRate { get; set; }
}

public class CreateTradeRequest
{
    public string Symbol { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // BUY, SELL, BUY_TO_OPEN, SELL_TO_OPEN, BUY_TO_CLOSE, SELL_TO_CLOSE
    public double Quantity { get; set; }
    public double Price { get; set; }
    public double Fee { get; set; } = 0;
    public string Currency { get; set; } = "USD";
    public DateTime Date { get; set; }
    public string? Notes { get; set; }
    public string AccountId { get; set; } = string.Empty;
    
    // Options fields
    public string InstrumentType { get; set; } = "Stock"; // Stock, Option
    public string? OptionType { get; set; } // Call, Put
    public double? StrikePrice { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public string? UnderlyingSymbol { get; set; }
    public int ContractMultiplier { get; set; } = 100;
    public string SpreadType { get; set; } = "Single"; // Single, CreditSpread, DebitSpread, etc.
    public string? SpreadGroupId { get; set; }
    public int? SpreadLegNumber { get; set; }
    public bool? IsOpeningTrade { get; set; }
}

public class UpdateTradeRequest
{
    public string? Symbol { get; set; }
    public string? Type { get; set; }
    public double? Quantity { get; set; }
    public double? Price { get; set; }
    public double? Fee { get; set; }
    public string? Currency { get; set; }
    public DateTime? Date { get; set; }
    public string? Notes { get; set; }
    public string? AccountId { get; set; }
    
    // Options fields
    public string? InstrumentType { get; set; }
    public string? OptionType { get; set; }
    public double? StrikePrice { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public string? UnderlyingSymbol { get; set; }
    public int? ContractMultiplier { get; set; }
    public string? SpreadType { get; set; }
    public string? SpreadGroupId { get; set; }
    public int? SpreadLegNumber { get; set; }
    public bool? IsOpeningTrade { get; set; }
}
