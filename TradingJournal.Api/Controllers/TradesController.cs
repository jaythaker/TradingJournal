using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TradingJournal.Api.Models;
using TradingJournal.Api.Services;

namespace TradingJournal.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TradesController : ControllerBase
{
    private readonly ITradeService _tradeService;

    public TradesController(ITradeService tradeService)
    {
        _tradeService = tradeService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Trade>>> GetTrades(
        [FromQuery] string? accountId = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var trades = await _tradeService.GetTradesByUserIdAsync(userId, accountId, startDate, endDate);
        return Ok(trades);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Trade>> GetTrade(string id)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var trade = await _tradeService.GetTradeByIdAsync(id, userId);
        if (trade == null)
        {
            return NotFound();
        }

        return Ok(trade);
    }

    [HttpPost]
    public async Task<ActionResult<Trade>> CreateTrade([FromBody] CreateTradeRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        try
        {
            var trade = await _tradeService.CreateTradeAsync(request, userId);
            return CreatedAtAction(nameof(GetTrade), new { id = trade.Id }, trade);
        }
        catch (KeyNotFoundException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<Trade>> UpdateTrade(string id, [FromBody] UpdateTradeRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        try
        {
            var trade = await _tradeService.UpdateTradeAsync(id, request, userId);
            return Ok(trade);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTrade(string id)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        try
        {
            await _tradeService.DeleteTradeAsync(id, userId);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("summary")]
    public async Task<ActionResult<IEnumerable<SymbolTradeSummary>>> GetTradeSummary(
        [FromQuery] string? accountId = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var summary = await _tradeService.GetTradeSummaryBySymbolAsync(userId, accountId, startDate, endDate);
        return Ok(summary);
    }

    [HttpGet("analysis")]
    public async Task<ActionResult<TimeAnalysisSummary>> GetTimeAnalysis(
        [FromQuery] string? accountId = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var analysis = await _tradeService.GetTimeAnalysisAsync(userId, accountId, startDate, endDate);
        return Ok(analysis);
    }

    [HttpPost("cleanup-duplicates")]
    public async Task<ActionResult<DuplicateCleanupResult>> CleanupDuplicates([FromQuery] string? accountId = null)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var result = await _tradeService.FindAndRemoveDuplicatesAsync(userId, accountId);
        return Ok(result);
    }

    [HttpDelete("account/{accountId}/all")]
    public async Task<ActionResult<DeleteAllTradesResult>> DeleteAllTradesForAccount(string accountId)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var result = await _tradeService.DeleteAllTradesForAccountAsync(userId, accountId);
        
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
}
