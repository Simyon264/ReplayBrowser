namespace Shared;

/// <summary>
/// Supported ranges for date-based queries.
/// </summary>
public enum RangeOption
{
    /// <summary>
    /// The last 24 hours.
    /// </summary>
    Last24Hours = 0,
    
    /// <summary>
    /// The last 7 days.
    /// </summary>
    Last7Days = 1,
    
    /// <summary>
    /// The last 30 days.
    /// </summary>
    Last30Days = 2,
    
    /// <summary>
    /// The last three months.
    /// </summary>
    Last90Days = 3,
    
    /// <summary>
    /// The last year.
    /// </summary>
    Last365Days = 4,
    
    /// <summary>
    /// All time.
    /// </summary>
    AllTime = 5
}

public static class RangeOptionExtensions
{
    public static TimeSpan GetTimeSpan(this RangeOption rangeOption)
    {
        return rangeOption switch
        {
            RangeOption.Last24Hours => TimeSpan.FromHours(24),
            RangeOption.Last7Days => TimeSpan.FromDays(7),
            RangeOption.Last30Days => TimeSpan.FromDays(30),
            RangeOption.Last90Days => TimeSpan.FromDays(90),
            RangeOption.Last365Days => TimeSpan.FromDays(365),
            RangeOption.AllTime => TimeSpan.FromDays(365 * 100), // 100 years, should be enough
            _ => throw new ArgumentOutOfRangeException(nameof(rangeOption), rangeOption, null)
        };
    }
}