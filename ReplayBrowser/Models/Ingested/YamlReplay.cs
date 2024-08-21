using YamlDotNet.Serialization;

namespace ReplayBrowser.Models.Ingested;

public class YamlReplay {
    public int? RoundId { get; set; }

    [YamlMember(Alias = "server_id", ApplyNamingConventions = false)]
    public required string ServerId { get; set; }
    [YamlMember(Alias = "server_name", ApplyNamingConventions = false)]
    public string? ServerName { get; set; }

    public required string Gamemode { get; set; }
    public string? Map { get; set; }
    public List<string>? Maps { get; set; }

    public List<YamlPlayer>? RoundEndPlayers { get; set; }
    public string? RoundEndText { get; set; }

    public int EndTick { get; set; }
    public required string Duration { get; set; }
    public int FileCount { get; set; }
    public int Size { get; set; }
    public int UncompressedSize { get; set; }
    public required string EndTime { get; set; }
}