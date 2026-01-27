using Microsoft.EntityFrameworkCore;
using TradingJournal.Api.Data;
using TradingJournal.Api.Models;

namespace TradingJournal.Api.Services;

public class DividendService : IDividendService
{
    private readonly ApplicationDbContext _context;

    public DividendService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Dividend>> GetDividendsByUserIdAsync(string userId, string? accountId = null)
    {
        var query = _context.Dividends
            .Include(d => d.Account)
            .Where(d => d.UserId == userId);

        if (!string.IsNullOrEmpty(accountId))
        {
            query = query.Where(d => d.AccountId == accountId);
        }

        return await query
            .OrderByDescending(d => d.PaymentDate)
            .ToListAsync();
    }

    public async Task<Dividend?> GetDividendByIdAsync(string dividendId, string userId)
    {
        return await _context.Dividends
            .Include(d => d.Account)
            .FirstOrDefaultAsync(d => d.Id == dividendId && d.UserId == userId);
    }

    public async Task<Dividend> CreateDividendAsync(CreateDividendRequest request, string userId)
    {
        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == request.AccountId && a.UserId == userId);

        if (account == null)
        {
            throw new KeyNotFoundException("Account not found");
        }

        var dividend = new Dividend
        {
            Symbol = request.Symbol.ToUpperInvariant(),
            Amount = request.Amount,
            Quantity = request.Quantity,
            PerShareAmount = request.PerShareAmount,
            Type = request.Type,
            Currency = request.Currency,
            PaymentDate = DateTime.SpecifyKind(request.PaymentDate, DateTimeKind.Utc),
            ExDividendDate = request.ExDividendDate.HasValue ? DateTime.SpecifyKind(request.ExDividendDate.Value, DateTimeKind.Utc) : null,
            RecordDate = request.RecordDate.HasValue ? DateTime.SpecifyKind(request.RecordDate.Value, DateTimeKind.Utc) : null,
            Notes = request.Notes,
            TaxWithheld = request.TaxWithheld,
            AccountId = request.AccountId,
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Dividends.Add(dividend);
        await _context.SaveChangesAsync();

        return dividend;
    }

    public async Task<Dividend> UpdateDividendAsync(string dividendId, UpdateDividendRequest request, string userId)
    {
        var dividend = await _context.Dividends
            .FirstOrDefaultAsync(d => d.Id == dividendId && d.UserId == userId);

        if (dividend == null)
        {
            throw new KeyNotFoundException("Dividend not found");
        }

        if (!string.IsNullOrEmpty(request.AccountId))
        {
            var account = await _context.Accounts
                .FirstOrDefaultAsync(a => a.Id == request.AccountId && a.UserId == userId);
            if (account == null)
            {
                throw new KeyNotFoundException("Account not found");
            }
            dividend.AccountId = request.AccountId;
        }

        if (!string.IsNullOrEmpty(request.Symbol)) dividend.Symbol = request.Symbol.ToUpperInvariant();
        if (request.Amount.HasValue) dividend.Amount = request.Amount.Value;
        if (request.Quantity.HasValue) dividend.Quantity = request.Quantity.Value;
        if (request.PerShareAmount.HasValue) dividend.PerShareAmount = request.PerShareAmount.Value;
        if (!string.IsNullOrEmpty(request.Type)) dividend.Type = request.Type;
        if (!string.IsNullOrEmpty(request.Currency)) dividend.Currency = request.Currency;
        if (request.PaymentDate.HasValue) dividend.PaymentDate = DateTime.SpecifyKind(request.PaymentDate.Value, DateTimeKind.Utc);
        if (request.ExDividendDate.HasValue) dividend.ExDividendDate = DateTime.SpecifyKind(request.ExDividendDate.Value, DateTimeKind.Utc);
        if (request.RecordDate.HasValue) dividend.RecordDate = DateTime.SpecifyKind(request.RecordDate.Value, DateTimeKind.Utc);
        if (request.Notes != null) dividend.Notes = request.Notes;
        if (request.TaxWithheld.HasValue) dividend.TaxWithheld = request.TaxWithheld.Value;

        dividend.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return dividend;
    }

    public async Task DeleteDividendAsync(string dividendId, string userId)
    {
        var dividend = await _context.Dividends
            .FirstOrDefaultAsync(d => d.Id == dividendId && d.UserId == userId);

        if (dividend == null)
        {
            throw new KeyNotFoundException("Dividend not found");
        }

        _context.Dividends.Remove(dividend);
        await _context.SaveChangesAsync();
    }

    public async Task<DividendSummary> GetDividendSummaryAsync(string userId, string? accountId = null)
    {
        var query = _context.Dividends.Where(d => d.UserId == userId);

        if (!string.IsNullOrEmpty(accountId))
        {
            query = query.Where(d => d.AccountId == accountId);
        }

        var dividends = await query.ToListAsync();

        var now = DateTime.UtcNow;
        var startOfYear = new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var startOfLastMonth = startOfMonth.AddMonths(-1);

        var summary = new DividendSummary
        {
            TotalDividends = dividends.Sum(d => d.Amount),
            TotalTaxWithheld = dividends.Sum(d => d.TaxWithheld),
            NetDividends = dividends.Sum(d => d.Amount - d.TaxWithheld),
            TotalPayments = dividends.Count,
            UniqueSymbols = dividends.Select(d => d.Symbol).Distinct().Count(),
            YtdDividends = dividends.Where(d => d.PaymentDate >= startOfYear).Sum(d => d.Amount),
            LastMonthDividends = dividends.Where(d => d.PaymentDate >= startOfLastMonth && d.PaymentDate < startOfMonth).Sum(d => d.Amount),
        };

        // Monthly breakdown (last 12 months)
        summary.MonthlyBreakdown = dividends
            .Where(d => d.PaymentDate >= now.AddMonths(-12))
            .GroupBy(d => new { d.PaymentDate.Year, d.PaymentDate.Month })
            .Select(g => new MonthlyDividend
            {
                Year = g.Key.Year,
                Month = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM"),
                Amount = g.Sum(d => d.Amount),
                PaymentCount = g.Count()
            })
            .OrderByDescending(m => m.Year)
            .ThenByDescending(m => DateTime.ParseExact(m.Month, "MMM", null).Month)
            .Take(12)
            .ToList();

        // Top symbols by dividend amount
        summary.TopSymbols = dividends
            .GroupBy(d => d.Symbol)
            .Select(g => new DividendBySymbol
            {
                Symbol = g.Key,
                TotalAmount = g.Sum(d => d.Amount),
                TotalTaxWithheld = g.Sum(d => d.TaxWithheld),
                NetAmount = g.Sum(d => d.Amount - d.TaxWithheld),
                PaymentCount = g.Count(),
                LastPaymentDate = g.Max(d => d.PaymentDate),
                LastPaymentAmount = g.OrderByDescending(d => d.PaymentDate).First().Amount
            })
            .OrderByDescending(s => s.TotalAmount)
            .Take(10)
            .ToList();

        return summary;
    }

    public async Task<IEnumerable<DividendBySymbol>> GetDividendsBySymbolAsync(string userId, string? accountId = null)
    {
        var query = _context.Dividends.Where(d => d.UserId == userId);

        if (!string.IsNullOrEmpty(accountId))
        {
            query = query.Where(d => d.AccountId == accountId);
        }

        var dividends = await query.ToListAsync();

        return dividends
            .GroupBy(d => d.Symbol)
            .Select(g => new DividendBySymbol
            {
                Symbol = g.Key,
                TotalAmount = g.Sum(d => d.Amount),
                TotalTaxWithheld = g.Sum(d => d.TaxWithheld),
                NetAmount = g.Sum(d => d.Amount - d.TaxWithheld),
                PaymentCount = g.Count(),
                LastPaymentDate = g.Max(d => d.PaymentDate),
                LastPaymentAmount = g.OrderByDescending(d => d.PaymentDate).First().Amount
            })
            .OrderByDescending(s => s.TotalAmount)
            .ToList();
    }
}
