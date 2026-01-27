using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TradingJournal.Api.Models;

[Table("trades")]
public class Trade
{
    [Key]
    [Column("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [Column("symbol")]
    public string Symbol { get; set; } = string.Empty;

    [Required]
    [Column("type")]
    public string Type { get; set; } = string.Empty; // BUY, SELL

    [Required]
    [Column("quantity")]
    public double Quantity { get; set; }

    [Required]
    [Column("price")]
    public double Price { get; set; }

    [Column("fee")]
    public double Fee { get; set; } = 0;

    [Required]
    [Column("currency")]
    public string Currency { get; set; } = "USD";

    [Required]
    [Column("date")]
    public DateTime Date { get; set; }

    [Column("notes")]
    public string? Notes { get; set; }

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
