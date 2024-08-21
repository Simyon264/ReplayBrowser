﻿using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReplayBrowser.Services.ReplayParser.Providers;

[ReplayProviderName("caddy")]
public class CaddyProvider : ReplayProvider
{
    public override async Task RetrieveFilesRecursive(string directoryUrl, CancellationToken token)
    {
        var httpClient = GetHttpClient();
        httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

        var responseText = await httpClient.GetStringAsync(directoryUrl, token);
        var response = JsonSerializer.Deserialize<CaddyResponse[]>(responseText);
        if (response == null)
        {
            return;
        }

        foreach (var caddyResponse in response)
        {
            if (caddyResponse.Name.EndsWith(".zip", StringComparison.Ordinal))
            {
                if (caddyResponse.LastModified < ReplayParserService.CutOffDateTime)
                {
                    continue;
                }

                await ReplayParserService.AddReplayToQueue(directoryUrl + caddyResponse.Name);
            }
            else if (caddyResponse.IsDir)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(10), token);
                await RetrieveFilesRecursive(directoryUrl + caddyResponse.Name, token);
            }
        }
    }

    internal class CaddyResponse
    {
        [JsonPropertyName("name")]
        public required string Name { get; set; }
        [JsonPropertyName("size")]
        public int Size { get; set; }
        [JsonPropertyName("url")]
        public required string Url { get; set; }
        [JsonPropertyName("mod_time")]
        public DateTime LastModified { get; set; }
        [JsonPropertyName("mode")]
        public long Mode { get; set; }
        [JsonPropertyName("is_dir")]
        public bool IsDir { get; set; }
        [JsonPropertyName("is_symlink")]
        public bool IsSymlink { get; set; }
    }

    public CaddyProvider(ReplayParserService replayParserService) : base(replayParserService)
    {
    }
}