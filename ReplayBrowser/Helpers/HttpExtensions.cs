using System.IO.Compression;

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
        var response = await client.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead, token);
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