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

    private readonly string _sessionsRoot;

    public CaptureSessionStorage()
    {
        _sessionsRoot = Path.Combine(AppContext.BaseDirectory, "capture-sessions");
    }

    public string CreateSessionOutputPath()
    {
        Directory.CreateDirectory(_sessionsRoot);

        var sessionId = $"session-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}";
        var sessionDirectory = Path.Combine(_sessionsRoot, sessionId);
        Directory.CreateDirectory(sessionDirectory);

        return Path.Combine(sessionDirectory, "audio.wav");
    }

    public string GetSessionsRoot() => _sessionsRoot;

    public IReadOnlyList<CaptureSessionRecord> GetSessions()
    {
        if (!Directory.Exists(_sessionsRoot))
        {
            return [];
        }

        return Directory
            .GetDirectories(_sessionsRoot)
            .Select(path =>
            {
                var directoryInfo = new DirectoryInfo(path);
                return new CaptureSessionRecord
                {
                    SessionId = Path.GetFileName(path),
                    DirectoryPath = path,
                    AudioPath = Path.Combine(path, "audio.wav"),
                    TranscriptPath = Path.Combine(path, "transcript.json"),
                    MetadataPath = Path.Combine(path, "session.json"),
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
}
