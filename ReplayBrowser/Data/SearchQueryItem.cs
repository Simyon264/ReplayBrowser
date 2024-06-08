using System.Text.Json.Serialization;

namespace ReplayBrowser.Data;

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
                "Map" => Data.SearchMode.Map,
                "Gamemode" => Data.SearchMode.Gamemode,
                "Server id" => Data.SearchMode.ServerId,
                "Round end text" => Data.SearchMode.RoundEndText,
                "Player ic name" => Data.SearchMode.PlayerIcName,
                "Player ooc name" => Data.SearchMode.PlayerOocName,
                "Guid" => Data.SearchMode.Guid,
                "Server name" => Data.SearchMode.ServerName,
                "Round id" => Data.SearchMode.RoundId,
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }
}