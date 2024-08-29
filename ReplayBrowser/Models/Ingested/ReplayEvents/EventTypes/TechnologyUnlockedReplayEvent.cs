using ReplayBrowser.Data.Models;

namespace ReplayBrowser.Models.Ingested.ReplayEvents.EventTypes;

public class TechnologyUnlockedReplayEvent : ReplayDbEvent
{
    public string Name;

    public string Discipline;

    public int Tier;

    public ReplayEventPlayer Player;
}