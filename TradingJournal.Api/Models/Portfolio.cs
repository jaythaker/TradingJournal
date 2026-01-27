using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TradingJournal.Api.Models;

[Table("portfolios")]
public class Portfolio
{
    [Key]
    [Column("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [Column("symbol")]
    public string Symbol { get; set; } = string.Empty;

    [Required]
    [Column("quantity")]
    public double Quantity { get; set; }

    [Required]
    [Column("averagePrice")]
    public double AveragePrice { get; set; }

    [Column("currentPrice")]
    public double? CurrentPrice { get; set; }

    [Required]
    [Column("userId")]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [Column("accountId")]
    public string AccountId { get; set; } = string.Empty;

    [Column("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("AccountId")]
    public Account Account { get; set; } = null!;

    [ForeignKey("UserId")]
    public User User { get; set; } = null!;
}
