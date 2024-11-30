using System.ComponentModel.DataAnnotations;

namespace ReplayBrowser.Models;

public enum SearchMode
{
    [Display(Name = "Map")]
    Map,
    [Display(Name = "Gamemode")]
    Gamemode,
    [Display(Name = "Server ID")]
    ServerId,
    [Display(Name = "Round End Text")]
    RoundEndText,
    [Display(Name = "Player IC Name")]
    PlayerIcName,
    [Display(Name = "Player OOC Name")]
    PlayerOocName,
    [Display(Name = "Player GUID")]
    Guid,
    [Display(Name = "Server Name")]
    ServerName,
    [Display(Name = "Round ID")]
    RoundId
}
