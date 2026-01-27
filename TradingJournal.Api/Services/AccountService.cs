using Microsoft.EntityFrameworkCore;
using TradingJournal.Api.Data;
using TradingJournal.Api.Models;

namespace TradingJournal.Api.Services;

public class AccountService : IAccountService
{
    private readonly ApplicationDbContext _context;

    public AccountService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Account>> GetAccountsByUserIdAsync(string userId)
    {
        return await _context.Accounts
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }

    public async Task<Account?> GetAccountByIdAsync(string accountId, string userId)
    {
        return await _context.Accounts
            .Include(a => a.Trades.OrderByDescending(t => t.Date).Take(10))
            .FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == userId);
    }

    public async Task<Account> CreateAccountAsync(CreateAccountRequest request, string userId)
    {
        var account = new Account
        {
            Name = request.Name,
            Currency = request.Currency,
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Accounts.Add(account);
        await _context.SaveChangesAsync();

        return account;
    }

    public async Task<Account> UpdateAccountAsync(string accountId, UpdateAccountRequest request, string userId)
    {
        var account = await GetAccountByIdAsync(accountId, userId);
        if (account == null)
        {
            throw new KeyNotFoundException("Account not found");
        }

        if (!string.IsNullOrEmpty(request.Name))
            account.Name = request.Name;
        if (!string.IsNullOrEmpty(request.Currency))
            account.Currency = request.Currency;

        account.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return account;
    }

    public async Task DeleteAccountAsync(string accountId, string userId)
    {
        var account = await GetAccountByIdAsync(accountId, userId);
        if (account == null)
        {
            throw new KeyNotFoundException("Account not found");
        }

        _context.Accounts.Remove(account);
        await _context.SaveChangesAsync();
    }
}
