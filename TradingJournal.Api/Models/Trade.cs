using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TradingJournal.Api.Models;

/// <summary>
/// Instrument type - Stock or Option
/// </summary>
public enum InstrumentType
{
    Stock,
    Option
}

/// <summary>
/// Option type - Call or Put
/// </summary>
public enum OptionType
{
    Call,
    Put
}

/// <summary>
/// Spread type for options strategies
/// </summary>
public enum SpreadType
{
    Single,         // Single leg trade
    CreditSpread,   // Sell higher premium, buy lower (receive credit)
    DebitSpread,    // Buy higher premium, sell lower (pay debit)
    IronCondor,     // 4-leg neutral strategy
    Straddle,       // Same strike call + put
    Strangle,       // Different strike call + put
    Calendar,       // Same strike, different expiration
    Butterfly,      // 3-leg strategy
    Custom          // Other multi-leg strategies
}

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
    public string Type { get; set; } = string.Empty; // BUY, SELL, BUY_TO_OPEN, SELL_TO_OPEN, BUY_TO_CLOSE, SELL_TO_CLOSE

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

    // ========== Options-specific fields ==========
    
    /// <summary>
    /// Instrument type: Stock or Option
    /// </summary>
    [Column("instrumentType")]
    public InstrumentType InstrumentType { get; set; } = InstrumentType.Stock;

    /// <summary>
    /// Option type: Call or Put (null for stocks)
    /// </summary>
    [Column("optionType")]
    public OptionType? OptionType { get; set; }

    /// <summary>
    /// Strike price for options (null for stocks)
    /// </summary>
    [Column("strikePrice")]
    public double? StrikePrice { get; set; }

    /// <summary>
    /// Expiration date for options (null for stocks)
    /// </summary>
    [Column("expirationDate")]
    public DateTime? ExpirationDate { get; set; }

    /// <summary>
    /// Underlying symbol for options (e.g., "AAPL" for AAPL options)
    /// </summary>
    [Column("underlyingSymbol")]
    public string? UnderlyingSymbol { get; set; }

    /// <summary>
    /// Contract multiplier (typically 100 for stock options)
    /// </summary>
    [Column("contractMultiplier")]
    public int ContractMultiplier { get; set; } = 100;

    /// <summary>
    /// Spread type for options strategies
    /// </summary>
    [Column("spreadType")]
    public SpreadType SpreadType { get; set; } = SpreadType.Single;

    /// <summary>
    /// Groups legs of a spread together (null for single trades)
    /// </summary>
    [Column("spreadGroupId")]
    public string? SpreadGroupId { get; set; }

    /// <summary>
    /// Leg number within a spread (1, 2, 3, 4...)
    /// </summary>
    [Column("spreadLegNumber")]
    public int? SpreadLegNumber { get; set; }

    /// <summary>
    /// Opening trade or closing trade for options
    /// </summary>
    [Column("isOpeningTrade")]
    public bool? IsOpeningTrade { get; set; }

    // ========== End Options fields ==========

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

    // ========== Computed Properties ==========

    /// <summary>
    /// Gets the notional value of the trade (for options, includes multiplier)
    /// </summary>
    [NotMapped]
    public double NotionalValue => InstrumentType == InstrumentType.Option 
        ? Price * Quantity * ContractMultiplier 
        : Price * Quantity;

    /// <summary>
    /// Gets a formatted description of the option (e.g., "AAPL 150C 01/19/2026")
    /// </summary>
    [NotMapped]
    public string OptionDescription => InstrumentType == InstrumentType.Option && StrikePrice.HasValue && ExpirationDate.HasValue
        ? $"{UnderlyingSymbol ?? Symbol} {StrikePrice:F0}{(OptionType == Models.OptionType.Call ? "C" : "P")} {ExpirationDate:MM/dd/yyyy}"
        : Symbol;
}
