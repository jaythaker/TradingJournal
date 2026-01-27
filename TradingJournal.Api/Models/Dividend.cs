using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TradingJournal.Api.Models;

[Table("dividends")]
public class Dividend
{
    [Key]
    [Column("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [Column("symbol")]
    public string Symbol { get; set; } = string.Empty;

    [Required]
    [Column("amount")]
    public double Amount { get; set; }

    [Column("quantity")]
    public double? Quantity { get; set; } // Number of shares at dividend time

    [Column("perShareAmount")]
    public double? PerShareAmount { get; set; } // Dividend per share

    [Required]
    [Column("type")]
    public string Type { get; set; } = "CASH"; // CASH, REINVESTED, QUALIFIED, NON_QUALIFIED

    [Required]
    [Column("currency")]
    public string Currency { get; set; } = "USD";

    [Required]
    [Column("paymentDate")]
    public DateTime PaymentDate { get; set; }

    [Column("exDividendDate")]
    public DateTime? ExDividendDate { get; set; }

    [Column("recordDate")]
    public DateTime? RecordDate { get; set; }

    [Column("notes")]
    public string? Notes { get; set; }

    [Column("taxWithheld")]
    public double TaxWithheld { get; set; } = 0;

    [Required]
    [Column("accountId")]
    public string AccountId { get; set; } = string.Empty;

    [Required]
    [Column("userId")]
    public string UserId { get; set; } = string.Empty;

    [Column("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("AccountId")]
    public Account Account { get; set; } = null!;

    [ForeignKey("UserId")]
    public User User { get; set; } = null!;
}
