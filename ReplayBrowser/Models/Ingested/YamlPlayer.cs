using YamlDotNet.Serialization;

namespace ReplayBrowser.Models.Ingested;

public class YamlPlayer
{
    public required  List<string> AntagPrototypes { get; set; }
    public required  List<string> JobPrototypes { get; set; }
    public Guid PlayerGuid { get; set; }
    [YamlMember(Alias = "playerICName")]
    public required  string PlayerIcName { get; set; }
    [YamlMember(Alias = "playerOOCName")]
    public required string PlayerOocName { get; set; }
    public bool Antag { get; set; }
}