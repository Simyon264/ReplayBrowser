namespace ReplayBrowser.Data.Models;

/// <summary>
/// Represent a notice that is displayed to every user if the condition is met.
/// </summary>
public class Notice
{
    public int? Id { get; set; }
    
    public required string Title { get; set; }
    public required string Message { get; set; }
    public required DateTime StartDate { get; set; }
    public required DateTime EndDate { get; set; }
}