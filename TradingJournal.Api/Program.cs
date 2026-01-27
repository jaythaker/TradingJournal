using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Text.Json.Serialization;
using TradingJournal.Api.Data;
using TradingJournal.Api.Services;
using TradingJournal.Api.Services.Import;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? builder.Configuration["DATABASE_URL"]
    ?? "Host=localhost;Database=tradingjournal;Username=tradingjournal;Password=tradingjournal";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// Authentication
var jwtSecret = builder.Configuration["JWT_SECRET"] ?? "your-super-secret-jwt-key-change-this-in-production";
var key = Encoding.ASCII.GetBytes(jwtSecret);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = false,
        ValidateAudience = false
    };
});

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<ITradeService, TradeService>();
builder.Services.AddScoped<IPortfolioService, PortfolioService>();
builder.Services.AddScoped<IImportService, ImportService>();
builder.Services.AddScoped<IDividendService, DividendService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<ISummaryService, SummaryService>();
builder.Services.AddSingleton<IStockQuoteService, StockQuoteService>();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(builder.Configuration["FRONTEND_URL"] ?? "http://localhost:5000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Ensure dividends table exists (for existing databases without migration tracking)
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    try
    {
        await context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS dividends (
                id TEXT PRIMARY KEY,
                symbol TEXT NOT NULL,
                amount DOUBLE PRECISION NOT NULL,
                quantity DOUBLE PRECISION,
                ""perShareAmount"" DOUBLE PRECISION,
                type TEXT NOT NULL DEFAULT 'CASH',
                currency TEXT NOT NULL DEFAULT 'USD',
                ""paymentDate"" TIMESTAMP WITH TIME ZONE NOT NULL,
                ""exDividendDate"" TIMESTAMP WITH TIME ZONE,
                ""recordDate"" TIMESTAMP WITH TIME ZONE,
                notes TEXT,
                ""taxWithheld"" DOUBLE PRECISION NOT NULL DEFAULT 0,
                ""accountId"" TEXT NOT NULL REFERENCES accounts(id) ON DELETE CASCADE,
                ""userId"" TEXT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
                ""createdAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                ""updatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
            );
        ");
        Console.WriteLine("Dividends table ready.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Note: {ex.Message}");
    }
}

app.Run();
