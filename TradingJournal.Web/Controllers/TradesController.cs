using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TradingJournal.Web.Models;
using TradingJournal.Web.Services;

namespace TradingJournal.Web.Controllers;

[Authorize]
public class TradesController : Controller
{
    private readonly ApiClient _apiClient;

    public TradesController(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<IActionResult> Index(string? accountId = null, string? instrumentFilter = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        try
        {
            // Default to current year if no dates provided
            if (!startDate.HasValue && !endDate.HasValue)
            {
                startDate = new DateTime(DateTime.Today.Year, 1, 1);
                endDate = DateTime.Today;
            }

            var queryParams = new List<string>();
            if (!string.IsNullOrEmpty(accountId))
                queryParams.Add($"accountId={accountId}");
            if (startDate.HasValue)
                queryParams.Add($"startDate={startDate.Value:yyyy-MM-dd}");
            if (endDate.HasValue)
                queryParams.Add($"endDate={endDate.Value:yyyy-MM-dd}");

            var endpoint = "trades";
            if (queryParams.Any())
                endpoint += "?" + string.Join("&", queryParams);

            var trades = await _apiClient.GetAsync<List<TradeDto>>(endpoint) ?? new List<TradeDto>();
            
            // Filter by instrument type
            if (instrumentFilter == "stock")
            {
                trades = trades.Where(t => t.InstrumentType == "Stock").ToList();
            }
            else if (instrumentFilter == "option")
            {
                trades = trades.Where(t => t.InstrumentType == "Option").ToList();
            }
            
            var accounts = await _apiClient.GetAsync<List<AccountDto>>("accounts") ?? new List<AccountDto>();
            ViewBag.Accounts = accounts;
            ViewBag.SelectedAccountId = accountId;
            ViewBag.InstrumentFilter = instrumentFilter ?? "all";
            ViewBag.StartDate = startDate;
            ViewBag.EndDate = endDate;

            return View(trades);
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
            return View();
        }
        catch (UnauthorizedAccessException)
        {
            return RedirectToAction("Login", "Auth");
        }
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateTradeDto trade)
    {
        try
        {
            await _apiClient.PostAsync<object>("trades", trade);
            return RedirectToAction("Index");
        }
        catch (Exception ex)
        {
            ViewBag.Error = ex.Message;
            var accounts = await _apiClient.GetAsync<List<AccountDto>>("accounts") ?? new List<AccountDto>();
            ViewBag.Accounts = accounts;
            return View(trade);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        try
        {
            var trade = await _apiClient.GetAsync<TradeDto>($"trades/{id}");
            if (trade == null)
            {
                return NotFound();
            }

            var accounts = await _apiClient.GetAsync<List<AccountDto>>("accounts") ?? new List<AccountDto>();
            ViewBag.Accounts = accounts;

            var editModel = new CreateTradeDto
            {
                Symbol = trade.Symbol,
                Type = trade.Type,
                Quantity = trade.Quantity,
                Price = trade.Price,
                Fee = trade.Fee,
                Date = trade.Date,
                AccountId = "" // Will need to get from trade
            };

            return View(editModel);
        }
        catch (UnauthorizedAccessException)
        {
            return RedirectToAction("Login", "Auth");
        }
    }

    [HttpPost]
    public async Task<IActionResult> Edit(string id, CreateTradeDto trade)
    {
        try
        {
            await _apiClient.PutAsync<object>($"trades/{id}", trade);
            return RedirectToAction("Index");
        }
        catch (Exception ex)
        {
            ViewBag.Error = ex.Message;
            var accounts = await _apiClient.GetAsync<List<AccountDto>>("accounts") ?? new List<AccountDto>();
            ViewBag.Accounts = accounts;
            return View(trade);
        }
    }

    [HttpPost]
    public async Task<IActionResult> Delete(string id)
    {
        try
        {
            await _apiClient.DeleteAsync($"trades/{id}");
            return RedirectToAction("Index");
        }
        catch
        {
            return RedirectToAction("Index");
        }
    }

    public async Task<IActionResult> Summary(string? accountId = null, string? status = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        try
        {
            // Default to current year if no dates provided
            if (!startDate.HasValue && !endDate.HasValue)
            {
                startDate = new DateTime(DateTime.Today.Year, 1, 1);
                endDate = DateTime.Today;
            }

            var queryParams = new List<string>();
            if (!string.IsNullOrEmpty(accountId))
                queryParams.Add($"accountId={accountId}");
            if (startDate.HasValue)
                queryParams.Add($"startDate={startDate.Value:yyyy-MM-dd}");
            if (endDate.HasValue)
                queryParams.Add($"endDate={endDate.Value:yyyy-MM-dd}");

            var endpoint = "trades/summary";
            if (queryParams.Any())
                endpoint += "?" + string.Join("&", queryParams);

            var summary = await _apiClient.GetAsync<List<SymbolTradeSummaryDto>>(endpoint) ?? new List<SymbolTradeSummaryDto>();
            
            // Filter by status
            if (status == "open")
            {
                summary = summary.Where(s => s.CurrentQuantity > 0).ToList();
            }
            else if (status == "closed")
            {
                summary = summary.Where(s => s.CurrentQuantity <= 0).ToList();
            }
            
            var accounts = await _apiClient.GetAsync<List<AccountDto>>("accounts") ?? new List<AccountDto>();
            ViewBag.Accounts = accounts;
            ViewBag.SelectedAccountId = accountId;
            ViewBag.Status = status;
            ViewBag.StartDate = startDate;
            ViewBag.EndDate = endDate;

            return View(summary);
        }
        catch (UnauthorizedAccessException)
        {
            return RedirectToAction("Login", "Auth");
        }
    }

    public async Task<IActionResult> Analysis(string? accountId = null, string view = "monthly", DateTime? startDate = null, DateTime? endDate = null)
    {
        try
        {
            // Default to current year if no dates provided
            if (!startDate.HasValue && !endDate.HasValue)
            {
                startDate = new DateTime(DateTime.Today.Year, 1, 1);
                endDate = DateTime.Today;
            }

            var queryParams = new List<string>();
            if (!string.IsNullOrEmpty(accountId))
                queryParams.Add($"accountId={accountId}");
            if (startDate.HasValue)
                queryParams.Add($"startDate={startDate.Value:yyyy-MM-dd}");
            if (endDate.HasValue)
                queryParams.Add($"endDate={endDate.Value:yyyy-MM-dd}");

            var endpoint = "trades/analysis";
            if (queryParams.Any())
                endpoint += "?" + string.Join("&", queryParams);

            var analysis = await _apiClient.GetAsync<TimeAnalysisSummaryDto>(endpoint) ?? new TimeAnalysisSummaryDto();
            
            var accounts = await _apiClient.GetAsync<List<AccountDto>>("accounts") ?? new List<AccountDto>();
            ViewBag.Accounts = accounts;
            ViewBag.SelectedAccountId = accountId;
            ViewBag.View = view;
            ViewBag.StartDate = startDate;
            ViewBag.EndDate = endDate;

            return View(analysis);
        }
        catch (UnauthorizedAccessException)
        {
            return RedirectToAction("Login", "Auth");
        }
    }

    public async Task<IActionResult> Options(string? accountId = null, string? underlying = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        try
        {
            // Default to current year if no dates provided
            if (!startDate.HasValue && !endDate.HasValue)
            {
                startDate = new DateTime(DateTime.Today.Year, 1, 1);
                endDate = DateTime.Today;
            }

            var queryParams = new List<string>();
            if (!string.IsNullOrEmpty(accountId))
                queryParams.Add($"accountId={accountId}");
            if (startDate.HasValue)
                queryParams.Add($"startDate={startDate.Value:yyyy-MM-dd}");
            if (endDate.HasValue)
                queryParams.Add($"endDate={endDate.Value:yyyy-MM-dd}");

            var endpoint = "trades";
            if (queryParams.Any())
                endpoint += "?" + string.Join("&", queryParams);

            var allTrades = await _apiClient.GetAsync<List<TradeDto>>(endpoint) ?? new List<TradeDto>();
            
            // Filter to options only
            var optionsTrades = allTrades.Where(t => t.InstrumentType == "Option").ToList();
            
            // Filter by underlying if specified
            if (!string.IsNullOrEmpty(underlying))
            {
                optionsTrades = optionsTrades.Where(t => t.UnderlyingSymbol == underlying).ToList();
            }
            
            var accounts = await _apiClient.GetAsync<List<AccountDto>>("accounts") ?? new List<AccountDto>();
            ViewBag.Accounts = accounts;
            ViewBag.SelectedAccountId = accountId;
            ViewBag.Underlying = underlying;
            ViewBag.StartDate = startDate;
            ViewBag.EndDate = endDate;
            
            // Get unique underlying symbols
            ViewBag.UnderlyingSymbols = optionsTrades
                .Where(t => !string.IsNullOrEmpty(t.UnderlyingSymbol))
                .Select(t => t.UnderlyingSymbol!)
                .Distinct()
                .OrderBy(s => s)
                .ToList();

            return View(optionsTrades);
        }
        catch (UnauthorizedAccessException)
        {
            return RedirectToAction("Login", "Auth");
        }
    }
}

public class SymbolTradeSummaryDto
{
    public string Symbol { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public int BuyCount { get; set; }
    public double TotalBuyQuantity { get; set; }
    public double TotalBuyAmount { get; set; }
    public double AverageBuyPrice { get; set; }
    public int SellCount { get; set; }
    public double TotalSellQuantity { get; set; }
    public double TotalSellAmount { get; set; }
    public double AverageSellPrice { get; set; }
    public double CurrentQuantity { get; set; }
    public double CurrentCostBasis { get; set; }
    public double RealizedPnL { get; set; }
    public double RealizedPnLPercent { get; set; }
    public List<TradeDetailDto> Trades { get; set; } = new();
}

public class TradeDetailDto
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public double Quantity { get; set; }
    public double Price { get; set; }
    public double Fee { get; set; }
    public double Total { get; set; }
    public DateTime Date { get; set; }
    public string? Notes { get; set; }
}

public class TimeAnalysisSummaryDto
{
    public List<TimePeriodSummaryDto> Monthly { get; set; } = new();
    public List<TimePeriodSummaryDto> Weekly { get; set; } = new();
    public double TotalRealizedPnL { get; set; }
    public int TotalTrades { get; set; }
    public double OverallWinRate { get; set; }
}

public class TimePeriodSummaryDto
{
    public string Period { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TotalTrades { get; set; }
    public int BuyCount { get; set; }
    public int SellCount { get; set; }
    public double TotalBuyAmount { get; set; }
    public double TotalSellAmount { get; set; }
    public double RealizedPnL { get; set; }
    public double RealizedPnLPercent { get; set; }
    public int WinningTrades { get; set; }
    public int LosingTrades { get; set; }
    public double WinRate { get; set; }
    public List<SymbolPnLDto> SymbolBreakdown { get; set; } = new();
}

public class SymbolPnLDto
{
    public string Symbol { get; set; } = string.Empty;
    public int TradeCount { get; set; }
    public double RealizedPnL { get; set; }
}

