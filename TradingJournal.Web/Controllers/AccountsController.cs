using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TradingJournal.Web.Models;
using TradingJournal.Web.Services;

namespace TradingJournal.Web.Controllers;

[Authorize]
public class AccountsController : Controller
{
    private readonly ApiClient _apiClient;

    public AccountsController(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            var accounts = await _apiClient.GetAsync<List<AccountDto>>("accounts") ?? new List<AccountDto>();
            return View(accounts);
        }
        catch (UnauthorizedAccessException)
        {
            return RedirectToAction("Login", "Auth");
        }
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateAccountDto account)
    {
        try
        {
            await _apiClient.PostAsync<object>("accounts", account);
            return RedirectToAction("Index");
        }
        catch (Exception ex)
        {
            ViewBag.Error = ex.Message;
            return View(account);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        try
        {
            var account = await _apiClient.GetAsync<AccountDto>($"accounts/{id}");
            if (account == null)
            {
                return NotFound();
            }
            return View(account);
        }
        catch (UnauthorizedAccessException)
        {
            return RedirectToAction("Login", "Auth");
        }
    }

    [HttpPost]
    public async Task<IActionResult> Edit(string id, AccountDto account)
    {
        try
        {
            await _apiClient.PutAsync<object>($"accounts/{id}", new { account.Name, account.Currency });
            return RedirectToAction("Index");
        }
        catch (Exception ex)
        {
            ViewBag.Error = ex.Message;
            return View(account);
        }
    }

    [HttpPost]
    public async Task<IActionResult> Delete(string id)
    {
        try
        {
            await _apiClient.DeleteAsync($"accounts/{id}");
            return RedirectToAction("Index");
        }
        catch
        {
            return RedirectToAction("Index");
        }
    }

    [HttpPost]
    public async Task<IActionResult> ClearTrades(string id)
    {
        try
        {
            var result = await _apiClient.DeleteAsync<ClearTradesResultDto>($"trades/account/{id}/all");
            if (result != null && result.Success)
            {
                TempData["Success"] = $"Successfully deleted {result.DeletedCount} trades from {result.AccountName}";
            }
            else
            {
                TempData["Error"] = result?.Error ?? "Failed to clear trades";
            }
        }
        catch (UnauthorizedAccessException)
        {
            return RedirectToAction("Login", "Auth");
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Error clearing trades: {ex.Message}";
        }
        
        return RedirectToAction("Index");
    }
}

