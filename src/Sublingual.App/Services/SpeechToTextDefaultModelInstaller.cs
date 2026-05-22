using Sublingual.App.Models;

namespace Sublingual.App.Services;

public sealed class SpeechToTextDefaultModelInstaller(HttpClient httpClient, SpeechToTextModelImporter modelImporter)
{
    public async Task<string> InstallAsync(
        SpeechToTextDefaultModelSource source,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (string.IsNullOrWhiteSpace(source.ModelName))
        {
            throw new InvalidOperationException("Default speech model name is not configured.");
        }

        if (string.IsNullOrWhiteSpace(source.ZipUrl))
        {
            throw new InvalidOperationException($"Download URL for {source.ModelName} is not configured.");
        }

        if (!Uri.TryCreate(source.ZipUrl, UriKind.Absolute, out var zipUri))
        {
            throw new InvalidOperationException($"Download URL for {source.ModelName} is invalid.");
        }

        var tempRoot = Path.Combine(Path.GetTempPath(), "Sublingual", "speech-model-download", Guid.NewGuid().ToString("N"));
        var zipPath = Path.Combine(tempRoot, $"{source.ModelName}.zip");
        Directory.CreateDirectory(tempRoot);

        try
        {
            using var response = await httpClient.GetAsync(
                zipUri,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength;

            await using (var stream = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var fileStream = File.Create(zipPath))
            {
                await CopyToAsync(stream, fileStream, totalBytes, progress, cancellationToken);
            }

            progress?.Report(100);

            return modelImporter.ImportFromZip(zipPath, source.ModelName);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static async Task CopyToAsync(
        Stream source,
        Stream destination,
        long? totalBytes,
        IProgress<int>? progress,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[81_920];
        long totalRead = 0;
        var lastReported = -1;

        while (true)
        {
            var bytesRead = await source.ReadAsync(buffer, cancellationToken);
            if (bytesRead == 0)
            {
                break;
            }

            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            totalRead += bytesRead;

            if (totalBytes is not > 0)
            {
                continue;
            }

            var percent = (int)Math.Clamp((totalRead * 100L) / totalBytes.Value, 0, 100);
            if (percent == lastReported)
            {
                continue;
            }

            lastReported = percent;
            progress?.Report(percent);
        }
    }
}
