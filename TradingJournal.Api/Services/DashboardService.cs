using Microsoft.EntityFrameworkCore;
using TradingJournal.Api.Data;

namespace TradingJournal.Api.Services;

public class DashboardService : IDashboardService
{
    private readonly ApplicationDbContext _context;
    private readonly IPortfolioService _portfolioService;

    public DashboardService(ApplicationDbContext context, IPortfolioService portfolioService)
    {
        _context = context;
        _portfolioService = portfolioService;
    }

    public async Task<DashboardMetrics> GetDashboardMetricsAsync(string userId, string? accountId = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        // Convert dates to UTC for PostgreSQL compatibility
        var startDateUtc = startDate.HasValue ? DateTime.SpecifyKind(startDate.Value, DateTimeKind.Utc) : (DateTime?)null;
        var endDateUtc = endDate.HasValue ? DateTime.SpecifyKind(endDate.Value.AddDays(1).AddTicks(-1), DateTimeKind.Utc) : (DateTime?)null;

        var metrics = new DashboardMetrics
        {
            FilterStartDate = startDate,
            FilterEndDate = endDate,
            FilterAccountId = accountId
        };

        // Get account name if filtered
        if (!string.IsNullOrEmpty(accountId))
        {
            var account = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == userId);
            metrics.FilterAccountName = account?.Name;
        }

        // Base query for trades
        var tradesQuery = _context.Trades.Where(t => t.UserId == userId);
        if (!string.IsNullOrEmpty(accountId))
        {
            tradesQuery = tradesQuery.Where(t => t.AccountId == accountId);
        }
        if (startDateUtc.HasValue)
        {
            tradesQuery = tradesQuery.Where(t => t.Date >= startDateUtc.Value);
        }
        if (endDateUtc.HasValue)
        {
            tradesQuery = tradesQuery.Where(t => t.Date <= endDateUtc.Value);
        }

        var trades = await tradesQuery.OrderBy(t => t.Date).ThenBy(t => t.Type == "SELL" ? 1 : 0).ToListAsync();

        // Base query for dividends
        var dividendsQuery = _context.Dividends.Where(d => d.UserId == userId);
        if (!string.IsNullOrEmpty(accountId))
        {
            dividendsQuery = dividendsQuery.Where(d => d.AccountId == accountId);
        }
        if (startDateUtc.HasValue)
        {
            dividendsQuery = dividendsQuery.Where(d => d.PaymentDate >= startDateUtc.Value);
        }
        if (endDateUtc.HasValue)
        {
            dividendsQuery = dividendsQuery.Where(d => d.PaymentDate <= endDateUtc.Value);
        }

        var dividends = await dividendsQuery.OrderBy(d => d.PaymentDate).ToListAsync();

        // Calculate P&L using FIFO method
        var symbolTrades = trades.GroupBy(t => t.Symbol);
        var dailyPnL = new Dictionary<DateTime, double>();
        int winningTrades = 0;
        int losingTrades = 0;
        double totalRealizedPnL = 0;

        foreach (var symbolGroup in symbolTrades)
        {
            var buyQueue = new Queue<(double Quantity, double Price, DateTime Date)>();
            
            foreach (var trade in symbolGroup.OrderBy(t => t.Date).ThenBy(t => t.Type == "SELL" ? 1 : 0))
            {
                if (trade.Type == "BUY")
                {
                    buyQueue.Enqueue((trade.Quantity, trade.Price, trade.Date));
                }
                else if (trade.Type == "SELL")
                {
                    double remainingQty = trade.Quantity;
                    double sellProceeds = trade.Quantity * trade.Price - trade.Fee;
                    double costBasis = 0;

                    while (remainingQty > 0 && buyQueue.Count > 0)
                    {
                        var buy = buyQueue.Peek();
                        double qtyToUse = Math.Min(remainingQty, buy.Quantity);
                        costBasis += qtyToUse * buy.Price;
                        remainingQty -= qtyToUse;

                        if (qtyToUse >= buy.Quantity)
                        {
                            buyQueue.Dequeue();
                        }
                        else
                        {
                            buyQueue.Dequeue();
                            buyQueue = new Queue<(double, double, DateTime)>(
                                new[] { (buy.Quantity - qtyToUse, buy.Price, buy.Date) }.Concat(buyQueue));
                        }
                    }

                    double pnl = sellProceeds - costBasis;
                    totalRealizedPnL += pnl;

                    // Track wins/losses
                    if (pnl > 0)
                        winningTrades++;
                    else if (pnl < 0)
                        losingTrades++;

                    // Track daily P&L
                    var tradeDate = trade.Date.Date;
                    if (!dailyPnL.ContainsKey(tradeDate))
                        dailyPnL[tradeDate] = 0;
                    dailyPnL[tradeDate] += pnl;
                }
            }
        }

        // Calculate metrics
        metrics.TotalTrades = trades.Count;
        metrics.WinningTrades = winningTrades;
        metrics.LosingTrades = losingTrades;
        metrics.TotalRealizedPnL = totalRealizedPnL;
        
        int totalClosedTrades = winningTrades + losingTrades;
        metrics.WinRate = totalClosedTrades > 0 ? (double)winningTrades / totalClosedTrades * 100 : 0;
        
        metrics.TradingDays = dailyPnL.Count;
        metrics.AvgWinsPerDay = metrics.TradingDays > 0 ? (double)winningTrades / metrics.TradingDays : 0;
        metrics.AvgPnLPerDay = metrics.TradingDays > 0 ? totalRealizedPnL / metrics.TradingDays : 0;

        // Calculate dividend totals
        metrics.TotalDividends = dividends.Sum(d => d.Amount);

        // Get portfolio value
        var portfolioQuery = _context.Portfolios.Where(p => p.UserId == userId && p.Quantity > 0);
        if (!string.IsNullOrEmpty(accountId))
        {
            portfolioQuery = portfolioQuery.Where(p => p.AccountId == accountId);
        }
        var portfolio = await portfolioQuery.ToListAsync();
        metrics.PortfolioCost = portfolio.Sum(p => p.Quantity * p.AveragePrice);
        metrics.PortfolioValue = portfolio.Sum(p => p.Quantity * (p.CurrentPrice ?? p.AveragePrice));
        metrics.UnrealizedPnL = metrics.PortfolioValue - metrics.PortfolioCost;

        // Build time series data
        var allDates = dailyPnL.Keys
            .Union(dividends.Select(d => d.PaymentDate.Date))
            .OrderBy(d => d)
            .ToList();

        if (allDates.Any())
        {
            // Fill in missing dates for continuous chart
            var minDate = allDates.Min();
            var maxDate = allDates.Max();
            var fullDateRange = Enumerable.Range(0, (maxDate - minDate).Days + 1)
                .Select(offset => minDate.AddDays(offset))
                .ToList();

            double cumulativePnL = 0;
            double cumulativeDividends = 0;
            double equityValue = metrics.PortfolioCost; // Start with cost basis

            foreach (var date in fullDateRange)
            {
                // Daily P&L
                double dayPnL = dailyPnL.ContainsKey(date) ? dailyPnL[date] : 0;
                metrics.DailyPnL.Add(new DateValue { Date = date, Value = Math.Round(dayPnL, 2) });

                // Cumulative P&L
                cumulativePnL += dayPnL;
                metrics.CumulativePnL.Add(new DateValue { Date = date, Value = Math.Round(cumulativePnL, 2) });

                // Equity curve (cumulative P&L + dividends + unrealized gains)
                equityValue += dayPnL;
                var dayDividends = dividends.Where(d => d.PaymentDate.Date == date).Sum(d => d.Amount);
                equityValue += dayDividends;
                metrics.EquityCurve.Add(new DateValue { Date = date, Value = Math.Round(equityValue, 2) });

                // Daily dividends
                metrics.DailyDividends.Add(new DateValue { Date = date, Value = Math.Round(dayDividends, 2) });

                // Cumulative dividends
                cumulativeDividends += dayDividends;
                metrics.CumulativeDividends.Add(new DateValue { Date = date, Value = Math.Round(cumulativeDividends, 2) });
            }
        }

        return metrics;
    }
}
