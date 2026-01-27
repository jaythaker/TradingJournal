using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TradingJournal.Web.Models;
using TradingJournal.Web.Services;

namespace TradingJournal.Web.Controllers;

[Authorize]
public class PortfolioController : Controller
{
    private readonly ApiClient _apiClient;

    public PortfolioController(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<IActionResult> Index(string? accountId = null)
    {
        try
        {
            // Use the with-quotes endpoint to get live prices
            var endpoint = string.IsNullOrEmpty(accountId) 
                ? "portfolio/with-quotes" 
                : $"portfolio/with-quotes?accountId={accountId}";
            var holdings = await _apiClient.GetAsync<List<PortfolioWithQuoteDto>>(endpoint) ?? new List<PortfolioWithQuoteDto>();

            var accounts = await _apiClient.GetAsync<List<AccountDto>>("accounts") ?? new List<AccountDto>();
            ViewBag.Accounts = accounts;
            ViewBag.SelectedAccountId = accountId;
            
            // Calculate totals from the live data
            var totalMarketValue = holdings.Sum(h => h.MarketValue);
            var totalCostBasis = holdings.Sum(h => h.CostBasis);
            var totalUnrealizedPnL = holdings.Sum(h => h.UnrealizedPnL);
            var totalUnrealizedPnLPercent = totalCostBasis > 0 ? (totalUnrealizedPnL / totalCostBasis) * 100 : 0;
            
            ViewBag.TotalMarketValue = totalMarketValue;
            ViewBag.TotalCostBasis = totalCostBasis;
            ViewBag.TotalUnrealizedPnL = totalUnrealizedPnL;
            ViewBag.TotalUnrealizedPnLPercent = totalUnrealizedPnLPercent;

            return View(holdings);
        }
        catch (UnauthorizedAccessException)
        {
            return RedirectToAction("Login", "Auth");
        }
    }
}

