using ReplayBrowser.Data.Models;

namespace ReplayBrowser.Models.Ingested.ReplayEvents.EventTypes;

public class ReplayExplosionEvent : ReplayDbEvent
{
    public ReplayEventPlayer? Source;

    public float Intensity;

    public float Slope;

    public float MaxTileIntensity;

    public float TileBreakScale;

    public int MaxTileBreak;

    public bool CanCreateVacuum;

    public string Type;
}