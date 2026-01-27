using TradingJournal.Api.Models;

namespace TradingJournal.Api.Services;

public interface IAccountService
{
    Task<IEnumerable<Account>> GetAccountsByUserIdAsync(string userId);
    Task<Account?> GetAccountByIdAsync(string accountId, string userId);
    Task<Account> CreateAccountAsync(CreateAccountRequest request, string userId);
    Task<Account> UpdateAccountAsync(string accountId, UpdateAccountRequest request, string userId);
    Task DeleteAccountAsync(string accountId, string userId);
}

public class CreateAccountRequest
{
    public string Name { get; set; } = string.Empty;
    public string Currency { get; set; } = "USD";
}

public class UpdateAccountRequest
{
    public string? Name { get; set; }
    public string? Currency { get; set; }
}
