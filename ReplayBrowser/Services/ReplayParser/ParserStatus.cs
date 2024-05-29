namespace ReplayBrowser.Services.ReplayParser;

public enum ParserStatus
{
    Off, // Service is stopped or has not started yet
    Idle, // Waiting for next run
    Discovering, // Discovering new replays by scraping remote websites
    Downloading, // Downloading replays
}

public static class ParserStatusExtensions
{
    public static string ToFriendlyString(this ParserStatus status)
    {
        return status switch
        {
            ParserStatus.Off => "Off",
            ParserStatus.Idle => "Idle",
            ParserStatus.Discovering => "Discovering",
            ParserStatus.Downloading => "Downloading",
            _ => "Unknown"
        };
    }
}