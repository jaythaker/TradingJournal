using Microsoft.EntityFrameworkCore;
using TradingJournal.Api.Data;
using TradingJournal.Api.Models;

namespace TradingJournal.Api.Services;

public class PortfolioService : IPortfolioService
{
    private readonly ApplicationDbContext _context;

    public PortfolioService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Portfolio>> GetPortfolioByUserIdAsync(string userId, string? accountId = null)
    {
        var query = _context.Portfolios
            .Include(p => p.Account)
            .Where(p => p.UserId == userId);

        if (!string.IsNullOrEmpty(accountId))
        {
            query = query.Where(p => p.AccountId == accountId);
        }

        return await query.ToListAsync();
    }

    public async Task<PortfolioPerformance> GetPerformanceAsync(string userId, string? accountId = null)
    {
        // Calculate realized P&L from all trades
        var tradesQuery = _context.Trades.Where(t => t.UserId == userId);
        if (!string.IsNullOrEmpty(accountId))
        {
            tradesQuery = tradesQuery.Where(t => t.AccountId == accountId);
        }
        var trades = await tradesQuery.ToListAsync();

        // Calculate realized P&L using FIFO method per symbol
        var realizedPnL = CalculateRealizedPnL(trades);

        // Get current holdings
        var portfolios = await GetPortfolioByUserIdAsync(userId, accountId);
        var portfolioList = portfolios.ToList();

        // Total cost basis of current holdings
        var totalCost = portfolioList.Sum(p => p.AveragePrice * p.Quantity);
        
        // For unrealized P&L, we'd need current prices - for now show realized only
        var totalValue = totalCost; // Without market data, value = cost (no unrealized)

        return new PortfolioPerformance
        {
            TotalValue = totalCost,
            TotalCost = totalCost,
            TotalPnL = realizedPnL,
            TotalPnLPercent = realizedPnL != 0 && totalCost > 0 ? (realizedPnL / totalCost) * 100 : 0,
            Holdings = portfolioList.Count
        };
    }

    private double CalculateRealizedPnL(List<Trade> trades)
    {
        // Group trades by symbol and calculate realized P&L using FIFO
        var tradesBySymbol = trades.GroupBy(t => t.Symbol);
        double totalRealizedPnL = 0;

        foreach (var symbolGroup in tradesBySymbol)
        {
            // Order by date, then ensure BUYs come before SELLs on the same day
            var orderedTrades = symbolGroup.OrderBy(t => t.Date).ThenBy(t => t.Type == "SELL" ? 1 : 0).ToList();
            var buyQueue = new Queue<(double Quantity, double Price, double Fee)>();
            double symbolRealizedPnL = 0;

            foreach (var trade in orderedTrades)
            {
                if (trade.Type == "BUY")
                {
                    buyQueue.Enqueue((trade.Quantity, trade.Price, trade.Fee));
                }
                else if (trade.Type == "SELL")
                {
                    double sellQuantity = trade.Quantity;
                    double sellProceeds = trade.Quantity * trade.Price - trade.Fee;
                    double costBasis = 0;

                    // Match with buys using FIFO
                    while (sellQuantity > 0 && buyQueue.Count > 0)
                    {
                        var buy = buyQueue.Dequeue();
                        if (buy.Quantity <= sellQuantity)
                        {
                            // Use entire buy lot
                            costBasis += buy.Quantity * buy.Price + (buy.Fee * buy.Quantity / buy.Quantity);
                            sellQuantity -= buy.Quantity;
                        }
                        else
                        {
                            // Partial use of buy lot
                            costBasis += sellQuantity * buy.Price + (buy.Fee * sellQuantity / buy.Quantity);
                            var remaining = buy.Quantity - sellQuantity;
                            var remainingFee = buy.Fee * (remaining / buy.Quantity);
                            buyQueue = new Queue<(double, double, double)>(
                                new[] { (remaining, buy.Price, remainingFee) }.Concat(buyQueue));
                            sellQuantity = 0;
                        }
                    }

                    symbolRealizedPnL += sellProceeds - costBasis;
                }
            }

            totalRealizedPnL += symbolRealizedPnL;
        }

        return totalRealizedPnL;
    }

    public async Task UpdatePortfolioAsync(string userId, string accountId, string symbol)
    {
        var trades = await _context.Trades
            .Where(t => t.UserId == userId && t.AccountId == accountId && t.Symbol == symbol)
            // Order by date, then ensure BUYs come before SELLs on the same day
            .OrderBy(t => t.Date)
            .ThenBy(t => t.Type == "SELL" ? 1 : 0)
            .ToListAsync();

        // Use FIFO to calculate current position and average cost
        var buyQueue = new Queue<(double Quantity, double Price)>();
        
        foreach (var trade in trades)
        {
            if (trade.Type == "BUY")
            {
                buyQueue.Enqueue((trade.Quantity, trade.Price));
            }
            else if (trade.Type == "SELL")
            {
                double sellQuantity = trade.Quantity;
                while (sellQuantity > 0 && buyQueue.Count > 0)
                {
                    var buy = buyQueue.Dequeue();
                    if (buy.Quantity <= sellQuantity)
                    {
                        sellQuantity -= buy.Quantity;
                    }
                    else
                    {
                        var remaining = buy.Quantity - sellQuantity;
                        buyQueue = new Queue<(double, double)>(
                            new[] { (remaining, buy.Price) }.Concat(buyQueue));
                        sellQuantity = 0;
                    }
                }
            }
        }

        // Calculate remaining position
        double totalQuantity = buyQueue.Sum(b => b.Quantity);
        double totalCost = buyQueue.Sum(b => b.Quantity * b.Price);

        // Handle floating-point precision issues - treat very small quantities as 0
        if (Math.Abs(totalQuantity) < 0.0001)
        {
            totalQuantity = 0;
        }

        if (totalQuantity <= 0)
        {
            // Remove portfolio entry if no holdings
            var portfolio = await _context.Portfolios
                .FirstOrDefaultAsync(p => p.UserId == userId && p.AccountId == accountId && p.Symbol == symbol);
            if (portfolio != null)
            {
                _context.Portfolios.Remove(portfolio);
                await _context.SaveChangesAsync();
            }
        }
        else
        {
            var averagePrice = totalCost / totalQuantity;

            var portfolio = await _context.Portfolios
                .FirstOrDefaultAsync(p => p.UserId == userId && p.AccountId == accountId && p.Symbol == symbol);

            if (portfolio == null)
            {
                portfolio = new Portfolio
                {
                    UserId = userId,
                    AccountId = accountId,
                    Symbol = symbol,
                    Quantity = totalQuantity,
                    AveragePrice = averagePrice,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.Portfolios.Add(portfolio);
            }
            else
            {
                portfolio.Quantity = totalQuantity;
                portfolio.AveragePrice = averagePrice;
                portfolio.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
        }
    }

    public async Task RecalculateAllAsync(string userId)
    {
        // Get all unique symbol/account combinations from trades
        var tradeGroups = await _context.Trades
            .Where(t => t.UserId == userId)
            .Select(t => new { t.AccountId, t.Symbol })
            .Distinct()
            .ToListAsync();

        foreach (var group in tradeGroups)
        {
            await UpdatePortfolioAsync(userId, group.AccountId, group.Symbol);
        }
    }
}
