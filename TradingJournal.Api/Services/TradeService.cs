using Microsoft.EntityFrameworkCore;
using TradingJournal.Api.Data;
using TradingJournal.Api.Models;

namespace TradingJournal.Api.Services;

public class TradeService : ITradeService
{
    private readonly ApplicationDbContext _context;
    private readonly IPortfolioService _portfolioService;

    public TradeService(ApplicationDbContext context, IPortfolioService portfolioService)
    {
        _context = context;
        _portfolioService = portfolioService;
    }

    public async Task<IEnumerable<Trade>> GetTradesByUserIdAsync(string userId, string? accountId = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _context.Trades
            .Include(t => t.Account)
            .Where(t => t.UserId == userId);

        if (!string.IsNullOrEmpty(accountId))
        {
            query = query.Where(t => t.AccountId == accountId);
        }

        if (startDate.HasValue)
        {
            var startUtc = DateTime.SpecifyKind(startDate.Value, DateTimeKind.Utc);
            query = query.Where(t => t.Date >= startUtc);
        }

        if (endDate.HasValue)
        {
            var endUtc = DateTime.SpecifyKind(endDate.Value.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
            query = query.Where(t => t.Date <= endUtc);
        }

        return await query
            .OrderByDescending(t => t.Date)
            .ToListAsync();
    }

    public async Task<Trade?> GetTradeByIdAsync(string tradeId, string userId)
    {
        return await _context.Trades
            .Include(t => t.Account)
            .FirstOrDefaultAsync(t => t.Id == tradeId && t.UserId == userId);
    }

    public async Task<Trade> CreateTradeAsync(CreateTradeRequest request, string userId)
    {
        // Verify account belongs to user
        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == request.AccountId && a.UserId == userId);

        if (account == null)
        {
            throw new KeyNotFoundException("Account not found");
        }

        var trade = new Trade
        {
            Symbol = request.Symbol,
            Type = request.Type,
            Quantity = request.Quantity,
            Price = request.Price,
            Fee = request.Fee,
            Currency = request.Currency,
            Date = request.Date,
            Notes = request.Notes,
            AccountId = request.AccountId,
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Trades.Add(trade);
        await _context.SaveChangesAsync();

        // Update portfolio
        await _portfolioService.UpdatePortfolioAsync(userId, request.AccountId, request.Symbol);

        return trade;
    }

    public async Task<Trade> UpdateTradeAsync(string tradeId, UpdateTradeRequest request, string userId)
    {
        var trade = await GetTradeByIdAsync(tradeId, userId);
        if (trade == null)
        {
            throw new KeyNotFoundException("Trade not found");
        }

        var oldSymbol = trade.Symbol;
        var oldAccountId = trade.AccountId;

        if (!string.IsNullOrEmpty(request.Symbol))
            trade.Symbol = request.Symbol;
        if (!string.IsNullOrEmpty(request.Type))
            trade.Type = request.Type;
        if (request.Quantity.HasValue)
            trade.Quantity = request.Quantity.Value;
        if (request.Price.HasValue)
            trade.Price = request.Price.Value;
        if (request.Fee.HasValue)
            trade.Fee = request.Fee.Value;
        if (!string.IsNullOrEmpty(request.Currency))
            trade.Currency = request.Currency;
        if (request.Date.HasValue)
            trade.Date = request.Date.Value;
        if (request.Notes != null)
            trade.Notes = request.Notes;
        if (!string.IsNullOrEmpty(request.AccountId))
            trade.AccountId = request.AccountId;

        trade.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Update portfolio if symbol or account changed
        if (request.Symbol != null || request.AccountId != null)
        {
            await _portfolioService.UpdatePortfolioAsync(userId, oldAccountId, oldSymbol);
            await _portfolioService.UpdatePortfolioAsync(userId, trade.AccountId, trade.Symbol);
        }
        else
        {
            await _portfolioService.UpdatePortfolioAsync(userId, trade.AccountId, trade.Symbol);
        }

        return trade;
    }

    public async Task DeleteTradeAsync(string tradeId, string userId)
    {
        var trade = await GetTradeByIdAsync(tradeId, userId);
        if (trade == null)
        {
            throw new KeyNotFoundException("Trade not found");
        }

        var accountId = trade.AccountId;
        var symbol = trade.Symbol;

        _context.Trades.Remove(trade);
        await _context.SaveChangesAsync();

        // Update portfolio
        await _portfolioService.UpdatePortfolioAsync(userId, accountId, symbol);
    }

    public async Task<IEnumerable<SymbolTradeSummary>> GetTradeSummaryBySymbolAsync(string userId, string? accountId = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _context.Trades
            .Include(t => t.Account)
            .Where(t => t.UserId == userId);

        if (!string.IsNullOrEmpty(accountId))
        {
            query = query.Where(t => t.AccountId == accountId);
        }

        if (startDate.HasValue)
        {
            var startUtc = DateTime.SpecifyKind(startDate.Value, DateTimeKind.Utc);
            query = query.Where(t => t.Date >= startUtc);
        }

        if (endDate.HasValue)
        {
            var endUtc = DateTime.SpecifyKind(endDate.Value.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
            query = query.Where(t => t.Date <= endUtc);
        }

        // Order by date, then ensure BUYs come before SELLs on the same day
        var trades = await query
            .OrderBy(t => t.Date)
            .ThenBy(t => t.Type == "SELL" ? 1 : 0)
            .ToListAsync();

        // Group by symbol and account
        var grouped = trades
            .GroupBy(t => new { t.Symbol, t.AccountId, AccountName = t.Account?.Name ?? "Unknown" })
            .Select(g => CalculateSymbolSummary(g.Key.Symbol, g.Key.AccountId, g.Key.AccountName, g.ToList()))
            .OrderBy(s => s.Symbol)
            .ToList();

        return grouped;
    }

    private SymbolTradeSummary CalculateSymbolSummary(string symbol, string accountId, string accountName, List<Trade> trades)
    {
        var buys = trades.Where(t => t.Type == "BUY").ToList();
        var sells = trades.Where(t => t.Type == "SELL").ToList();

        var totalBuyQuantity = buys.Sum(t => t.Quantity);
        var totalBuyAmount = buys.Sum(t => t.Quantity * t.Price + t.Fee);
        var totalSellQuantity = sells.Sum(t => t.Quantity);
        var totalSellAmount = sells.Sum(t => t.Quantity * t.Price - t.Fee);

        // Calculate realized P&L using FIFO
        var buyQueue = new Queue<(double Quantity, double Price, double Fee)>();
        double realizedPnL = 0;
        double totalCostForSold = 0;

        // Order by date, then ensure BUYs come before SELLs on the same day
        foreach (var trade in trades.OrderBy(t => t.Date).ThenBy(t => t.Type == "SELL" ? 1 : 0))
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

                while (sellQuantity > 0 && buyQueue.Count > 0)
                {
                    var buy = buyQueue.Dequeue();
                    if (buy.Quantity <= sellQuantity)
                    {
                        var buyCost = buy.Quantity * buy.Price + buy.Fee;
                        costBasis += buyCost;
                        totalCostForSold += buyCost;
                        sellQuantity -= buy.Quantity;
                    }
                    else
                    {
                        var partialCost = sellQuantity * buy.Price + (buy.Fee * sellQuantity / buy.Quantity);
                        costBasis += partialCost;
                        totalCostForSold += partialCost;
                        var remaining = buy.Quantity - sellQuantity;
                        var remainingFee = buy.Fee * (remaining / buy.Quantity);
                        buyQueue = new Queue<(double, double, double)>(
                            new[] { (remaining, buy.Price, remainingFee) }.Concat(buyQueue));
                        sellQuantity = 0;
                    }
                }

                realizedPnL += sellProceeds - costBasis;
            }
        }

        // Current position
        var currentQuantity = buyQueue.Sum(b => b.Quantity);
        var currentCostBasis = buyQueue.Sum(b => b.Quantity * b.Price + b.Fee);

        return new SymbolTradeSummary
        {
            Symbol = symbol,
            AccountId = accountId,
            AccountName = accountName,
            BuyCount = buys.Count,
            TotalBuyQuantity = totalBuyQuantity,
            TotalBuyAmount = totalBuyAmount,
            AverageBuyPrice = totalBuyQuantity > 0 ? totalBuyAmount / totalBuyQuantity : 0,
            SellCount = sells.Count,
            TotalSellQuantity = totalSellQuantity,
            TotalSellAmount = totalSellAmount,
            AverageSellPrice = totalSellQuantity > 0 ? totalSellAmount / totalSellQuantity : 0,
            CurrentQuantity = currentQuantity,
            CurrentCostBasis = currentCostBasis,
            RealizedPnL = realizedPnL,
            RealizedPnLPercent = totalCostForSold > 0 ? (realizedPnL / totalCostForSold) * 100 : 0,
            Trades = trades.OrderByDescending(t => t.Date).Select(t => new TradeDetail
            {
                Id = t.Id,
                Type = t.Type,
                Quantity = t.Quantity,
                Price = t.Price,
                Fee = t.Fee,
                Total = t.Type == "BUY" ? -(t.Quantity * t.Price + t.Fee) : (t.Quantity * t.Price - t.Fee),
                Date = t.Date,
                Notes = t.Notes
            }).ToList()
        };
    }

    public async Task<TimeAnalysisSummary> GetTimeAnalysisAsync(string userId, string? accountId = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _context.Trades
            .Include(t => t.Account)
            .Where(t => t.UserId == userId);

        if (!string.IsNullOrEmpty(accountId))
        {
            query = query.Where(t => t.AccountId == accountId);
        }

        if (startDate.HasValue)
        {
            var startUtc = DateTime.SpecifyKind(startDate.Value, DateTimeKind.Utc);
            query = query.Where(t => t.Date >= startUtc);
        }

        if (endDate.HasValue)
        {
            var endUtc = DateTime.SpecifyKind(endDate.Value.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
            query = query.Where(t => t.Date <= endUtc);
        }

        // Order by date, then ensure BUYs come before SELLs on the same day
        var trades = await query
            .OrderBy(t => t.Date)
            .ThenBy(t => t.Type == "SELL" ? 1 : 0)
            .ToListAsync();

        if (!trades.Any())
        {
            return new TimeAnalysisSummary();
        }

        // Calculate P/L for each sell using FIFO per symbol
        var tradePnLs = CalculateTradePnLs(trades);

        // Group by month
        var monthly = trades
            .GroupBy(t => new { t.Date.Year, t.Date.Month })
            .Select(g => CreateTimePeriodSummary(
                $"{g.Key.Year}-{g.Key.Month:D2}",
                new DateTime(g.Key.Year, g.Key.Month, 1),
                new DateTime(g.Key.Year, g.Key.Month, DateTime.DaysInMonth(g.Key.Year, g.Key.Month)),
                g.ToList(),
                tradePnLs))
            .OrderByDescending(p => p.Period)
            .ToList();

        // Group by week (ISO week)
        var weekly = trades
            .GroupBy(t => {
                var cal = System.Globalization.CultureInfo.InvariantCulture.Calendar;
                var week = cal.GetWeekOfYear(t.Date, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
                return new { t.Date.Year, Week = week };
            })
            .Select(g => {
                var startOfWeek = FirstDateOfWeek(g.Key.Year, g.Key.Week);
                return CreateTimePeriodSummary(
                    $"{g.Key.Year}-W{g.Key.Week:D2}",
                    startOfWeek,
                    startOfWeek.AddDays(6),
                    g.ToList(),
                    tradePnLs);
            })
            .OrderByDescending(p => p.Period)
            .ToList();

        var totalRealizedPnL = tradePnLs.Values.Sum();
        var sellTrades = tradePnLs.Count;
        var winningTrades = tradePnLs.Values.Count(p => p > 0);

        return new TimeAnalysisSummary
        {
            Monthly = monthly,
            Weekly = weekly,
            TotalRealizedPnL = totalRealizedPnL,
            TotalTrades = trades.Count,
            OverallWinRate = sellTrades > 0 ? (double)winningTrades / sellTrades * 100 : 0
        };
    }

    private Dictionary<string, double> CalculateTradePnLs(List<Trade> trades)
    {
        var result = new Dictionary<string, double>();
        var symbolQueues = new Dictionary<string, Queue<(double Quantity, double Price, double Fee)>>();

        // Order by date, then ensure BUYs come before SELLs on the same day
        foreach (var trade in trades.OrderBy(t => t.Date).ThenBy(t => t.Type == "SELL" ? 1 : 0))
        {
            if (!symbolQueues.ContainsKey(trade.Symbol))
            {
                symbolQueues[trade.Symbol] = new Queue<(double, double, double)>();
            }

            var queue = symbolQueues[trade.Symbol];

            if (trade.Type == "BUY")
            {
                queue.Enqueue((trade.Quantity, trade.Price, trade.Fee));
            }
            else if (trade.Type == "SELL")
            {
                double sellQuantity = trade.Quantity;
                double sellProceeds = trade.Quantity * trade.Price - trade.Fee;
                double costBasis = 0;

                while (sellQuantity > 0 && queue.Count > 0)
                {
                    var buy = queue.Dequeue();
                    if (buy.Quantity <= sellQuantity)
                    {
                        costBasis += buy.Quantity * buy.Price + buy.Fee;
                        sellQuantity -= buy.Quantity;
                    }
                    else
                    {
                        var partialCost = sellQuantity * buy.Price + (buy.Fee * sellQuantity / buy.Quantity);
                        costBasis += partialCost;
                        var remaining = buy.Quantity - sellQuantity;
                        var remainingFee = buy.Fee * (remaining / buy.Quantity);
                        symbolQueues[trade.Symbol] = new Queue<(double, double, double)>(
                            new[] { (remaining, buy.Price, remainingFee) }.Concat(queue));
                        sellQuantity = 0;
                    }
                }

                result[trade.Id] = sellProceeds - costBasis;
            }
        }

        return result;
    }

    private TimePeriodSummary CreateTimePeriodSummary(string period, DateTime start, DateTime end, List<Trade> trades, Dictionary<string, double> tradePnLs)
    {
        var buys = trades.Where(t => t.Type == "BUY").ToList();
        var sells = trades.Where(t => t.Type == "SELL").ToList();

        var periodPnLs = sells.Where(s => tradePnLs.ContainsKey(s.Id)).Select(s => tradePnLs[s.Id]).ToList();
        var realizedPnL = periodPnLs.Sum();
        var winningTrades = periodPnLs.Count(p => p > 0);
        var losingTrades = periodPnLs.Count(p => p < 0);
        var totalCost = buys.Sum(b => b.Quantity * b.Price + b.Fee);

        var symbolBreakdown = sells
            .Where(s => tradePnLs.ContainsKey(s.Id))
            .GroupBy(s => s.Symbol)
            .Select(g => new SymbolPnL
            {
                Symbol = g.Key,
                TradeCount = g.Count(),
                RealizedPnL = g.Sum(s => tradePnLs[s.Id])
            })
            .OrderByDescending(s => Math.Abs(s.RealizedPnL))
            .ToList();

        return new TimePeriodSummary
        {
            Period = period,
            StartDate = start,
            EndDate = end,
            TotalTrades = trades.Count,
            BuyCount = buys.Count,
            SellCount = sells.Count,
            TotalBuyAmount = buys.Sum(b => b.Quantity * b.Price + b.Fee),
            TotalSellAmount = sells.Sum(s => s.Quantity * s.Price - s.Fee),
            RealizedPnL = realizedPnL,
            RealizedPnLPercent = totalCost > 0 ? (realizedPnL / totalCost) * 100 : 0,
            WinningTrades = winningTrades,
            LosingTrades = losingTrades,
            WinRate = periodPnLs.Count > 0 ? (double)winningTrades / periodPnLs.Count * 100 : 0,
            SymbolBreakdown = symbolBreakdown
        };
    }

    private static DateTime FirstDateOfWeek(int year, int weekOfYear)
    {
        var jan1 = new DateTime(year, 1, 1);
        var daysOffset = DayOfWeek.Monday - jan1.DayOfWeek;
        var firstMonday = jan1.AddDays(daysOffset);
        var firstWeek = System.Globalization.CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(
            jan1, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        if (firstWeek <= 1)
        {
            weekOfYear -= 1;
        }
        return firstMonday.AddDays(weekOfYear * 7);
    }

    public async Task<DuplicateCleanupResult> FindAndRemoveDuplicatesAsync(string userId, string? accountId = null)
    {
        var result = new DuplicateCleanupResult();

        // Get all trades for the user
        var query = _context.Trades.Where(t => t.UserId == userId);
        if (!string.IsNullOrEmpty(accountId))
        {
            query = query.Where(t => t.AccountId == accountId);
        }

        var trades = await query
            .OrderBy(t => t.CreatedAt)
            .ToListAsync();

        // Group trades by key attributes (symbol, date, type, quantity, price)
        var groups = trades
            .GroupBy(t => new 
            { 
                t.Symbol, 
                Date = t.Date.Date, 
                t.Type, 
                Quantity = Math.Round(t.Quantity, 3), 
                Price = Math.Round(t.Price, 3) 
            })
            .Where(g => g.Count() > 1)
            .ToList();

        var affectedSymbols = new HashSet<(string Symbol, string AccountId)>();

        foreach (var group in groups)
        {
            var duplicates = group.OrderBy(t => t.CreatedAt).ToList();
            
            // Keep the first one (oldest), remove the rest
            var toRemove = duplicates.Skip(1).ToList();

            var duplicateGroup = new DuplicateGroup
            {
                Symbol = group.Key.Symbol,
                Type = group.Key.Type,
                Date = group.Key.Date,
                Quantity = (double)group.Key.Quantity,
                Price = (double)group.Key.Price,
                Count = duplicates.Count,
                Removed = toRemove.Count
            };

            result.Groups.Add(duplicateGroup);
            result.DuplicatesFound += duplicates.Count - 1;

            foreach (var trade in toRemove)
            {
                _context.Trades.Remove(trade);
                result.DuplicatesRemoved++;
                affectedSymbols.Add((trade.Symbol, trade.AccountId));
            }
        }

        if (result.DuplicatesRemoved > 0)
        {
            await _context.SaveChangesAsync();

            // Recalculate portfolio for affected symbols
            foreach (var (symbol, affectedAccountId) in affectedSymbols)
            {
                await _portfolioService.UpdatePortfolioAsync(userId, affectedAccountId, symbol);
            }
        }

        return result;
    }

    public async Task<DeleteAllTradesResult> DeleteAllTradesForAccountAsync(string userId, string accountId)
    {
        var result = new DeleteAllTradesResult();

        // Verify account belongs to user
        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == userId);

        if (account == null)
        {
            result.Success = false;
            result.Error = "Account not found or access denied";
            return result;
        }

        result.AccountName = account.Name;

        // Get all trades for this account
        var trades = await _context.Trades
            .Where(t => t.AccountId == accountId && t.UserId == userId)
            .ToListAsync();

        if (!trades.Any())
        {
            result.Success = true;
            result.DeletedCount = 0;
            return result;
        }

        // Get unique symbols for portfolio cleanup
        var symbols = trades.Select(t => t.Symbol).Distinct().ToList();

        // Delete all trades
        _context.Trades.RemoveRange(trades);
        result.DeletedCount = trades.Count;

        // Delete portfolio entries for this account
        var portfolioEntries = await _context.Portfolios
            .Where(p => p.AccountId == accountId && p.UserId == userId)
            .ToListAsync();

        _context.Portfolios.RemoveRange(portfolioEntries);

        await _context.SaveChangesAsync();

        result.Success = true;
        return result;
    }
}
