using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TradingJournal.Api.Models;
using TradingJournal.Api.Services;

namespace TradingJournal.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AccountsController : ControllerBase
{
    private readonly IAccountService _accountService;

    public AccountsController(IAccountService accountService)
    {
        _accountService = accountService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Account>>> GetAccounts()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var accounts = await _accountService.GetAccountsByUserIdAsync(userId);
        return Ok(accounts);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Account>> GetAccount(string id)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var account = await _accountService.GetAccountByIdAsync(id, userId);
        if (account == null)
        {
            return NotFound();
        }

        return Ok(account);
    }

    [HttpPost]
    public async Task<ActionResult<Account>> CreateAccount([FromBody] CreateAccountRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var account = await _accountService.CreateAccountAsync(request, userId);
        return CreatedAtAction(nameof(GetAccount), new { id = account.Id }, account);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<Account>> UpdateAccount(string id, [FromBody] UpdateAccountRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        try
        {
            var account = await _accountService.UpdateAccountAsync(id, request, userId);
            return Ok(account);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAccount(string id)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        try
        {
            await _accountService.DeleteAccountAsync(id, userId);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
