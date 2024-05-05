using System.Diagnostics.Metrics;

namespace Server.Metrics;

public class ReplayMetrics
{
    /// <summary>
    /// Records the number of replays that have been parsed.
    /// </summary>
    private readonly Counter<int> _replayParsedCounter;
    /// <summary>
    /// Records the number of replays that have failed to parse.
    /// </summary>
    private readonly Counter<int> _replayParsedErrorCounter;
    
    public ReplayMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("ReplayBrowser");
        _replayParsedCounter = meter.CreateCounter<int>("replay_browser.replay.parsed", null, "The number of replays that have been parsed.");
        _replayParsedErrorCounter = meter.CreateCounter<int>("replay_browser.replay.error", null, "The number of replays that have failed to parse.");
    }
    
    public void ReplayParsed(string url)
    {
        _replayParsedCounter.Add(1);
    }
    
    public void ReplayError(string url)
    {
        _replayParsedErrorCounter.Add(1, new KeyValuePair<string, object?>("replay_browser.replay.url", url));
    }
}