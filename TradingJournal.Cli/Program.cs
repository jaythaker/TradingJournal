using System.CommandLine;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace TradingJournal.Cli;

class Program
{
    private static readonly HttpClient _httpClient = new();
    private static string _apiUrl = "http://localhost:3333";
    private static string? _token;

    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Trading Journal CLI - Import trades and dividends");

        // Global options
        var apiUrlOption = new Option<string>(
            name: "--api-url",
            description: "API base URL",
            getDefaultValue: () => "http://localhost:3333");

        rootCommand.AddGlobalOption(apiUrlOption);

        // Login command
        var loginCommand = new Command("login", "Login to get authentication token");
        var emailOption = new Option<string>("--email", "Email address") { IsRequired = true };
        var passwordOption = new Option<string>("--password", "Password") { IsRequired = true };
        loginCommand.AddOption(emailOption);
        loginCommand.AddOption(passwordOption);
        loginCommand.SetHandler(async (email, password, apiUrl) =>
        {
            _apiUrl = apiUrl;
            await LoginAsync(email, password);
        }, emailOption, passwordOption, apiUrlOption);

        // Import command group
        var importCommand = new Command("import", "Import trades or dividends from CSV files");

        // Import trades command
        var importTradesCommand = new Command("trades", "Import trades from a CSV file");
        var tradesFileOption = new Option<FileInfo>("--file", "CSV file path") { IsRequired = true };
        var accountIdOption = new Option<string>("--account", "Account ID") { IsRequired = true };
        var tokenOption = new Option<string>("--token", "JWT token (or set TJ_TOKEN env var)");
        var formatOption = new Option<string>(
            "--format",
            getDefaultValue: () => "fidelity",
            description: "CSV format: fidelity, generic");

        importTradesCommand.AddOption(tradesFileOption);
        importTradesCommand.AddOption(accountIdOption);
        importTradesCommand.AddOption(tokenOption);
        importTradesCommand.AddOption(formatOption);
        importTradesCommand.SetHandler(async (file, accountId, token, format, apiUrl) =>
        {
            _apiUrl = apiUrl;
            _token = token ?? Environment.GetEnvironmentVariable("TJ_TOKEN");
            await ImportTradesAsync(file, accountId, format);
        }, tradesFileOption, accountIdOption, tokenOption, formatOption, apiUrlOption);

        // Import dividends command
        var importDividendsCommand = new Command("dividends", "Import dividends from a CSV file");
        var dividendsFileOption = new Option<FileInfo>("--file", "CSV file path") { IsRequired = true };
        var divAccountIdOption = new Option<string>("--account", "Account ID") { IsRequired = true };
        var divTokenOption = new Option<string>("--token", "JWT token (or set TJ_TOKEN env var)");

        importDividendsCommand.AddOption(dividendsFileOption);
        importDividendsCommand.AddOption(divAccountIdOption);
        importDividendsCommand.AddOption(divTokenOption);
        importDividendsCommand.SetHandler(async (file, accountId, token, apiUrl) =>
        {
            _apiUrl = apiUrl;
            _token = token ?? Environment.GetEnvironmentVariable("TJ_TOKEN");
            await ImportDividendsAsync(file, accountId);
        }, dividendsFileOption, divAccountIdOption, divTokenOption, apiUrlOption);

        importCommand.AddCommand(importTradesCommand);
        importCommand.AddCommand(importDividendsCommand);

        // List accounts command
        var accountsCommand = new Command("accounts", "List all accounts");
        var accTokenOption = new Option<string>("--token", "JWT token (or set TJ_TOKEN env var)");
        accountsCommand.AddOption(accTokenOption);
        accountsCommand.SetHandler(async (token, apiUrl) =>
        {
            _apiUrl = apiUrl;
            _token = token ?? Environment.GetEnvironmentVariable("TJ_TOKEN");
            await ListAccountsAsync();
        }, accTokenOption, apiUrlOption);

        // Summary command
        var summaryCommand = new Command("summary", "Show trade summary");
        var sumTokenOption = new Option<string>("--token", "JWT token (or set TJ_TOKEN env var)");
        var sumAccountOption = new Option<string?>("--account", "Filter by account ID");
        summaryCommand.AddOption(sumTokenOption);
        summaryCommand.AddOption(sumAccountOption);
        summaryCommand.SetHandler(async (token, accountId, apiUrl) =>
        {
            _apiUrl = apiUrl;
            _token = token ?? Environment.GetEnvironmentVariable("TJ_TOKEN");
            await ShowSummaryAsync(accountId);
        }, sumTokenOption, sumAccountOption, apiUrlOption);

        rootCommand.AddCommand(loginCommand);
        rootCommand.AddCommand(importCommand);
        rootCommand.AddCommand(accountsCommand);
        rootCommand.AddCommand(summaryCommand);

        return await rootCommand.InvokeAsync(args);
    }

    static async Task LoginAsync(string email, string password)
    {
        Console.WriteLine($"Logging in as {email}...");

        try
        {
            var response = await _httpClient.PostAsJsonAsync($"{_apiUrl}/api/auth/login", new { email, password });

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
                Console.WriteLine();
                Console.WriteLine("Login successful!");
                Console.WriteLine();
                Console.WriteLine("Your token (valid for 7 days):");
                Console.WriteLine(result?.AccessToken);
                Console.WriteLine();
                Console.WriteLine("Set it as environment variable:");
                Console.WriteLine($"  export TJ_TOKEN=\"{result?.AccessToken}\"");
                Console.WriteLine();
                Console.WriteLine("Or pass it with --token option");
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Login failed: {response.StatusCode}");
                Console.WriteLine(error);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    static async Task ImportTradesAsync(FileInfo file, string accountId, string format)
    {
        if (!file.Exists)
        {
            Console.WriteLine($"Error: File not found: {file.FullName}");
            return;
        }

        if (string.IsNullOrEmpty(_token))
        {
            Console.WriteLine("Error: No token provided. Use --token or set TJ_TOKEN environment variable.");
            Console.WriteLine("Run 'tj login --email <email> --password <password>' to get a token.");
            return;
        }

        Console.WriteLine($"Importing trades from: {file.FullName}");
        Console.WriteLine($"Account ID: {accountId}");
        Console.WriteLine($"Format: {format}");
        Console.WriteLine();

        try
        {
            using var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(await File.ReadAllBytesAsync(file.FullName));
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
            content.Add(fileContent, "file", file.Name);
            content.Add(new StringContent(accountId), "accountId");
            content.Add(new StringContent(format), "format");

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);

            var response = await _httpClient.PostAsync($"{_apiUrl}/api/import/trades", content);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ImportResult>();
                Console.WriteLine("Import successful!");
                Console.WriteLine($"  Trades imported: {result?.TradesImported ?? 0}");
                Console.WriteLine($"  Dividends imported: {result?.DividendsImported ?? 0}");
                Console.WriteLine($"  Skipped (duplicates): {result?.Skipped ?? 0}");

                if (result?.Errors?.Any() == true)
                {
                    Console.WriteLine();
                    Console.WriteLine("Errors:");
                    foreach (var error in result.Errors.Take(10))
                    {
                        Console.WriteLine($"  - {error}");
                    }
                    if (result.Errors.Count > 10)
                    {
                        Console.WriteLine($"  ... and {result.Errors.Count - 10} more errors");
                    }
                }
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Import failed: {response.StatusCode}");
                Console.WriteLine(error);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    static async Task ImportDividendsAsync(FileInfo file, string accountId)
    {
        if (!file.Exists)
        {
            Console.WriteLine($"Error: File not found: {file.FullName}");
            return;
        }

        if (string.IsNullOrEmpty(_token))
        {
            Console.WriteLine("Error: No token provided. Use --token or set TJ_TOKEN environment variable.");
            Console.WriteLine("Run 'tj login --email <email> --password <password>' to get a token.");
            return;
        }

        Console.WriteLine($"Importing dividends from: {file.FullName}");
        Console.WriteLine($"Account ID: {accountId}");
        Console.WriteLine();

        try
        {
            using var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(await File.ReadAllBytesAsync(file.FullName));
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
            content.Add(fileContent, "file", file.Name);
            content.Add(new StringContent(accountId), "accountId");
            content.Add(new StringContent("fidelity"), "format");

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);

            var response = await _httpClient.PostAsync($"{_apiUrl}/api/import/trades", content);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ImportResult>();
                Console.WriteLine("Import successful!");
                Console.WriteLine($"  Dividends imported: {result?.DividendsImported ?? 0}");
                Console.WriteLine($"  Skipped (duplicates): {result?.Skipped ?? 0}");
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Import failed: {response.StatusCode}");
                Console.WriteLine(error);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    static async Task ListAccountsAsync()
    {
        if (string.IsNullOrEmpty(_token))
        {
            Console.WriteLine("Error: No token provided. Use --token or set TJ_TOKEN environment variable.");
            return;
        }

        try
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            var response = await _httpClient.GetAsync($"{_apiUrl}/api/accounts");

            if (response.IsSuccessStatusCode)
            {
                var accounts = await response.Content.ReadFromJsonAsync<List<AccountDto>>();
                
                if (accounts?.Any() != true)
                {
                    Console.WriteLine("No accounts found.");
                    return;
                }

                Console.WriteLine("Accounts:");
                Console.WriteLine(new string('-', 80));
                Console.WriteLine($"{"ID",-40} {"Name",-20} {"Type",-15}");
                Console.WriteLine(new string('-', 80));

                foreach (var account in accounts)
                {
                    Console.WriteLine($"{account.Id,-40} {account.Name,-20} {account.Type,-15}");
                }
            }
            else
            {
                Console.WriteLine($"Failed to list accounts: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    static async Task ShowSummaryAsync(string? accountId)
    {
        if (string.IsNullOrEmpty(_token))
        {
            Console.WriteLine("Error: No token provided. Use --token or set TJ_TOKEN environment variable.");
            return;
        }

        try
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            
            var url = $"{_apiUrl}/api/summary";
            if (!string.IsNullOrEmpty(accountId))
            {
                url += $"?accountId={accountId}";
            }

            var response = await _httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var summary = await response.Content.ReadFromJsonAsync<SummaryDto>();
                
                Console.WriteLine();
                Console.WriteLine("Trading Summary");
                Console.WriteLine(new string('=', 50));
                Console.WriteLine();
                
                Console.WriteLine("P&L Statistics:");
                Console.WriteLine($"  Total P&L:        ${summary?.PnLStats?.TotalPnL:N2}");
                Console.WriteLine($"  Total Profit:     ${summary?.PnLStats?.TotalProfit:N2}");
                Console.WriteLine($"  Total Loss:       ${summary?.PnLStats?.TotalLoss:N2}");
                Console.WriteLine($"  Max Profit:       ${summary?.PnLStats?.MaxProfit:N2}");
                Console.WriteLine($"  Max Loss:         ${summary?.PnLStats?.MaxLoss:N2}");
                Console.WriteLine();
                
                Console.WriteLine("Performance Scores:");
                Console.WriteLine($"  Win Rate:         {summary?.PerformanceScores?.WinRate:N1}%");
                Console.WriteLine($"  Profit Factor:    {summary?.PerformanceScores?.ProfitFactor:N2}");
                Console.WriteLine($"  Expectancy:       ${summary?.PerformanceScores?.Expectancy:N2}");
                Console.WriteLine($"  Max Drawdown:     {summary?.PerformanceScores?.MaxDrawdownPercent:N1}%");
                Console.WriteLine();
                
                Console.WriteLine("Trade Statistics:");
                Console.WriteLine($"  Total Trades:     {summary?.TradeStats?.TotalTrades}");
                Console.WriteLine($"  Winning Trades:   {summary?.TradeStats?.WinningTrades}");
                Console.WriteLine($"  Losing Trades:    {summary?.TradeStats?.LosingTrades}");
                Console.WriteLine($"  Trading Days:     {summary?.TradeStats?.TradingDays}");
                Console.WriteLine();
                
                Console.WriteLine("Dividend Statistics:");
                Console.WriteLine($"  Total Dividends:  ${summary?.DividendStats?.TotalDividends:N2}");
                Console.WriteLine($"  Payments:         {summary?.DividendStats?.TotalPayments}");
            }
            else
            {
                Console.WriteLine($"Failed to get summary: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}

// DTOs
record LoginResponse(string AccessToken, UserDto User);
record UserDto(string Id, string Email, string? Name);
record AccountDto(string Id, string Name, string Type, string? Description);
record ImportResult(int TradesImported, int DividendsImported, int Skipped, List<string> Errors);

record SummaryDto(
    PnLStatsDto? PnLStats,
    PerformanceScoresDto? PerformanceScores,
    TradeStatsDto? TradeStats,
    DividendStatsDto? DividendStats
);

record PnLStatsDto(double TotalPnL, double TotalProfit, double TotalLoss, double MaxProfit, double MaxLoss);
record PerformanceScoresDto(double WinRate, double ProfitFactor, double Expectancy, double MaxDrawdownPercent);
record TradeStatsDto(int TotalTrades, int WinningTrades, int LosingTrades, int TradingDays);
record DividendStatsDto(double TotalDividends, int TotalPayments);
