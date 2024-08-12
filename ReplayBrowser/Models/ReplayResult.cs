using OpenTelemetry.Trace;
using ReplayBrowser.Data.Models;

namespace ReplayBrowser.Models;

public class ReplayResult
{
    public int Id { get; set; }
    public string? Link { get; set; }
    public string ServerId { get; set; } = null!;
    public string? ServerName { get; set; }
    public string Gamemode { get; set; } = null!;
    public string? Map { get; set; }
    public List<string>? Maps { get; set; }

    // These properties are only used by the details display
    public string Duration { get; set; } = null!;
    public DateTime? Date { get; set; }
    public int? RoundId { get; set; }
    public int Size { get; set; }
    public int UncompressedSize { get; set; }

    public bool IsFavorite { get; set; }
}