using System.Text.Json.Serialization;
using Microsoft.Extensions.Primitives;

namespace ReplayBrowser.Models;

public class SearchQueryItem
{
    [JsonPropertyName("searchMode")]
    public string SearchMode
    {
        set
        {
            if (!ModeMapping.TryGetValue(value.ToLower(), out var mapped))
                throw new ArgumentOutOfRangeException();
            SearchModeEnum = mapped;
        }
    }
    [JsonPropertyName("searchValue")]
    public required string SearchValue { get; set; }
    [JsonIgnore]
    public SearchMode SearchModeEnum { get; set; }

    public static List<SearchQueryItem> FromQuery(IQueryCollection query) {
        List<SearchQueryItem> result = [];

        foreach (var item in query)
        {
            result.AddRange(QueryValueParse(item.Key, item.Value));
        }

        var legacyQuery = query["searches"];
        if (legacyQuery.Count > 0 && legacyQuery[0]!.Length > 0)
            result.AddRange(FromQueryLegacy(legacyQuery[0]!));

        return result;
    }

    public static List<SearchQueryItem> FromQueryLegacy(string searchesParam) {
        var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(searchesParam));
        return System.Text.Json.JsonSerializer.Deserialize<List<SearchQueryItem>>(decoded)!;
    }

    public static List<SearchQueryItem> QueryValueParse(string key, StringValues values) {
        if (!ModeMapping.TryGetValue(key, out var type))
            return [];

        return values
            .Where(v => v is not null && v.Length > 0)
            .Select(v => new SearchQueryItem { SearchModeEnum = type, SearchValue = v! })
            .ToList();
    }

    // String values must be lowercase!
    // Be careful with changing any of the values here, as it can cause old searched to be invalid
    // For this reason, it's better to only add new entries
    static readonly Dictionary<string, SearchMode> ModeMapping = new() {
        { "guid", Models.SearchMode.Guid },
        { "username", Models.SearchMode.PlayerOocName },
        { "character", Models.SearchMode.PlayerIcName },
        { "server_id", Models.SearchMode.ServerId },
        { "server", Models.SearchMode.ServerName },
        { "round", Models.SearchMode.RoundId },
        { "map", Models.SearchMode.Map },
        { "gamemode", Models.SearchMode.Gamemode },
        { "endtext", Models.SearchMode.RoundEndText },
        // Legacy
        { "player ooc name", Models.SearchMode.PlayerOocName },
        { "player ic name", Models.SearchMode.PlayerIcName },
        { "server id", Models.SearchMode.ServerId },
        { "server name", Models.SearchMode.ServerName },
        { "round id", Models.SearchMode.RoundId },
        { "round end text", Models.SearchMode.RoundEndText },
    };
}