using HtmlAgilityPack;
using Serilog;

namespace ReplayBrowser.Services.ReplayParser.Providers;

[ReplayProviderName("nginx")]
public class NginxProvider : ReplayProvider
{
    public override async Task RetrieveFilesRecursive(string directoryUrl, CancellationToken token)
    {
        var client = GetHttpClient();
        var htmlContent = await client.GetStringAsync(directoryUrl, token);
        var document = new HtmlDocument();
        document.LoadHtml(htmlContent);

        var links = document.DocumentNode.SelectNodes("//a[@href]");
        if (links == null)
        {
            Log.Warning("No links found on " + directoryUrl + ".");
            return;
        }

        foreach (var link in links)
        {
            if (token.IsCancellationRequested)
            {
                return;
            }

            var href = link.Attributes["href"].Value;

            if (href.StartsWith("..", StringComparison.Ordinal))
            {
                continue;
            }

            if (!Uri.TryCreate(href, UriKind.Absolute, out _))
            {
                href = new Uri(new Uri(directoryUrl), href).ToString();
            }

            if (href.EndsWith("/", StringComparison.Ordinal))
            {
                await RetrieveFilesRecursive(href, token);
            }

            if (href.EndsWith(".zip", StringComparison.Ordinal))
            {
                await ReplayParserService.AddReplayToQueue(href);
            }
        }
    }

    public NginxProvider(ReplayParserService replayParserService) : base(replayParserService)
    {
    }
}