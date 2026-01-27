using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TradingJournal.Web.Models;
using TradingJournal.Web.Services;

namespace TradingJournal.Web.Controllers;

[Authorize]
public class DividendsController : Controller
{
    private readonly ApiClient _apiClient;

    public DividendsController(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<IActionResult> Index(string? accountId = null)
    {
        try
        {
            var endpoint = string.IsNullOrEmpty(accountId) 
                ? "dividends" 
                : $"dividends?accountId={accountId}";
            var dividends = await _apiClient.GetAsync<List<DividendDto>>(endpoint) ?? new List<DividendDto>();
            
            var summaryEndpoint = string.IsNullOrEmpty(accountId) 
                ? "dividends/summary" 
                : $"dividends/summary?accountId={accountId}";
            var summary = await _apiClient.GetAsync<DividendSummaryDto>(summaryEndpoint);

            var accounts = await _apiClient.GetAsync<List<AccountDto>>("accounts") ?? new List<AccountDto>();
            
            ViewBag.Accounts = accounts;
            ViewBag.SelectedAccountId = accountId;
            ViewBag.Summary = summary;

            return View(dividends);
        }
        catch (UnauthorizedAccessException)
        {
            return RedirectToAction("Login", "Auth");
        }
    }

    public async Task<IActionResult> BySymbol(string? accountId = null)
    {
        try
        {
            var endpoint = string.IsNullOrEmpty(accountId) 
                ? "dividends/by-symbol" 
                : $"dividends/by-symbol?accountId={accountId}";
            var bySymbol = await _apiClient.GetAsync<List<DividendBySymbolDto>>(endpoint) ?? new List<DividendBySymbolDto>();

            var accounts = await _apiClient.GetAsync<List<AccountDto>>("accounts") ?? new List<AccountDto>();
            
            ViewBag.Accounts = accounts;
            ViewBag.SelectedAccountId = accountId;
            ViewBag.TotalDividends = bySymbol.Sum(d => d.TotalAmount);
            ViewBag.TotalSymbols = bySymbol.Count;

            return View(bySymbol);
        }
        catch (UnauthorizedAccessException)
        {
            return RedirectToAction("Login", "Auth");
        }
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        try
        {
            var accounts = await _apiClient.GetAsync<List<AccountDto>>("accounts") ?? new List<AccountDto>();
            ViewBag.Accounts = accounts;
            return View(new CreateDividendDto { PaymentDate = DateTime.Now });
        }
        catch (UnauthorizedAccessException)
        {
            return RedirectToAction("Login", "Auth");
        }
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateDividendDto dividend)
    {
        try
        {
            await _apiClient.PostAsync<object>("dividends", dividend);
            TempData["Success"] = $"Dividend for {dividend.Symbol} recorded successfully!";
            return RedirectToAction("Index");
        }
        catch (Exception ex)
        {
            var accounts = await _apiClient.GetAsync<List<AccountDto>>("accounts") ?? new List<AccountDto>();
            ViewBag.Accounts = accounts;
            ViewBag.Error = ex.Message;
            return View(dividend);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        try
        {
            var dividend = await _apiClient.GetAsync<DividendDto>($"dividends/{id}");
            if (dividend == null)
            {
                return NotFound();
            }

            var accounts = await _apiClient.GetAsync<List<AccountDto>>("accounts") ?? new List<AccountDto>();
            ViewBag.Accounts = accounts;

            var model = new CreateDividendDto
            {
                Symbol = dividend.Symbol,
                Amount = dividend.Amount,
                Quantity = dividend.Quantity,
                PerShareAmount = dividend.PerShareAmount,
                Type = dividend.Type,
                Currency = dividend.Currency,
                PaymentDate = dividend.PaymentDate,
                ExDividendDate = dividend.ExDividendDate,
                RecordDate = dividend.RecordDate,
                Notes = dividend.Notes,
                TaxWithheld = dividend.TaxWithheld,
                AccountId = dividend.AccountId
            };

            ViewBag.DividendId = id;
            return View(model);
        }
        catch (UnauthorizedAccessException)
        {
            return RedirectToAction("Login", "Auth");
        }
    }

    [HttpPost]
    public async Task<IActionResult> Edit(string id, CreateDividendDto dividend)
    {
        try
        {
            await _apiClient.PutAsync<object>($"dividends/{id}", dividend);
            TempData["Success"] = "Dividend updated successfully!";
            return RedirectToAction("Index");
        }
        catch (Exception ex)
        {
            var accounts = await _apiClient.GetAsync<List<AccountDto>>("accounts") ?? new List<AccountDto>();
            ViewBag.Accounts = accounts;
            ViewBag.Error = ex.Message;
            ViewBag.DividendId = id;
            return View(dividend);
        }
    }

    [HttpPost]
    public async Task<IActionResult> Delete(string id)
    {
        try
        {
            await _apiClient.DeleteAsync($"dividends/{id}");
            TempData["Success"] = "Dividend deleted successfully!";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Error deleting dividend: {ex.Message}";
        }
        
        return RedirectToAction("Index");
    }
}
