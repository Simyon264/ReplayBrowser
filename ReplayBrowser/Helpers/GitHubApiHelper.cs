using System.Net.Http;
using System.Text.Json;
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
        catch (Exception e)
        {
            Log.Error("GitHubAPIToken not set, contributors cannot be fetched.");
        }
    }

    public async Task<List<GitHubAccount>> GetContributors()
    {
        if (!_memoryCache.TryGetValue("GitHubContributors", out List<GitHubAccount> contributors))
        {
            try
            {
                using (var httpClient = new HttpClient())
                {
                    using (var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/repos/Simyon264/ReplayBrowser/contributors"))
                    {
                        request.Headers.TryAddWithoutValidation("Accept", "application/vnd.github+json");
                        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_apiToken}");
                        request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
                        request.Headers.TryAddWithoutValidation("User-Agent", "YourAppNameHere");

                        var response = await httpClient.SendAsync(request);
                        response.EnsureSuccessStatusCode();

                        var responseBody = await response.Content.ReadAsStringAsync();

                        var responseJson = JsonSerializer.Deserialize<List<JsonElement>>(responseBody);
                        contributors = new List<GitHubAccount>();

                        foreach (var obj in responseJson)
                        {
                            string accountName = null;
                            string accountImageURL = null;
                            string accountLink = null;

                            if (obj.TryGetProperty("login", out var loginProperty))
                            {
                                accountName = loginProperty.GetString();
                            }

                            if (obj.TryGetProperty("avatar_url", out var avatarUrlProperty))
                            {
                                accountImageURL = avatarUrlProperty.GetString();
                            }

                            if (obj.TryGetProperty("html_url", out var htmlUrlProperty))
                            {
                                accountLink = htmlUrlProperty.GetString();
                            }

                            if (accountName != null && accountImageURL != null && accountLink != null)
                            {
                                var account = new GitHubAccount
                                {
                                    AccountName = accountName,
                                    AccountImageUrl = accountImageURL,
                                    AccountLink = accountLink
                                };

                                contributors.Add(account);
                            }
                            else
                            {
                                Log.Warning("A required property was missing in the JSON response while attempting to fetch project contributors.");
                            }
                        }

                        Log.Information("Successfully fetched project contributor list.");

                        // Set cache options and cache the contributors list
                        var cacheEntryOptions = new MemoryCacheEntryOptions()
                            .SetAbsoluteExpiration(TimeSpan.FromMinutes(30)); // Adjust expiration as needed

                        _memoryCache.Set("GitHubContributors", contributors, cacheEntryOptions);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error($"Exception when querying GitHub: {e.Message} - {e.StackTrace}");

                // return empty list
                return new List<GitHubAccount>();
            }
        }

        return contributors;
    }
}

public struct GitHubAccount
{
    public string AccountName { get; init; }
    public string AccountImageUrl { get; init; }
    public string AccountLink { get; init; }
}
