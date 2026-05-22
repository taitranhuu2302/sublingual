using Sublingual.App.Models;

namespace Sublingual.App.Services;

public sealed class SpeechToTextDefaultModelInstaller(HttpClient httpClient, SpeechToTextModelImporter modelImporter)
{
    public async Task<string> InstallAsync(SpeechToTextDefaultModelSource source, CancellationToken cancellationToken = default)
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
            using var response = await httpClient.GetAsync(zipUri, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using (var stream = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var fileStream = File.Create(zipPath))
            {
                await stream.CopyToAsync(fileStream, cancellationToken);
            }

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
}
