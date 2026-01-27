using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using TradingJournal.Api.Data;
using TradingJournal.Api.Models;

namespace TradingJournal.Api.Services;

public class AuthService : IAuthService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;

    public AuthService(ApplicationDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        // Check if user exists
        if (await _context.Users.AnyAsync(u => u.Email == request.Email))
        {
            throw new InvalidOperationException("User with this email already exists");
        }

        // Hash password
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);

        // Create user
        var user = new User
        {
            Email = request.Email,
            Password = hashedPassword,
            Name = request.Name,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return GenerateTokenResponse(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
        {
            throw new UnauthorizedAccessException("Invalid credentials");
        }

        return GenerateTokenResponse(user);
    }

    private AuthResponse GenerateTokenResponse(User user)
    {
        var jwtSecret = _configuration["JWT_SECRET"] ?? "your-super-secret-jwt-key-change-this-in-production";
        var key = Encoding.ASCII.GetBytes(jwtSecret);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.NameIdentifier, user.Id)
            }),
            Expires = DateTime.UtcNow.AddDays(7),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = tokenHandler.WriteToken(token);

        return new AuthResponse
        {
            AccessToken = tokenString,
            User = new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                Name = user.Name
            }
        };
    }
}
