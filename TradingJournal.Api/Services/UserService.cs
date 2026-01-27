using Microsoft.EntityFrameworkCore;
using TradingJournal.Api.Data;

namespace TradingJournal.Api.Services;

public class UserService : IUserService
{
    private readonly ApplicationDbContext _context;

    public UserService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<UserDto?> GetUserByIdAsync(string userId)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null) return null;

        return new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            Name = user.Name
        };
    }
}
