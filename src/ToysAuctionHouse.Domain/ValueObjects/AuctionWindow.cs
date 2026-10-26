namespace ToysAuctionHouse.Domain.ValueObjects;

/// <summary>
/// Encapsulates the auction time window and validates the schedule rules.
/// All timestamps are UTC.
/// </summary>
public readonly record struct AuctionWindow
{
    public DateTime StartTime { get; }
    public DateTime EndTime { get; }

    public AuctionWindow(DateTime startTime, DateTime endTime)
    {
        if (endTime <= startTime)
        {
            throw new ArgumentOutOfRangeException(nameof(endTime), "EndTime must be after StartTime.");
        }

        StartTime = startTime;
        EndTime = endTime;
    }

    public bool HasStarted(DateTime now) => now >= StartTime;

    public bool HasEnded(DateTime now) => now >= EndTime;

    public bool IsWithinSnipeWindow(DateTime now, int snipeWindowSeconds, bool antiSnipingEnabled)
    {
        if (!antiSnipingEnabled)
        {
            return false;
        }

        var remaining = EndTime - now;
        return remaining > TimeSpan.Zero && remaining <= TimeSpan.FromSeconds(snipeWindowSeconds);
    }
}
