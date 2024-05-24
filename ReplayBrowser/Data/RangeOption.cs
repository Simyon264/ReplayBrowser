namespace ReplayBrowser.Data;

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
    /// <summary>
    /// Converts a <see cref="RangeOption"/> to a string for use in SQL queries.
    /// </summary>
    public static string GetTimeSpan(this RangeOption rangeOption)
    {
        return rangeOption switch
        {
            RangeOption.Last24Hours => "1 day",
            RangeOption.Last7Days => "7 days",
            RangeOption.Last30Days => "30 days",
            RangeOption.Last90Days => "90 days",
            RangeOption.Last365Days => "365 days",
            RangeOption.AllTime => "100 years", // 100 years, should be enough
            _ => throw new ArgumentOutOfRangeException(nameof(rangeOption), rangeOption, null)
        };
    }
}