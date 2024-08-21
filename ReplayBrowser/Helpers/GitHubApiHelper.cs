using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Serilog;

namespace ReplayBrowser.Helpers;

public class GitHubApiHelper
{
    private readonly IMemoryCache _memoryCache;
    private readonly string? _apiToken;

    public GitHubApiHelper(IConfiguration configuration, IMemoryCache memoryCache)
    {
        _memoryCache = memoryCache;

        try
        {
            _apiToken = configuration["GitHubAPIToken"];
        }
        catch (Exception)
        {
            Log.Error("GitHubAPIToken not set, contributors cannot be fetched.");
        }
    }

    public async Task<List<GitHubAccount>> GetContributors()
    {
        if (_memoryCache.TryGetValue("GitHubContributors", out List<GitHubAccount>? contributors))
            return contributors!;

        try
        {
            using var httpClient = new HttpClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/repos/Simyon264/ReplayBrowser/contributors");

            request.Headers.TryAddWithoutValidation("Accept", "application/vnd.github+json");
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_apiToken}");
            request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
            request.Headers.TryAddWithoutValidation("User-Agent", "YourAppNameHere");

            var response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            contributors = await response.Content.ReadFromJsonAsync<List<GitHubAccount>>();

            Log.Information("Successfully fetched project contributor list.");

            // Set cache options and cache the contributors list
            var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(30)); // Adjust expiration as needed

            _memoryCache.Set("GitHubContributors", contributors, cacheEntryOptions);
        }
        catch (Exception e)
        {
            Log.Error($"Exception when querying GitHub: {e.Message} - {e.StackTrace}");

            // return empty list
            return [];
        }

        return contributors!;
    }
}

public readonly struct GitHubAccount
{
    [JsonPropertyName("login")]
    public required string AccountName { get; init; }
    [JsonPropertyName("avatar_url")]
    public required string AccountImageUrl { get; init; }
    [JsonPropertyName("html_url")]
    public required string AccountLink { get; init; }
}
