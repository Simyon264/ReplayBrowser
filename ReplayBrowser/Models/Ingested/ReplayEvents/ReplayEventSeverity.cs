namespace ReplayBrowser.Models.Ingested.ReplayEvents;

public enum ReplayEventSeverity
{
    /// <summary>
    /// Low relevance, such as a player joining or leaving
    /// </summary>
    Low,

    /// <summary>
    /// Medium relevance, such as a chat message
    /// </summary>
    Medium,

    /// <summary>
    /// High relevance, so an announcment or a shuttle call
    /// </summary>
    High,

    /// <summary>
    /// Severe relevance, such as the station nuke being armed
    /// </summary>
    Critical
}