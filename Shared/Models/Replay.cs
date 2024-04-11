using System.Text.Json.Serialization;
using NpgsqlTypes;
using YamlDotNet.Serialization;

namespace Shared.Models;

public class Replay
{
    public int Id { get; set; }
    
    public string? Link { get; set; }
    
    [YamlMember(Alias = "roundId")]
    public int? RoundId { get; set; }
    
    [YamlMember(Alias = "server_name")]
    public string? ServerName { get; set; }
    public DateTime? Date { get; set; }
    
    [YamlMember(Alias = "map")]
    public string Map { get; set; }
    [YamlMember(Alias = "gamemode")]
    public string Gamemode { get; set; }
    [YamlMember(Alias = "roundEndPlayers")]
    public List<Player>? RoundEndPlayers { get; set; }
    [YamlMember(Alias = "roundEndText")]
    public string? RoundEndText { get; set; }
    [YamlMember(Alias = "server_id")]
    public string ServerId { get; set; }
    [YamlMember(Alias = "endTick")]
    public int EndTick { get; set; }
    [YamlMember(Alias = "duration")]
    public string Duration { get; set; }
    [YamlMember(Alias = "fileCount")]
    public int FileCount { get; set; }
    [YamlMember(Alias = "size")]
    public int Size { get; set; }
    [YamlMember(Alias = "uncompressedSize")]
    public int UncompressedSize { get; set; }
    [YamlMember(Alias = "endTime")]
    public string EndTime { get; set; }
    
    [JsonIgnore]
    public NpgsqlTsVector RoundEndTextSearchVector { get; set; }
}