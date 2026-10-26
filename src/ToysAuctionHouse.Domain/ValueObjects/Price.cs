namespace ToysAuctionHouse.Domain.ValueObjects;

/// <summary>
/// Wraps a monetary price and validates auction step rules. Money is always a
/// non-negative decimal with at most 2 decimal places (SQL DECIMAL(18,2)).
/// </summary>
public readonly record struct Price
{
    public decimal Amount { get; }

    public Price(decimal amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Price cannot be negative.");
        }

        if (decimal.Round(amount, 2) != amount)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Price can have at most 2 decimal places.");
        }

        Amount = amount;
    }

    public static implicit operator decimal(Price price) => price.Amount;

    public override string ToString() => Amount.ToString("0.00");

    /// <summary>
    /// True when a proposed bid is exactly the current price plus at least one
    /// whole step (mirrors the Lua script amount/step validation).
    /// </summary>
    public bool IsValidBid(decimal bidAmount, decimal stepPrice, decimal currentPrice)
    {
        if (stepPrice <= 0 || bidAmount <= 0)
        {
            return false;
        }

        return bidAmount >= currentPrice + stepPrice;
    }
}
