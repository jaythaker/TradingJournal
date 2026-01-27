using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TradingJournal.Api.Services.Import;

namespace TradingJournal.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ImportController : ControllerBase
{
    private readonly IImportService _importService;

    public ImportController(IImportService importService)
    {
        _importService = importService;
    }

    [HttpGet("formats")]
    public ActionResult<IEnumerable<ImportFormatInfo>> GetFormats()
    {
        return Ok(_importService.GetAvailableFormats());
    }

    [HttpPost("trades")]
    public async Task<ActionResult<ImportResult>> ImportTrades(
        [FromForm] IFormFile file, 
        [FromForm] string accountId, 
        [FromForm] string? format = null)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        if (file == null || file.Length == 0)
        {
            return BadRequest(new { error = "No file uploaded" });
        }

        if (string.IsNullOrEmpty(accountId))
        {
            return BadRequest(new { error = "Account ID is required" });
        }

        using var stream = file.OpenReadStream();
        var result = await _importService.ImportTradesAsync(stream, userId, accountId, format);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
}
