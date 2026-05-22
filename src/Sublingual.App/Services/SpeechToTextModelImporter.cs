using System.IO.Compression;

namespace Sublingual.App.Services;

public sealed class SpeechToTextModelImporter(SpeechToTextModelCatalog modelCatalog)
{
    public string ImportFromDirectory(string sourceDirectoryPath, string? modelNameOverride = null)
    {
        if (string.IsNullOrWhiteSpace(sourceDirectoryPath))
        {
            throw new ArgumentException("Model directory path must not be empty.", nameof(sourceDirectoryPath));
        }

        if (!Directory.Exists(sourceDirectoryPath))
        {
            throw new DirectoryNotFoundException($"Model directory not found: {sourceDirectoryPath}");
        }

        var sourceDirectory = new DirectoryInfo(sourceDirectoryPath);
        if (!LooksLikeVoskModelDirectory(sourceDirectory.FullName))
        {
            throw new InvalidOperationException(
                "Selected folder does not look like a Vosk model. Expected files such as mfcc.conf, final.mdl, or a conf/ directory.");
        }

        var modelName = SanitizeModelName(string.IsNullOrWhiteSpace(modelNameOverride) ? sourceDirectory.Name : modelNameOverride);
        if (string.IsNullOrWhiteSpace(modelName))
        {
            throw new InvalidOperationException("Could not derive a valid model name from the selected folder.");
        }

        var destinationRoot = modelCatalog.GetManagedModelsRoot();
        var destinationDirectory = Path.Combine(destinationRoot, modelName);

        if (Directory.Exists(destinationDirectory))
        {
            Directory.Delete(destinationDirectory, recursive: true);
        }

        CopyDirectory(sourceDirectory.FullName, destinationDirectory);
        return destinationDirectory;
    }

    public string ImportFromZip(string zipFilePath, string? modelNameOverride = null)
    {
        if (string.IsNullOrWhiteSpace(zipFilePath))
        {
            throw new ArgumentException("Zip file path must not be empty.", nameof(zipFilePath));
        }

        if (!File.Exists(zipFilePath))
        {
            throw new FileNotFoundException($"Zip file not found: {zipFilePath}", zipFilePath);
        }

        var extractionRoot = Path.Combine(Path.GetTempPath(), "Sublingual", "speech-model-import", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(extractionRoot);

        try
        {
            ZipFile.ExtractToDirectory(zipFilePath, extractionRoot);
            var extractedModelDirectory = FindModelDirectory(extractionRoot)
                ?? throw new InvalidOperationException("The zip file does not contain a recognizable Vosk model directory.");

            return ImportFromDirectory(extractedModelDirectory, modelNameOverride);
        }
        finally
        {
            if (Directory.Exists(extractionRoot))
            {
                Directory.Delete(extractionRoot, recursive: true);
            }
        }
    }

    private static bool LooksLikeVoskModelDirectory(string directoryPath)
    {
        return File.Exists(Path.Combine(directoryPath, "mfcc.conf"))
            || File.Exists(Path.Combine(directoryPath, "final.mdl"))
            || Directory.Exists(Path.Combine(directoryPath, "conf"))
            || Directory.Exists(Path.Combine(directoryPath, "am"));
    }

    private static string? FindModelDirectory(string rootDirectory)
    {
        if (LooksLikeVoskModelDirectory(rootDirectory))
        {
            return rootDirectory;
        }

        foreach (var directory in Directory.EnumerateDirectories(rootDirectory, "*", SearchOption.AllDirectories))
        {
            if (LooksLikeVoskModelDirectory(directory))
            {
                return directory;
            }
        }

        return null;
    }

    private static string SanitizeModelName(string modelName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitizedChars = modelName
            .Trim()
            .Select(ch => invalid.Contains(ch) ? '-' : ch)
            .ToArray();

        return new string(sanitizedChars).Trim();
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (var filePath in Directory.GetFiles(sourceDirectory))
        {
            var destinationFilePath = Path.Combine(destinationDirectory, Path.GetFileName(filePath));
            File.Copy(filePath, destinationFilePath, overwrite: true);
        }

        foreach (var subdirectoryPath in Directory.GetDirectories(sourceDirectory))
        {
            var destinationSubdirectoryPath = Path.Combine(destinationDirectory, Path.GetFileName(subdirectoryPath));
            CopyDirectory(subdirectoryPath, destinationSubdirectoryPath);
        }
    }
}
