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

            // Fetch options positions
            var optionsEndpoint = string.IsNullOrEmpty(accountId)
                ? "portfolio/options"
                : $"portfolio/options?accountId={accountId}";
            var optionGroups = await _apiClient.GetAsync<List<OptionSpreadGroupDto>>(optionsEndpoint) ?? new List<OptionSpreadGroupDto>();

            var accounts = await _apiClient.GetAsync<List<AccountDto>>("accounts") ?? new List<AccountDto>();
            ViewBag.Accounts = accounts;
            ViewBag.SelectedAccountId = accountId;
            ViewBag.OptionGroups = optionGroups;
            
            // Calculate totals from the live data
            var totalMarketValue = holdings.Sum(h => h.MarketValue);
            var totalCostBasis = holdings.Sum(h => h.CostBasis);
            var totalUnrealizedPnL = holdings.Sum(h => h.UnrealizedPnL);
            var totalUnrealizedPnLPercent = totalCostBasis > 0 ? (totalUnrealizedPnL / totalCostBasis) * 100 : 0;
            
            // Options summary
            var optionsNetPremium = optionGroups.Sum(g => g.NetPremium);
            var openStrategies = optionGroups.Count(g => g.IsOpen);
            var closedStrategies = optionGroups.Count(g => !g.IsOpen);
            
            ViewBag.TotalMarketValue = totalMarketValue;
            ViewBag.TotalCostBasis = totalCostBasis;
            ViewBag.TotalUnrealizedPnL = totalUnrealizedPnL;
            ViewBag.TotalUnrealizedPnLPercent = totalUnrealizedPnLPercent;
            ViewBag.OptionsNetPremium = optionsNetPremium;
            ViewBag.OpenStrategies = openStrategies;
            ViewBag.ClosedStrategies = closedStrategies;

            return View(holdings);
        }
        catch (UnauthorizedAccessException)
        {
            return RedirectToAction("Login", "Auth");
        }
    }
}

