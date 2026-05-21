using System.Text.Json;
using Sublingual.App.Models;

namespace Sublingual.App.Services;

public sealed class CaptureSessionStorage
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly AppSettingsStore _settingsStore;

    public CaptureSessionStorage(AppSettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
    }

    public string CreateSessionOutputPath(string? sessionTitle = null, string? sessionTreePath = null)
    {
        var sessionsRoot = GetSessionsRoot();
        Directory.CreateDirectory(sessionsRoot);

        var sanitizedTreePath = SanitizeTreePath(sessionTreePath);
        var baseDirectory = string.IsNullOrWhiteSpace(sanitizedTreePath)
            ? sessionsRoot
            : Path.Combine(sessionsRoot, sanitizedTreePath);
        Directory.CreateDirectory(baseDirectory);

        var titleSlug = SlugifySessionTitle(sessionTitle);
        var sessionId = string.IsNullOrWhiteSpace(titleSlug)
            ? $"session-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}"
            : $"{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{titleSlug}-{Guid.NewGuid():N}";
        var sessionDirectory = Path.Combine(baseDirectory, sessionId);
        Directory.CreateDirectory(sessionDirectory);

        return Path.Combine(sessionDirectory, "audio.wav");
    }

    public string GetSessionsRoot()
    {
        var settings = _settingsStore.Load();
        return AppPathHelper.ResolveConfiguredPath(settings.Storage.SessionsRoot, "sessions");
    }

    public IReadOnlyList<CaptureSessionRecord> GetSessions()
    {
        var sessionsRoot = GetSessionsRoot();
        if (!Directory.Exists(sessionsRoot))
        {
            return [];
        }

        return Directory
            .EnumerateFiles(sessionsRoot, "session.json", SearchOption.AllDirectories)
            .Select(Path.GetDirectoryName)
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path!)
            .Select(path =>
            {
                var directoryInfo = new DirectoryInfo(path);
                var metadataPath = Path.Combine(path, "session.json");
                var metadata = GetSessionMetadata(metadataPath);
                return new CaptureSessionRecord
                {
                    SessionId = Path.GetFileName(path),
                    Title = metadata?.Title ?? string.Empty,
                    TreePath = metadata?.TreePath ?? GetRelativeTreePath(sessionsRoot, path),
                    DirectoryPath = path,
                    AudioPath = Path.Combine(path, "audio.wav"),
                    TranscriptPath = Path.Combine(path, "transcript.json"),
                    MetadataPath = metadataPath,
                    CreatedAt = directoryInfo.CreationTimeUtc,
                };
            })
            .OrderByDescending(session => session.CreatedAt)
            .ToList();
    }

    public int DeleteSessions(IEnumerable<string> sessionDirectoryPaths)
    {
        var deleted = 0;
        foreach (var path in sessionDirectoryPaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(path))
            {
                continue;
            }

            Directory.Delete(path, true);
            deleted += 1;
        }

        return deleted;
    }

    public int ClearAllSessions()
    {
        return DeleteSessions(GetSessions().Select(session => session.DirectoryPath));
    }

    public void SaveTranscriptEntry(string outputAudioPath, SavedTranscriptEntry entry)
    {
        var sessionDirectory = Path.GetDirectoryName(outputAudioPath);
        if (string.IsNullOrWhiteSpace(sessionDirectory))
        {
            return;
        }

        Directory.CreateDirectory(sessionDirectory);

        var transcriptPath = Path.Combine(sessionDirectory, "transcript.json");
        var existingEntries = LoadTranscriptEntries(transcriptPath).ToList();

        var last = existingEntries.LastOrDefault();
        var isDuplicate = last is not null
            && string.Equals(last.PartialText, entry.PartialText, StringComparison.Ordinal)
            && string.Equals(last.PartialTranslatedText, entry.PartialTranslatedText, StringComparison.Ordinal)
            && string.Equals(last.FinalText, entry.FinalText, StringComparison.Ordinal)
            && string.Equals(last.FinalTranslatedText, entry.FinalTranslatedText, StringComparison.Ordinal);

        if (isDuplicate)
        {
            return;
        }

        existingEntries.Add(entry);

        var json = JsonSerializer.Serialize(existingEntries, SerializerOptions);
        File.WriteAllText(transcriptPath, json);
    }

    public void SaveSessionMetadata(string outputAudioPath, CaptureSessionMetadata metadata)
    {
        var sessionDirectory = Path.GetDirectoryName(outputAudioPath);
        if (string.IsNullOrWhiteSpace(sessionDirectory))
        {
            return;
        }

        Directory.CreateDirectory(sessionDirectory);
        var metadataPath = Path.Combine(sessionDirectory, "session.json");
        var json = JsonSerializer.Serialize(metadata, SerializerOptions);
        File.WriteAllText(metadataPath, json);
    }

    public CaptureSessionMetadata? GetSessionMetadata(string metadataPath)
    {
        if (!File.Exists(metadataPath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(metadataPath);
            return JsonSerializer.Deserialize<CaptureSessionMetadata>(json, SerializerOptions);
        }
        catch
        {
            return null;
        }
    }

    public IReadOnlyList<SavedTranscriptEntry> GetTranscriptEntries(string transcriptPath)
    {
        return LoadTranscriptEntries(transcriptPath);
    }

    private static IReadOnlyList<SavedTranscriptEntry> LoadTranscriptEntries(string transcriptPath)
    {
        if (!File.Exists(transcriptPath))
        {
            return [];
        }

        try
        {
            var json = File.ReadAllText(transcriptPath);
            return JsonSerializer.Deserialize<List<SavedTranscriptEntry>>(json, SerializerOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static string SlugifySessionTitle(string? sessionTitle)
    {
        if (string.IsNullOrWhiteSpace(sessionTitle))
        {
            return string.Empty;
        }

        var chars = sessionTitle
            .Trim()
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray();

        var slug = new string(chars);
        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }

        return slug.Trim('-');
    }

    private static string SanitizeTreePath(string? treePath)
    {
        if (string.IsNullOrWhiteSpace(treePath))
        {
            return string.Empty;
        }

        var segments = treePath
            .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(SlugifySessionTitle)
            .Where(static segment => !string.IsNullOrWhiteSpace(segment))
            .ToArray();

        return Path.Combine(segments);
    }

    private static string GetRelativeTreePath(string sessionsRoot, string sessionDirectory)
    {
        var relative = Path.GetRelativePath(sessionsRoot, sessionDirectory);
        var parent = Path.GetDirectoryName(relative);
        return string.IsNullOrWhiteSpace(parent) ? string.Empty : parent;
    }
}
