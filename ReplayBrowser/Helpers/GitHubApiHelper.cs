using System.Net.Http;
using System.Net.Mime;
using System.Text.Json; // Add this for JSON deserialization
using Serilog;

namespace ReplayBrowser.Helpers;

public class GitHubApiHelper
{
    public List<GitHubAccount> Contributors;
    private readonly string? _apiToken;

    public GitHubApiHelper(IConfiguration configuration)
    {
        Contributors = new List<GitHubAccount>();
        
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
        try
        {
            using (var httpClient = new HttpClient())
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/repos/Simyon264/ReplayBrowser/contributors"))
                {
                    // lesson learned: don't forget the user agent
                    request.Headers.TryAddWithoutValidation("Accept", "application/vnd.github+json");
                    request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_apiToken}");
                    request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
                    request.Headers.TryAddWithoutValidation("User-Agent", "YourAppNameHere");

                    var response = await httpClient.SendAsync(request);
                    response.EnsureSuccessStatusCode();

                    var responseBody = await response.Content.ReadAsStringAsync();
                    
                    // Deserialize JSON into GitHubAccount list
                    var responseJson = JsonSerializer.Deserialize<List<JsonElement>>(responseBody);

                    foreach (var obj in responseJson)
                    {

                        // Initialize variables with default values
                        // Doing this to 100% make sure that respinse errors / null responses don't crash anything
                        string accountName = null;
                        string accountImageURL = null;
                        string accountLink = null;

                        // Safely retrieve properties
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

                        // Check for nulls before adding to the list
                        if (accountName != null && accountImageURL != null && accountLink != null)
                        {
                            var account = new GitHubAccount
                            {
                                AccountName = accountName,
                                AccountImageURL = accountImageURL,
                                AccountLink = accountLink
                            };

                            Contributors.Add(account);
                            Contributors.Add(account);
                            Contributors.Add(account);
                            Contributors.Add(account);
                        }
                        else
                        {
                            Log.Warning("A required property was missing in the JSON response while attempting to fetch project contributors.");
                        }
                    }

                    Log.Information("Successfully fetched project contributor list.");
                    return Contributors;
                }
            }
        }
        catch (Exception e)
        {
            Log.Error($"Exception when querying GitHub: {e.Message} - {e.StackTrace.ToString()}");
            
            // return empty list
            // TODO - Proper handling
            return new List<GitHubAccount>();
        }
    }
}

public struct GitHubAccount
{
    public string AccountName { get; set; }
    public string AccountImageURL { get; set; }
    public string AccountLink { get; set; }
}
