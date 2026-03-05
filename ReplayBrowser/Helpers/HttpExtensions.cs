using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using Serilog;

namespace ReplayBrowser.Helpers;

public static class HttpExtensions
{
    /// <summary>
    /// Checks if the server supports
    /// </summary>
    public static async Task<bool> SupportsRangeRequests(this HttpClient client, string requestUri)
    {
        var request = new HttpRequestMessage(HttpMethod.Head, requestUri);
        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        return response.Headers.AcceptRanges.Contains("bytes");
    }

    /// <summary>
    /// Returns the size of the file in bytes
    /// </summary>
    public static async Task<long> GetFileSizeAsync(this HttpClient client, string requestUri)
    {
        var request = new HttpRequestMessage(HttpMethod.Head, requestUri);
        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        var contentLength = response.Content.Headers.ContentLength;
        return contentLength ?? -1;
    }

    public static async Task<Stream> GetStreamAsync(this HttpClient client, string requestUri, IProgress<double> progress, CancellationToken token)
    {
        const int maxAttempts = 50;
        var attempts = 0;

        var response = await client.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead, token);

        while (response.StatusCode == HttpStatusCode.TooManyRequests && attempts < maxAttempts)
        {
            attempts++;

            var retryAfter = response.Headers.RetryAfter;
            TimeSpan delay;

            if (retryAfter != null)
            {
                if (retryAfter.Delta.HasValue)
                {
                    delay = retryAfter.Delta.Value;
                }
                else if (retryAfter.Date.HasValue)
                {
                    delay = retryAfter.Date.Value - DateTimeOffset.UtcNow;
                    if (delay < TimeSpan.Zero)
                        delay = TimeSpan.FromSeconds(5);
                }
                else
                {
                    delay = TimeSpan.FromSeconds(Random.Shared.Next(5, 16));
                }
            }
            else
            {
                delay = TimeSpan.FromSeconds(Random.Shared.Next(5, 16));
            }

            Log.Warning("Hit ratelimit! {requestUri} retrying in {delay} (attempt {attempt}/{max})",
                requestUri, delay, attempts, maxAttempts);

            response.Dispose();
            await Task.Delay(delay, token);

            response = await client.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead, token);
        }

        response.EnsureSuccessStatusCode();
        var contentLength = response.Content.Headers.ContentLength;
        var httpStream = await response.Content.ReadAsStreamAsync(token);
        var memoryStream = new MemoryStream();
        var totalRead = 0L;
        var buffer = new byte[81920];

        int bytesRead;
        do
        {
            token.ThrowIfCancellationRequested();
            bytesRead = await httpStream.ReadAsync(buffer, 0, buffer.Length, token);
            if (bytesRead == 0)
            {
                progress.Report(1);
                break;
            }
            else
            {
                await memoryStream.WriteAsync(buffer, 0, bytesRead, token);
                totalRead += bytesRead;
                progress.Report(contentLength == null ? 0 : (double)totalRead / contentLength.Value);
            }
        } while (true);

        // If the download is completed, set the progress to 100%
        if (bytesRead == 0)
        {
            progress.Report(1);
        }

        memoryStream.Position = 0; // Reset the stream position to the beginning
        return memoryStream;
    }
}