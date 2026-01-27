using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
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

    public PortfolioController(IPortfolioService portfolioService, IStockQuoteService stockQuoteService)
    {
        _portfolioService = portfolioService;
        _stockQuoteService = stockQuoteService;
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
}
