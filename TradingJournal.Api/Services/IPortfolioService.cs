using TradingJournal.Api.Models;

namespace TradingJournal.Api.Services;

public interface IPortfolioService
{
    Task<IEnumerable<Portfolio>> GetPortfolioByUserIdAsync(string userId, string? accountId = null);
    Task<PortfolioPerformance> GetPerformanceAsync(string userId, string? accountId = null);
    Task UpdatePortfolioAsync(string userId, string accountId, string symbol);
    Task RecalculateAllAsync(string userId);
}

public class PortfolioPerformance
{
    public double TotalValue { get; set; }
    public double TotalCost { get; set; }
    public double TotalPnL { get; set; }
    public double TotalPnLPercent { get; set; }
    public int Holdings { get; set; }
}
