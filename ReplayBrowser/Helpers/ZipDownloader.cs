
using System.Diagnostics;
using System.Text;

namespace ReplayBrowser.Helpers;

public static class ZipDownloader
{
    public static async Task<Dictionary<string, Stream>> ExtractFilesFromZipAsync(string zipUrl, string[] filesToExtract)
    {
        // ok so i first tried doing this in c#, but then saw a python script "unzip_http" that does this, so now im just gonna call that

        var files = new Dictionary<string, Stream>();

        foreach (var file in filesToExtract)
        {
            var process = new Process();
            process.StartInfo.FileName = "python";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory + "Tools/unzip_http.py");
            process.StartInfo.Arguments = $"{path} -o {zipUrl} {file}";

            process.Start();

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                throw new Exception($"Failed to extract files from zip: {error}");
            }

            var stream = new MemoryStream(Encoding.UTF8.GetBytes(output));
            files.Add(file, stream);
        }

        return files;
    }
}