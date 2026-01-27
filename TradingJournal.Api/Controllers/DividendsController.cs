using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TradingJournal.Api.Models;
using TradingJournal.Api.Services;

namespace TradingJournal.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DividendsController : ControllerBase
{
    private readonly IDividendService _dividendService;

    public DividendsController(IDividendService dividendService)
    {
        _dividendService = dividendService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Dividend>>> GetDividends([FromQuery] string? accountId = null)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var dividends = await _dividendService.GetDividendsByUserIdAsync(userId, accountId);
        return Ok(dividends);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Dividend>> GetDividend(string id)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var dividend = await _dividendService.GetDividendByIdAsync(id, userId);
        if (dividend == null)
        {
            return NotFound();
        }

        return Ok(dividend);
    }

    [HttpPost]
    public async Task<ActionResult<Dividend>> CreateDividend([FromBody] CreateDividendRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        try
        {
            var dividend = await _dividendService.CreateDividendAsync(request, userId);
            return CreatedAtAction(nameof(GetDividend), new { id = dividend.Id }, dividend);
        }
        catch (KeyNotFoundException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<Dividend>> UpdateDividend(string id, [FromBody] UpdateDividendRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        try
        {
            var dividend = await _dividendService.UpdateDividendAsync(id, request, userId);
            return Ok(dividend);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteDividend(string id)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        try
        {
            await _dividendService.DeleteDividendAsync(id, userId);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("summary")]
    public async Task<ActionResult<DividendSummary>> GetSummary([FromQuery] string? accountId = null)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var summary = await _dividendService.GetDividendSummaryAsync(userId, accountId);
        return Ok(summary);
    }

    [HttpGet("by-symbol")]
    public async Task<ActionResult<IEnumerable<DividendBySymbol>>> GetBySymbol([FromQuery] string? accountId = null)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var bySymbol = await _dividendService.GetDividendsBySymbolAsync(userId, accountId);
        return Ok(bySymbol);
    }
}
