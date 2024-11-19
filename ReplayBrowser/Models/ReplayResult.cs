using OpenTelemetry.Trace;
using ReplayBrowser.Data.Models;

namespace ReplayBrowser.Models;

public class ReplayResult
{
    public int Id { get; set; }
    public required string Link { get; set; }
    public required string ServerId { get; set; }
    public string? ServerName { get; set; }
    public required string Gamemode { get; set; }
    public string? Map { get; set; }
    public List<string>? Maps { get; set; }

    // These properties are only used by the details display
    public required string Duration { get; set; }
    public DateTime? Date { get; set; }
    public int? RoundId { get; set; }
    public long Size { get; set; }
    public long UncompressedSize { get; set; }

    public bool IsFavorite { get; set; }
}