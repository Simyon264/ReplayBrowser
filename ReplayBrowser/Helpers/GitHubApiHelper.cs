using System.Net.Mime;
using System.Text;
using Serilog;

namespace ReplayBrowser.Helpers;

public class GitHubApiHelper
{
    public GitHubAccount[] Contributors;
    private string APIToken;

    public GitHubApiHelper(IConfiguration configuration)
    {
        try
        {
            APIToken = configuration.GetSection("GitHubAPIToken").ToString();
        }
        catch (Exception e)
        {
            Log.Error("GitHubAPIToken not set, contributors cannot be fetched.");
        }
    }

    public async Task<GitHubAccount[]> GetContributors()
    {
        try
        {
            /*
            var client = new HttpClient();

            client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {APIToken}");
            client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
            
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri("https://api.github.com/repos/Simyon264/ReplayBrowser/contributors")
            };
            */
            
            using (var httpClient = new HttpClient())
            {
                using (var request = new HttpRequestMessage(new HttpMethod("GET"), "https://api.github.com/repos/Simyon264/ReplayBrowser/contributors"))
                {
                    request.Headers.TryAddWithoutValidation("Accept", "application/vnd.github+json");
                    request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {APIToken}");
                    request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28"); 

                    var response = await httpClient.SendAsync(request);
                    
                    Log.Debug(response.ToString());
                    response.EnsureSuccessStatusCode();

                    var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    Log.Information(responseBody);

                    return []; // where is my debug
                }
            }
            
            //var response = await client.SendAsync(request).ConfigureAwait(false);
            
        }
        catch (Exception e)
        {
            Log.Information($"Exception when querying GitHub: {e.Message}");
            return [];
        }
    }
}

public struct GitHubAccount
{
    private string AccountName;
    private string AccountImageURL;
    private string AccountLink;
}