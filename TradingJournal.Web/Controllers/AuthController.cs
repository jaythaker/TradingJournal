using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using TradingJournal.Web.Services;

namespace TradingJournal.Web.Controllers;

public class AuthController : Controller
{
    private readonly ApiClient _apiClient;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuthController(ApiClient apiClient, IHttpContextAccessor httpContextAccessor)
    {
        _apiClient = apiClient;
        _httpContextAccessor = httpContextAccessor;
    }

    [HttpGet]
    public IActionResult Login()
    {
        if (IsAuthenticated())
        {
            return RedirectToAction("Index", "Dashboard");
        }
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Login(string email, string password)
    {
        try
        {
            var response = await _apiClient.PostAsync<AuthResponse>("auth/login", new { email, password });
            if (response != null)
            {
                HttpContext.Session.SetString("Token", response.AccessToken);
                HttpContext.Session.SetString("UserId", response.User.Id);
                HttpContext.Session.SetString("UserEmail", response.User.Email ?? "");
                
                var claims = new List<System.Security.Claims.Claim>
                {
                    new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, response.User.Id),
                    new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Email, response.User.Email ?? ""),
                    new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, response.User.Name ?? "")
                };
                var identity = new System.Security.Claims.ClaimsIdentity(claims, "CookieAuth");
                var principal = new System.Security.Claims.ClaimsPrincipal(identity);
                await HttpContext.SignInAsync("CookieAuth", principal);
                
                return RedirectToAction("Index", "Dashboard");
            }
        }
        catch
        {
            ViewBag.Error = "Invalid email or password";
        }
        return View();
    }

    [HttpGet]
    public IActionResult Register()
    {
        if (IsAuthenticated())
        {
            return RedirectToAction("Index", "Dashboard");
        }
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Register(string email, string password, string? name)
    {
        try
        {
            var response = await _apiClient.PostAsync<AuthResponse>("auth/register", new { email, password, name });
            if (response != null)
            {
                HttpContext.Session.SetString("Token", response.AccessToken);
                HttpContext.Session.SetString("UserId", response.User.Id);
                HttpContext.Session.SetString("UserEmail", response.User.Email ?? "");
                
                var claims = new List<System.Security.Claims.Claim>
                {
                    new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, response.User.Id),
                    new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Email, response.User.Email ?? ""),
                    new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, response.User.Name ?? "")
                };
                var identity = new System.Security.Claims.ClaimsIdentity(claims, "CookieAuth");
                var principal = new System.Security.Claims.ClaimsPrincipal(identity);
                await HttpContext.SignInAsync("CookieAuth", principal);
                
                return RedirectToAction("Index", "Dashboard");
            }
        }
        catch (Exception ex)
        {
            ViewBag.Error = ex.Message;
        }
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        HttpContext.Session.Clear();
        await HttpContext.SignOutAsync("CookieAuth");
        return RedirectToAction("Login");
    }

    private bool IsAuthenticated()
    {
        return !string.IsNullOrEmpty(HttpContext.Session.GetString("Token"));
    }
}

public class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public UserDto User { get; set; } = null!;
}

public class UserDto
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Name { get; set; }
}
