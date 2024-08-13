using System.Text.Json.Serialization;

namespace ReplayBrowser.Models;

public class SearchQueryItem
{
    [JsonPropertyName("searchMode")]
    public required string SearchMode { get; set; }
    [JsonPropertyName("searchValue")]
    public required string SearchValue { get; set; }

    public SearchMode SearchModeEnum
    {
        get
        {
            return SearchMode switch
            {
                "Map" => Models.SearchMode.Map,
                "Gamemode" => Models.SearchMode.Gamemode,
                "Server id" => Models.SearchMode.ServerId,
                "Round end text" => Models.SearchMode.RoundEndText,
                "Player ic name" => Models.SearchMode.PlayerIcName,
                "Player ooc name" => Models.SearchMode.PlayerOocName,
                "Guid" => Models.SearchMode.Guid,
                "Server name" => Models.SearchMode.ServerName,
                "Round id" => Models.SearchMode.RoundId,
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }
}