using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using ReplayBrowser.Data.Models;
using ReplayBrowser.Models;
using Serilog;

namespace ReplayBrowser.Helpers;

public class Ss14ApiHelper
{
    private readonly IMemoryCache _cache;

    public Ss14ApiHelper(IMemoryCache cache)
    {
        _cache = cache;
    }

    public async Task<PlayerData?> FetchPlayerDataFromUsername(string username)
    {
        HttpResponseMessage response = null;
        try
        {
            var httpClient = new HttpClient();
            response = await httpClient.GetAsync($"https://central.spacestation14.io/auth/api/query/name?name={username}");
            response.EnsureSuccessStatusCode();
            var responseString = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<UsernameResponse>(responseString);
            return new PlayerData()
            {
                PlayerGuid = Guid.Parse(data.userId),
                Username = data.userName
            };
        }
        catch (Exception e)
        {
            Log.Error("Unable to fetch GUID for player with username {Username}: {Error}", username, e.Message);
            if (e.Message.Contains("'<' is an")) // This is a hacky way to check if we got sent a website.
            {
                // Probably got sent a website? Log full response.
                Log.Error("Website might have been sent: {Response}", response?.Content.ReadAsStringAsync().Result);
            }

            return null;
        }
    }

    public async Task<PlayerData?> FetchPlayerDataFromGuid(Guid guid)
    {
        if (_cache.TryGetValue(guid.ToString(), out PlayerData? playerKey))
            return playerKey;

        playerKey = new PlayerData()
        {
            PlayerGuid = guid
        };

        HttpResponseMessage response = null;
        try
        {
            var httpClient = new HttpClient();
            response = await httpClient.GetAsync($"https://central.spacestation14.io/auth/api/query/userid?userid={playerKey.PlayerGuid}");
            response.EnsureSuccessStatusCode();
            var responseString = await response.Content.ReadAsStringAsync();
            var username = JsonSerializer.Deserialize<UsernameResponse>(responseString).userName;
            playerKey.Username = username;
        }
        catch (Exception e)
        {
            Log.Error("Unable to fetch username for player with GUID {PlayerGuid}: {Error}", playerKey.PlayerGuid, e.Message);
            if (e.Message.Contains("'<' is an")) // This is a hacky way to check if we got sent a website.
            {
                // Probably got sent a website? Log full response.
                Log.Error("Website might have been sent: {Response}", response?.Content.ReadAsStringAsync().Result);
            }

            playerKey.Username = "Unable to fetch username (API error)";
        }

        var cacheEntryOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(TimeSpan.FromMinutes(60));

        _cache.Set(guid.ToString(), playerKey, cacheEntryOptions);

        return playerKey;
    }
}