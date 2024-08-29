namespace ReplayBrowser.Models.Ingested.ReplayEvents;

public enum MobState : byte
{
    Invalid = 0,
    Alive = 1,
    Critical = 2,
    Dead = 3,
}