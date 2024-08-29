namespace ReplayBrowser.Models.Ingested.ReplayEvents;

/// <summary>
/// Represents the type of event that occurred in a replay.
/// </summary>
public enum ReplayEventType
{
    #region Out of character events

    PlayerJoin,
    PlayerLeave,

    #endregion

    #region Gameflow

    GameRuleStarted,
    GameRuleEnded,
    RoundEnded,

    #endregion

    #region In character events

    CargoProductOrdered,
    CargoProductSold,

    MobStateChanged,

    MobSlipped,
    MobStunned,

    NukeArmed,
    NukeDetonated,
    NukeDefused,

    PowerEngineSpawned, // Tesla or Singularity
    ContainmentFieldDisengaged,

    ItemBoughtFromStore, // Item bought from an (for example) uplink.

    Explosion,

    AnnouncementSent, // Comms console
    ChatMessageSent,
    AlertLevelChanged,
    NewsArticlePublished,

    TechnologyUnlocked,

    EvacuationShuttleCalled,
    EvacuationShuttleDocked,
    EvacuationShuttleDockedCentCom,
    EvacuationShuttleDeparted,
    EvacuationShuttleRecalled,
    #endregion
}