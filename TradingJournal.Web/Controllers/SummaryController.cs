using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TradingJournal.Web.Models;
using TradingJournal.Web.Services;

namespace TradingJournal.Web.Controllers;

[Authorize]
public class SummaryController : Controller
{
    private readonly ApiClient _apiClient;

    public SummaryController(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<IActionResult> Index(string? accountId = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        try
        {
            // Default to current year if no dates provided
            if (!startDate.HasValue && !endDate.HasValue)
            {
                startDate = new DateTime(DateTime.Today.Year, 1, 1);
                endDate = DateTime.Today;
            }

            var accounts = await _apiClient.GetAsync<List<AccountDto>>("accounts") ?? new List<AccountDto>();
            ViewBag.Accounts = accounts;
            ViewBag.SelectedAccountId = accountId;
            ViewBag.StartDate = startDate;
            ViewBag.EndDate = endDate;

            var queryParams = new List<string>();
            if (!string.IsNullOrEmpty(accountId))
                queryParams.Add($"accountId={accountId}");
            if (startDate.HasValue)
                queryParams.Add($"startDate={startDate.Value:yyyy-MM-dd}");
            if (endDate.HasValue)
                queryParams.Add($"endDate={endDate.Value:yyyy-MM-dd}");

            var endpoint = "summary";
            if (queryParams.Any())
                endpoint += "?" + string.Join("&", queryParams);

            var summary = await _apiClient.GetAsync<TradingSummaryDto>(endpoint) ?? new TradingSummaryDto();
            return View(summary);
        }
        catch (UnauthorizedAccessException)
        {
            return RedirectToAction("Login", "Auth");
        }
        catch (Exception ex)
        {
            ViewBag.Accounts = new List<AccountDto>();
            ViewBag.SelectedAccountId = accountId;
            ViewBag.StartDate = startDate;
            ViewBag.EndDate = endDate;
            ViewBag.Error = ex.Message;
            return View(new TradingSummaryDto());
        }
    }
}
