using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TradingJournal.Web.Models;
using TradingJournal.Web.Services;

namespace TradingJournal.Web.Controllers;

[Authorize]
public class ImportController : Controller
{
    private readonly ApiClient _apiClient;

    public ImportController(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            var accounts = await _apiClient.GetAsync<List<AccountDto>>("accounts") ?? new List<AccountDto>();
            var formats = await _apiClient.GetAsync<List<ImportFormatDto>>("import/formats") ?? new List<ImportFormatDto>();
            
            ViewBag.Accounts = accounts;
            ViewBag.Formats = formats;
        }
        catch (UnauthorizedAccessException)
        {
            return RedirectToAction("Login", "Auth");
        }
        catch
        {
            ViewBag.Accounts = new List<AccountDto>();
            ViewBag.Formats = new List<ImportFormatDto>();
        }

        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Upload(IFormFile file, string accountId, string? format)
    {
        if (file == null || file.Length == 0)
        {
            TempData["Error"] = "Please select a file to upload.";
            return RedirectToAction("Index");
        }

        if (string.IsNullOrEmpty(accountId))
        {
            TempData["Error"] = "Please select an account.";
            return RedirectToAction("Index");
        }

        try
        {
            var result = await _apiClient.UploadFileAsync<ImportResultDto>("import/trades", file, accountId, format);
            
            if (result != null && result.Success)
            {
                TempData["Success"] = $"Successfully imported {result.ImportedCount} trades. {result.SkippedCount} rows skipped.";
                if (result.Errors?.Count > 0)
                {
                    TempData["Warnings"] = string.Join("; ", result.Errors.Take(5));
                }
            }
            else
            {
                TempData["Error"] = result?.Errors?.FirstOrDefault() ?? "Import failed.";
            }
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Import failed: {ex.Message}";
        }

        return RedirectToAction("Index");
    }
}

public class ImportFormatDto
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class ImportResultDto
{
    public bool Success { get; set; }
    public int ImportedCount { get; set; }
    public int SkippedCount { get; set; }
    public int ErrorCount { get; set; }
    public List<string>? Errors { get; set; }
}
