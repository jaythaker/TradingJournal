using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TradingJournal.Api.Models;

[Table("accounts")]
public class Account
{
    [Key]
    [Column("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Column("currency")]
    public string Currency { get; set; } = "USD";

    [Required]
    [Column("userId")]
    public string UserId { get; set; } = string.Empty;

    [Column("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("UserId")]
    public User User { get; set; } = null!;

    public ICollection<Trade> Trades { get; set; } = new List<Trade>();
    public ICollection<Portfolio> Portfolios { get; set; } = new List<Portfolio>();
}
