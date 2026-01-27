using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TradingJournal.Api.Services;

namespace TradingJournal.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SummaryController : ControllerBase
{
    private readonly ISummaryService _summaryService;

    public SummaryController(ISummaryService summaryService)
    {
        _summaryService = summaryService;
    }

    [HttpGet]
    public async Task<ActionResult<TradingSummary>> GetSummary(
        [FromQuery] string? accountId = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var summary = await _summaryService.GetTradingSummaryAsync(userId, accountId, startDate, endDate);
        return Ok(summary);
    }
}
