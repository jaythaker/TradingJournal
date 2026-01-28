using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.RegularExpressions;
using TradingJournal.Api.Data;
using TradingJournal.Api.Models;
using TradingJournal.Api.Services;

namespace TradingJournal.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PortfolioController : ControllerBase
{
    private readonly IPortfolioService _portfolioService;
    private readonly IStockQuoteService _stockQuoteService;
    private readonly ApplicationDbContext _context;

    public PortfolioController(IPortfolioService portfolioService, IStockQuoteService stockQuoteService, ApplicationDbContext context)
    {
        _portfolioService = portfolioService;
        _stockQuoteService = stockQuoteService;
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Portfolio>>> GetPortfolio([FromQuery] string? accountId = null)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var portfolio = await _portfolioService.GetPortfolioByUserIdAsync(userId, accountId);
        return Ok(portfolio);
    }

    [HttpGet("with-quotes")]
    public async Task<ActionResult<IEnumerable<PortfolioWithQuote>>> GetPortfolioWithQuotes([FromQuery] string? accountId = null)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var portfolio = await _portfolioService.GetPortfolioByUserIdAsync(userId, accountId);
        var portfolioList = portfolio.ToList();

        if (!portfolioList.Any())
        {
            return Ok(new List<PortfolioWithQuote>());
        }

        // Fetch live quotes for all symbols
        var symbols = portfolioList.Select(p => p.Symbol).Distinct();
        var quotes = await _stockQuoteService.GetQuotesAsync(symbols);

        var result = portfolioList.Select(p =>
        {
            var quote = quotes.TryGetValue(p.Symbol, out var q) ? q : null;
            var currentPrice = quote?.Price ?? p.AveragePrice;
            var marketValue = p.Quantity * currentPrice;
            var costBasis = p.Quantity * p.AveragePrice;
            var unrealizedPnL = marketValue - costBasis;
            var unrealizedPnLPercent = costBasis > 0 ? (unrealizedPnL / costBasis) * 100 : 0;

            return new PortfolioWithQuote
            {
                Id = p.Id,
                Symbol = p.Symbol,
                Quantity = p.Quantity,
                AveragePrice = p.AveragePrice,
                CostBasis = costBasis,
                CurrentPrice = currentPrice,
                MarketValue = marketValue,
                UnrealizedPnL = unrealizedPnL,
                UnrealizedPnLPercent = unrealizedPnLPercent,
                DayChange = quote?.Change ?? 0,
                DayChangePercent = quote?.ChangePercent ?? 0,
                DayHigh = quote?.DayHigh ?? 0,
                DayLow = quote?.DayLow ?? 0,
                Volume = quote?.Volume ?? 0,
                MarketState = quote?.MarketState ?? "UNKNOWN",
                LastUpdated = quote?.LastUpdated ?? DateTime.UtcNow,
                AccountId = p.AccountId,
                AccountName = p.Account?.Name
            };
        }).OrderByDescending(p => p.MarketValue).ToList();

        return Ok(result);
    }

    [HttpGet("performance")]
    public async Task<ActionResult<PortfolioPerformance>> GetPerformance([FromQuery] string? accountId = null)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var performance = await _portfolioService.GetPerformanceAsync(userId, accountId);
        return Ok(performance);
    }

    [HttpPost("recalculate")]
    public async Task<ActionResult> Recalculate()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        await _portfolioService.RecalculateAllAsync(userId);
        return Ok(new { message = "Portfolio recalculated successfully" });
    }

    /// <summary>
    /// Get open options positions grouped by spread strategy
    /// </summary>
    [HttpGet("options")]
    public async Task<ActionResult<IEnumerable<OptionSpreadGroup>>> GetOptionsPositions([FromQuery] string? accountId = null)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        // Get all options trades
        var query = _context.Trades
            .Include(t => t.Account)
            .Where(t => t.UserId == userId && t.InstrumentType == InstrumentType.Option);

        if (!string.IsNullOrEmpty(accountId))
        {
            query = query.Where(t => t.AccountId == accountId);
        }

        var optionTrades = await query.OrderByDescending(t => t.Date).ToListAsync();

        // Group by SpreadGroupId to identify spreads
        var spreadGroups = optionTrades
            .Where(t => !string.IsNullOrEmpty(t.SpreadGroupId))
            .GroupBy(t => t.SpreadGroupId!)
            .Select(g =>
            {
                var legs = g.OrderBy(t => t.SpreadLegNumber ?? 0).ToList();
                var firstLeg = legs.First();
                
                // Extract strategy name from notes if available
                var strategyName = ExtractStrategyName(firstLeg.Notes, firstLeg.SpreadType.ToString());
                
                // Calculate net premium (positive = credit, negative = debit)
                var netPremium = legs.Sum(t =>
                {
                    var premium = t.Price * t.Quantity * t.ContractMultiplier;
                    // SELL trades are credits (positive), BUY trades are debits (negative)
                    return t.Type.Contains("SELL") ? premium : -premium;
                });

                return new OptionSpreadGroup
                {
                    SpreadGroupId = g.Key,
                    SpreadType = firstLeg.SpreadType.ToString(),
                    StrategyName = strategyName,
                    UnderlyingSymbol = firstLeg.UnderlyingSymbol ?? firstLeg.Symbol,
                    ExpirationDate = legs.Max(t => t.ExpirationDate),
                    TradeDate = firstLeg.Date,
                    NetPremium = netPremium,
                    LegCount = legs.Count,
                    IsOpen = firstLeg.IsOpeningTrade ?? true,
                    AccountId = firstLeg.AccountId,
                    AccountName = firstLeg.Account?.Name,
                    Legs = legs.Select(t => new OptionLeg
                    {
                        Id = t.Id,
                        Symbol = t.Symbol,
                        Action = t.Type,
                        OptionType = t.OptionType?.ToString() ?? "",
                        StrikePrice = t.StrikePrice ?? 0,
                        Quantity = t.Quantity,
                        Price = t.Price,
                        Premium = t.Price * t.Quantity * t.ContractMultiplier * (t.Type.Contains("SELL") ? 1 : -1),
                        LegNumber = t.SpreadLegNumber ?? 0,
                        ExpirationDate = t.ExpirationDate
                    }).ToList()
                };
            })
            .OrderByDescending(g => g.TradeDate)
            .ToList();

        // Also include single-leg options that aren't part of a spread
        var singleOptions = optionTrades
            .Where(t => string.IsNullOrEmpty(t.SpreadGroupId) || t.SpreadType == SpreadType.Single)
            .Select(t => new OptionSpreadGroup
            {
                SpreadGroupId = t.Id, // Use trade ID as group ID
                SpreadType = "Single",
                StrategyName = $"Single {t.OptionType}",
                UnderlyingSymbol = t.UnderlyingSymbol ?? t.Symbol,
                ExpirationDate = t.ExpirationDate,
                TradeDate = t.Date,
                NetPremium = t.Price * t.Quantity * t.ContractMultiplier * (t.Type.Contains("SELL") ? 1 : -1),
                LegCount = 1,
                IsOpen = t.IsOpeningTrade ?? true,
                AccountId = t.AccountId,
                AccountName = t.Account?.Name,
                Legs = new List<OptionLeg>
                {
                    new OptionLeg
                    {
                        Id = t.Id,
                        Symbol = t.Symbol,
                        Action = t.Type,
                        OptionType = t.OptionType?.ToString() ?? "",
                        StrikePrice = t.StrikePrice ?? 0,
                        Quantity = t.Quantity,
                        Price = t.Price,
                        Premium = t.Price * t.Quantity * t.ContractMultiplier * (t.Type.Contains("SELL") ? 1 : -1),
                        LegNumber = 1,
                        ExpirationDate = t.ExpirationDate
                    }
                }
            })
            .ToList();

        // Combine and sort by date
        var allGroups = spreadGroups.Concat(singleOptions)
            .OrderByDescending(g => g.TradeDate)
            .ToList();

        return Ok(allGroups);
    }

    private static string ExtractStrategyName(string? notes, string fallbackType)
    {
        if (string.IsNullOrEmpty(notes))
            return fallbackType;
        
        // Try to extract strategy name from notes (e.g., "Credit Call Spread ($588.66)")
        var match = Regex.Match(notes, @"^([^(]+)");
        if (match.Success)
        {
            return match.Groups[1].Value.Trim();
        }
        
        return fallbackType;
    }
}

public class PortfolioWithQuote
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
public class OptionSpreadGroup
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
    public List<OptionLeg> Legs { get; set; } = new();
}

public class OptionLeg
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
