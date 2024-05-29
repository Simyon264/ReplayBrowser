namespace ReplayBrowser.Helpers;

public static class HttpExtensions
{
    public static async Task<Stream> GetStreamAsync(this HttpClient client, string requestUri, IProgress<double> progress, CancellationToken token)
    {
        var response = await client.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead, token);
        var contentLength = response.Content.Headers.ContentLength;
        var stream = await response.Content.ReadAsStreamAsync();
        var totalRead = 0L;
        var buffer = new byte[81920];
        var isMoreToRead = true;
        do
        {
            token.ThrowIfCancellationRequested();
            var read = await stream.ReadAsync(buffer, 0, buffer.Length, token);
            if (read == 0)
            {
                isMoreToRead = false;
            }
            else
            {
                totalRead += read;
                progress.Report(contentLength == null ? 0 : (double)totalRead / contentLength.Value);
            }
        } while (isMoreToRead);
        return stream;
    }
}