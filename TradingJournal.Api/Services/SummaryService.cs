using Microsoft.EntityFrameworkCore;
using TradingJournal.Api.Data;
using System.Globalization;

namespace TradingJournal.Api.Services;

public class SummaryService : ISummaryService
{
    private readonly ApplicationDbContext _context;

    public SummaryService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<TradingSummary> GetTradingSummaryAsync(string userId, string? accountId = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        // Convert dates to UTC for PostgreSQL compatibility
        var startDateUtc = startDate.HasValue ? DateTime.SpecifyKind(startDate.Value, DateTimeKind.Utc) : (DateTime?)null;
        var endDateUtc = endDate.HasValue ? DateTime.SpecifyKind(endDate.Value.AddDays(1).AddTicks(-1), DateTimeKind.Utc) : (DateTime?)null;

        var summary = new TradingSummary
        {
            FilterStartDate = startDate,
            FilterEndDate = endDate,
            FilterAccountId = accountId
        };

        // Get account name if filtered
        if (!string.IsNullOrEmpty(accountId))
        {
            var account = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == userId);
            summary.FilterAccountName = account?.Name;
        }

        // Query trades
        var tradesQuery = _context.Trades.Where(t => t.UserId == userId);
        if (!string.IsNullOrEmpty(accountId))
            tradesQuery = tradesQuery.Where(t => t.AccountId == accountId);
        if (startDateUtc.HasValue)
            tradesQuery = tradesQuery.Where(t => t.Date >= startDateUtc.Value);
        if (endDateUtc.HasValue)
            tradesQuery = tradesQuery.Where(t => t.Date <= endDateUtc.Value);

        var trades = await tradesQuery.OrderBy(t => t.Date).ThenBy(t => t.Type == "SELL" ? 1 : 0).ToListAsync();

        // Query dividends
        var dividendsQuery = _context.Dividends.Where(d => d.UserId == userId);
        if (!string.IsNullOrEmpty(accountId))
            dividendsQuery = dividendsQuery.Where(d => d.AccountId == accountId);
        if (startDateUtc.HasValue)
            dividendsQuery = dividendsQuery.Where(d => d.PaymentDate >= startDateUtc.Value);
        if (endDateUtc.HasValue)
            dividendsQuery = dividendsQuery.Where(d => d.PaymentDate <= endDateUtc.Value);

        var dividends = await dividendsQuery.ToListAsync();

        // Calculate realized P&L per trade using FIFO
        var tradePnLs = CalculateTradePnLs(trades);
        var winningPnLs = tradePnLs.Where(p => p.PnL > 0).ToList();
        var losingPnLs = tradePnLs.Where(p => p.PnL < 0).ToList();
        var breakEvenPnLs = tradePnLs.Where(p => p.PnL == 0).ToList();

        // PnL Statistics
        summary.PnL = CalculatePnLStatistics(tradePnLs, winningPnLs, losingPnLs, trades);

        // Performance Scores
        summary.Scores = CalculatePerformanceScores(tradePnLs, winningPnLs, losingPnLs);

        // Trade Statistics
        summary.TradeStats = CalculateTradeStatistics(trades, tradePnLs, winningPnLs, losingPnLs, breakEvenPnLs, startDate, endDate);

        // Symbol Statistics
        summary.SymbolStats = CalculateSymbolStatistics(tradePnLs);

        // Time Statistics
        summary.TimeStats = CalculateTimeStatistics(tradePnLs);

        // Commission Statistics
        summary.CommissionStats = CalculateCommissionStatistics(trades, winningPnLs);

        // Dividend Statistics
        summary.DividendStats = CalculateDividendStatistics(dividends);

        return summary;
    }

    private class TradePnL
    {
        public string Symbol { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public double PnL { get; set; }
        public double Quantity { get; set; }
        public double Fee { get; set; }
    }

    private List<TradePnL> CalculateTradePnLs(List<Models.Trade> trades)
    {
        var result = new List<TradePnL>();
        var symbolTrades = trades.GroupBy(t => t.Symbol);

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
                    result.Add(new TradePnL
                    {
                        Symbol = trade.Symbol,
                        Date = trade.Date,
                        PnL = pnl,
                        Quantity = trade.Quantity,
                        Fee = trade.Fee
                    });
                }
            }
        }

        return result.OrderBy(t => t.Date).ToList();
    }

    private PnLStatistics CalculatePnLStatistics(List<TradePnL> tradePnLs, List<TradePnL> winningPnLs, List<TradePnL> losingPnLs, List<Models.Trade> allTrades)
    {
        var stats = new PnLStatistics
        {
            TotalPnL = tradePnLs.Sum(t => t.PnL),
            TotalProfit = winningPnLs.Sum(t => t.PnL),
            TotalLoss = Math.Abs(losingPnLs.Sum(t => t.PnL)),
            MaxProfit = winningPnLs.Any() ? winningPnLs.Max(t => t.PnL) : 0,
            MaxLoss = losingPnLs.Any() ? Math.Abs(losingPnLs.Min(t => t.PnL)) : 0,
            AvgProfit = winningPnLs.Any() ? winningPnLs.Average(t => t.PnL) : 0,
            AvgLoss = losingPnLs.Any() ? Math.Abs(losingPnLs.Average(t => t.PnL)) : 0,
            AvgPnL = tradePnLs.Any() ? tradePnLs.Average(t => t.PnL) : 0
        };

        // Yearly breakdown
        stats.YearlyPnL = tradePnLs
            .GroupBy(t => t.Date.Year)
            .Select(g => new PeriodPnL
            {
                Period = g.Key.ToString(),
                PnL = g.Sum(t => t.PnL),
                Trades = g.Count(),
                WinRate = g.Count() > 0 ? (double)g.Count(t => t.PnL > 0) / g.Count() * 100 : 0
            })
            .OrderBy(p => p.Period)
            .ToList();

        // Monthly breakdown
        stats.MonthlyPnL = tradePnLs
            .GroupBy(t => $"{t.Date.Year}-{t.Date.Month:D2}")
            .Select(g => new PeriodPnL
            {
                Period = g.Key,
                PnL = g.Sum(t => t.PnL),
                Trades = g.Count(),
                WinRate = g.Count() > 0 ? (double)g.Count(t => t.PnL > 0) / g.Count() * 100 : 0
            })
            .OrderBy(p => p.Period)
            .ToList();

        // Weekly breakdown (last 12 weeks)
        var calendar = CultureInfo.InvariantCulture.Calendar;
        stats.WeeklyPnL = tradePnLs
            .GroupBy(t => $"{t.Date.Year}-W{calendar.GetWeekOfYear(t.Date, CalendarWeekRule.FirstDay, DayOfWeek.Monday):D2}")
            .Select(g => new PeriodPnL
            {
                Period = g.Key,
                PnL = g.Sum(t => t.PnL),
                Trades = g.Count(),
                WinRate = g.Count() > 0 ? (double)g.Count(t => t.PnL > 0) / g.Count() * 100 : 0
            })
            .OrderByDescending(p => p.Period)
            .Take(12)
            .OrderBy(p => p.Period)
            .ToList();

        return stats;
    }

    private PerformanceScores CalculatePerformanceScores(List<TradePnL> tradePnLs, List<TradePnL> winningPnLs, List<TradePnL> losingPnLs)
    {
        var scores = new PerformanceScores();

        if (!tradePnLs.Any()) return scores;

        double totalProfit = winningPnLs.Sum(t => t.PnL);
        double totalLoss = Math.Abs(losingPnLs.Sum(t => t.PnL));
        int totalTrades = tradePnLs.Count;
        int winCount = winningPnLs.Count;
        int lossCount = losingPnLs.Count;

        scores.WinRate = totalTrades > 0 ? (double)winCount / totalTrades * 100 : 0;
        scores.LossRate = totalTrades > 0 ? (double)lossCount / totalTrades * 100 : 0;

        // Profit Factor = Gross Profit / Gross Loss
        scores.ProfitFactor = totalLoss > 0 ? totalProfit / totalLoss : totalProfit > 0 ? double.PositiveInfinity : 0;

        // Gain-to-Pain Ratio = Sum of all returns / Abs(Sum of negative returns)
        double sumAll = tradePnLs.Sum(t => t.PnL);
        scores.GainToPainRatio = totalLoss > 0 ? sumAll / totalLoss : sumAll > 0 ? double.PositiveInfinity : 0;

        // Win/Loss Ratio = Average Win / Average Loss
        double avgWin = winningPnLs.Any() ? winningPnLs.Average(t => t.PnL) : 0;
        double avgLoss = losingPnLs.Any() ? Math.Abs(losingPnLs.Average(t => t.PnL)) : 0;
        scores.WinLossRatio = avgLoss > 0 ? avgWin / avgLoss : avgWin > 0 ? double.PositiveInfinity : 0;

        // Adjusted Win/Loss Ratio = (Avg Win * Win%) / (Avg Loss * Loss%)
        double winPct = (double)winCount / totalTrades;
        double lossPct = (double)lossCount / totalTrades;
        scores.AdjustedWinLossRatio = (avgLoss * lossPct) > 0 ? (avgWin * winPct) / (avgLoss * lossPct) : 0;

        // Trading Expectancy = (Win% * Avg Win) - (Loss% * Avg Loss)
        scores.TradingExpectancy = (winPct * avgWin) - (lossPct * avgLoss);

        // Kelly Criterion = W - [(1-W) / R] where W = win rate, R = win/loss ratio
        if (scores.WinLossRatio > 0 && scores.WinLossRatio != double.PositiveInfinity)
        {
            scores.KellyCriterion = winPct - ((1 - winPct) / scores.WinLossRatio);
        }

        // Standard Deviations
        if (tradePnLs.Count > 1)
        {
            double mean = tradePnLs.Average(t => t.PnL);
            scores.StandardDeviationPnL = Math.Sqrt(tradePnLs.Sum(t => Math.Pow(t.PnL - mean, 2)) / (tradePnLs.Count - 1));
        }
        if (winningPnLs.Count > 1)
        {
            double meanWin = winningPnLs.Average(t => t.PnL);
            scores.StandardDeviationProfit = Math.Sqrt(winningPnLs.Sum(t => Math.Pow(t.PnL - meanWin, 2)) / (winningPnLs.Count - 1));
        }
        if (losingPnLs.Count > 1)
        {
            double meanLoss = losingPnLs.Average(t => t.PnL);
            scores.StandardDeviationLoss = Math.Sqrt(losingPnLs.Sum(t => Math.Pow(t.PnL - meanLoss, 2)) / (losingPnLs.Count - 1));
        }

        // System Quality Number (SQN) = (Average PnL / StdDev) * Sqrt(N)
        if (scores.StandardDeviationPnL > 0)
        {
            double avgPnL = tradePnLs.Average(t => t.PnL);
            scores.SystemQualityNumber = (avgPnL / scores.StandardDeviationPnL) * Math.Sqrt(tradePnLs.Count);
        }

        // Sharpe Ratio (simplified, assuming risk-free rate = 0)
        if (scores.StandardDeviationPnL > 0)
        {
            scores.SharpeRatio = tradePnLs.Average(t => t.PnL) / scores.StandardDeviationPnL;
        }

        // Max Drawdown
        double peak = 0;
        double maxDrawdown = 0;
        double cumulative = 0;
        foreach (var trade in tradePnLs)
        {
            cumulative += trade.PnL;
            if (cumulative > peak) peak = cumulative;
            double drawdown = peak - cumulative;
            if (drawdown > maxDrawdown) maxDrawdown = drawdown;
        }
        scores.MaxDrawdown = maxDrawdown;
        scores.MaxDrawdownPercent = peak > 0 ? (maxDrawdown / peak) * 100 : 0;

        // Max Consecutive Wins/Losses
        int currentWinStreak = 0, currentLossStreak = 0;
        int maxWinStreak = 0, maxLossStreak = 0;
        foreach (var trade in tradePnLs)
        {
            if (trade.PnL > 0)
            {
                currentWinStreak++;
                currentLossStreak = 0;
                if (currentWinStreak > maxWinStreak) maxWinStreak = currentWinStreak;
            }
            else if (trade.PnL < 0)
            {
                currentLossStreak++;
                currentWinStreak = 0;
                if (currentLossStreak > maxLossStreak) maxLossStreak = currentLossStreak;
            }
        }
        scores.MaxConsecutiveWins = maxWinStreak;
        scores.MaxConsecutiveLosses = maxLossStreak;

        return scores;
    }

    private TradeStatistics CalculateTradeStatistics(List<Models.Trade> trades, List<TradePnL> tradePnLs, 
        List<TradePnL> winningPnLs, List<TradePnL> losingPnLs, List<TradePnL> breakEvenPnLs,
        DateTime? startDate, DateTime? endDate)
    {
        var tradingDays = trades.Select(t => t.Date.Date).Distinct().Count();
        var minDate = trades.Any() ? trades.Min(t => t.Date) : DateTime.Now;
        var maxDate = trades.Any() ? trades.Max(t => t.Date) : DateTime.Now;
        var totalDays = (maxDate - minDate).Days + 1;
        var totalWeeks = Math.Max(1, totalDays / 7.0);
        var totalMonths = Math.Max(1, totalDays / 30.0);

        return new TradeStatistics
        {
            TotalTrades = trades.Count,
            WinningTrades = winningPnLs.Count,
            LosingTrades = losingPnLs.Count,
            BreakEvenTrades = breakEvenPnLs.Count,
            BuyTrades = trades.Count(t => t.Type == "BUY"),
            SellTrades = trades.Count(t => t.Type == "SELL"),
            TotalVolume = trades.Sum(t => t.Quantity * t.Price),
            TradingDays = tradingDays,
            TotalDaysInPeriod = totalDays,
            AvgTradesPerDay = tradingDays > 0 ? (double)trades.Count / tradingDays : 0,
            AvgTradesPerWeek = trades.Count / totalWeeks,
            AvgTradesPerMonth = trades.Count / totalMonths
        };
    }

    private SymbolStatistics CalculateSymbolStatistics(List<TradePnL> tradePnLs)
    {
        var symbolGroups = tradePnLs
            .GroupBy(t => t.Symbol)
            .Select(g => new SymbolStat
            {
                Symbol = g.Key,
                PnL = g.Sum(t => t.PnL),
                TradeCount = g.Count(),
                Volume = g.Sum(t => t.Quantity),
                WinRate = g.Count() > 0 ? (double)g.Count(t => t.PnL > 0) / g.Count() * 100 : 0
            })
            .ToList();

        return new SymbolStatistics
        {
            UniqueSymbols = symbolGroups.Count,
            BestSymbol = symbolGroups.OrderByDescending(s => s.PnL).FirstOrDefault(),
            WorstSymbol = symbolGroups.OrderBy(s => s.PnL).FirstOrDefault(),
            MostTraded = symbolGroups.OrderByDescending(s => s.TradeCount).FirstOrDefault(),
            TopProfitable = symbolGroups.Where(s => s.PnL > 0).OrderByDescending(s => s.PnL).Take(5).ToList(),
            TopLosing = symbolGroups.Where(s => s.PnL < 0).OrderBy(s => s.PnL).Take(5).ToList(),
            MostActive = symbolGroups.OrderByDescending(s => s.TradeCount).Take(5).ToList()
        };
    }

    private TimeStatistics CalculateTimeStatistics(List<TradePnL> tradePnLs)
    {
        var stats = new TimeStatistics();

        if (!tradePnLs.Any()) return stats;

        // Daily P&L
        var dailyPnL = tradePnLs
            .GroupBy(t => t.Date.Date)
            .Select(g => new { Date = g.Key, PnL = g.Sum(t => t.PnL) })
            .ToList();

        var bestDay = dailyPnL.OrderByDescending(d => d.PnL).FirstOrDefault();
        var worstDay = dailyPnL.OrderBy(d => d.PnL).FirstOrDefault();
        
        if (bestDay != null)
        {
            stats.BestTradingDay = bestDay.Date.ToString("yyyy-MM-dd");
            stats.BestTradingDayPnL = bestDay.PnL;
        }
        if (worstDay != null)
        {
            stats.WorstTradingDay = worstDay.Date.ToString("yyyy-MM-dd");
            stats.WorstTradingDayPnL = worstDay.PnL;
        }

        // Monthly P&L
        var monthlyPnL = tradePnLs
            .GroupBy(t => new { t.Date.Year, t.Date.Month })
            .Select(g => new { Period = $"{g.Key.Year}-{g.Key.Month:D2}", PnL = g.Sum(t => t.PnL) })
            .ToList();

        var bestMonth = monthlyPnL.OrderByDescending(m => m.PnL).FirstOrDefault();
        var worstMonth = monthlyPnL.OrderBy(m => m.PnL).FirstOrDefault();

        if (bestMonth != null)
        {
            stats.BestMonth = bestMonth.Period;
            stats.BestMonthPnL = bestMonth.PnL;
        }
        if (worstMonth != null)
        {
            stats.WorstMonth = worstMonth.Period;
            stats.WorstMonthPnL = worstMonth.PnL;
        }

        // Day of Week analysis
        stats.DayOfWeekStats = tradePnLs
            .GroupBy(t => t.Date.DayOfWeek)
            .Select(g => new DayOfWeekStat
            {
                DayName = g.Key.ToString(),
                TotalPnL = g.Sum(t => t.PnL),
                TradeCount = g.Count(),
                WinRate = g.Count() > 0 ? (double)g.Count(t => t.PnL > 0) / g.Count() * 100 : 0,
                AvgPnL = g.Any() ? g.Average(t => t.PnL) : 0
            })
            .OrderBy(d => (int)Enum.Parse<DayOfWeek>(d.DayName))
            .ToList();

        return stats;
    }

    private CommissionStatistics CalculateCommissionStatistics(List<Models.Trade> trades, List<TradePnL> winningPnLs)
    {
        var totalCommissions = trades.Sum(t => t.Fee);
        var tradingDays = trades.Select(t => t.Date.Date).Distinct().Count();
        var totalProfit = winningPnLs.Sum(t => t.PnL);

        return new CommissionStatistics
        {
            TotalCommissions = totalCommissions,
            AvgCommissionPerTrade = trades.Any() ? totalCommissions / trades.Count : 0,
            AvgCommissionPerDay = tradingDays > 0 ? totalCommissions / tradingDays : 0,
            CommissionAsPercentOfProfit = totalProfit > 0 ? (totalCommissions / totalProfit) * 100 : 0
        };
    }

    private DividendStatistics CalculateDividendStatistics(List<Models.Dividend> dividends)
    {
        var symbolGroups = dividends.GroupBy(d => d.Symbol).ToList();
        var topSymbol = symbolGroups.OrderByDescending(g => g.Sum(d => d.Amount)).FirstOrDefault();

        return new DividendStatistics
        {
            TotalDividends = dividends.Sum(d => d.Amount),
            DividendPayments = dividends.Count,
            AvgDividendPerPayment = dividends.Any() ? dividends.Average(d => d.Amount) : 0,
            DividendSymbols = symbolGroups.Count,
            TopDividendSymbol = topSymbol?.Key,
            TopDividendAmount = topSymbol?.Sum(d => d.Amount) ?? 0
        };
    }
}
