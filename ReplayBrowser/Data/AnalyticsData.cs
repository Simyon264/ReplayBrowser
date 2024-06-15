namespace ReplayBrowser.Data;

/// <summary>
/// Contains data for the analytics page.
/// </summary>
public class AnalyticsData
{
    public required List<Analytics> Analytics { get; set; }
}

/// <summary>
/// Contains the chart.js data for the analytics page.
/// </summary>
public class Analytics
{
    public required string Name { get; set; }
    public required string Description { get; set; }
    
    /// <summary>
    /// Did something go wrong when generating the chart?
    /// </summary>
    public string? Error { get; set; }
    
    /// <summary>
    /// The type of chart to display.
    /// </summary>
    public required string Type { get; set; }
    
    /// <summary>
    /// The data for the chart.
    /// </summary>
    public required List<ChartData> Data { get; set; }
}

/// <summary>
/// Contains the data for a chart.js chart.
/// </summary>
public class ChartData
{
    public required string Label { get; set; }
    public required double Data { get; set; }
}