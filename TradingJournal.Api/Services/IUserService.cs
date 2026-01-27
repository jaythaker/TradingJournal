using TradingJournal.Api.Models;

namespace TradingJournal.Api.Services;

public interface IUserService
{
    Task<UserDto?> GetUserByIdAsync(string userId);
}
