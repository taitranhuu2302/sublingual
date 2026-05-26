using System.Text.Json;
using Sublingual.App.Models;

namespace Sublingual.App.Services;

public sealed class CaptureSessionStorage
{
    public const string GlobalSessionFolderId = "global";
    public const string GlobalSessionFolderName = "Global";
    public const string GlobalSessionFolderSlug = "global";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly AppSettingsStore _settingsStore;
    private readonly SessionIndexStore _index;

    public CaptureSessionStorage(AppSettingsStore settingsStore, SessionIndexStore index)
    {
        _settingsStore = settingsStore;
        _index = index;
    }

    public string CreateSessionOutputPath(string? sessionTitle = null, string? folderSelector = null)
    {
        var folder = ResolveFolder(folderSelector);
        var sessionsRoot = GetSessionsRoot();
        Directory.CreateDirectory(sessionsRoot);

        var baseDirectory = Path.Combine(sessionsRoot, folder.Slug);
        Directory.CreateDirectory(baseDirectory);

        var titleSlug = SlugifySessionTitle(sessionTitle);
        var sessionId = string.IsNullOrWhiteSpace(titleSlug)
            ? $"session-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}"
            : $"{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{titleSlug}-{Guid.NewGuid():N}";
        var sessionDirectory = Path.Combine(baseDirectory, sessionId);
        Directory.CreateDirectory(sessionDirectory);

        return Path.Combine(sessionDirectory, "audio.wav");
    }

    public string NormalizeSessionTreePath(string? sessionTreePath)
    {
        return ResolveFolder(sessionTreePath).Name;
    }

    public string GetSessionsRoot()
    {
        var settings = _settingsStore.Load();
        return AppPathHelper.ResolveConfiguredPath(settings.Storage.SessionsRoot, "sessions");
    }

    public IReadOnlyList<SessionFolderRecord> GetFolders()
    {
        EnsureDefaultFolder();
        return LoadFolders().Folders
            .OrderByDescending(folder => folder.IsDefault)
            .ThenBy(folder => folder.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public SessionFolderRecord EnsureDefaultFolder()
    {
        var sessionsRoot = GetSessionsRoot();
        Directory.CreateDirectory(sessionsRoot);

        var collection = LoadFolders();
        var existing = collection.Folders.FirstOrDefault(folder =>
            string.Equals(folder.Id, GlobalSessionFolderId, StringComparison.OrdinalIgnoreCase));

        if (existing is null)
        {
            existing = new SessionFolderRecord
            {
                Id = GlobalSessionFolderId,
                Name = GlobalSessionFolderName,
                Slug = GlobalSessionFolderSlug,
                IsDefault = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };
            collection.Folders.Add(existing);
            SaveFolders(collection);
            _index.UpsertFolder(existing);
        }
        else
        {
            existing.Name = GlobalSessionFolderName;
            existing.Slug = GlobalSessionFolderSlug;
            existing.IsDefault = true;
            SaveFolders(collection);
            _index.UpsertFolder(existing);
        }

        Directory.CreateDirectory(Path.Combine(sessionsRoot, GlobalSessionFolderSlug));
        return existing;
    }

    public SessionFolderRecord CreateFolder(string name)
    {
        var normalizedName = NormalizeFolderName(name);
        var collection = LoadFolders();
        EnsureDefaultFolder();
        collection = LoadFolders();

        if (collection.Folders.Any(folder => string.Equals(folder.Name, normalizedName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Folder `{normalizedName}` already exists.");
        }

        var slug = BuildUniqueFolderSlug(collection.Folders, normalizedName);
        var folder = new SessionFolderRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = normalizedName,
            Slug = slug,
            IsDefault = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        collection.Folders.Add(folder);
        SaveFolders(collection);
        Directory.CreateDirectory(Path.Combine(GetSessionsRoot(), folder.Slug));
        _index.UpsertFolder(folder);
        return folder;
    }

    public IReadOnlyList<CaptureSessionRecord> GetSessions()
    {
        EnsureDefaultFolder();
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
                var folder = ResolveFolderForSessionDirectory(path, metadata);

                return new CaptureSessionRecord
                {
                    SessionId = Path.GetFileName(path),
                    FolderId = folder.Id,
                    Title = metadata?.Title ?? string.Empty,
                    TreePath = folder.Name,
                    DirectoryPath = path,
                    AudioPath = Path.Combine(path, "audio.wav"),
                    TranscriptPath = Path.Combine(path, "transcript.json"),
                    MetadataPath = metadataPath,
                    CreatedAt = metadata?.CreatedAt ?? directoryInfo.CreationTimeUtc,
                };
            })
            .OrderByDescending(session => session.CreatedAt)
            .ToList();
    }

    public IReadOnlyList<string> GetSessionFolderPaths()
    {
        return GetFolders().Select(folder => folder.Name).ToList();
    }

    public string CreateSessionFolder(string? sessionTreePath)
    {
        var folder = CreateFolder(sessionTreePath ?? string.Empty);
        return folder.Name;
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

            var sessionId = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            Directory.Delete(path, true);
            _index.DeleteTranscriptEntriesForSession(sessionId);
            _index.DeleteSession(sessionId);
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
        var existingIndex = existingEntries.FindIndex(existing =>
            string.Equals(existing.SegmentId, entry.SegmentId, StringComparison.Ordinal));

        if (existingIndex >= 0)
        {
            var existing = existingEntries[existingIndex];
            var isDuplicate = string.Equals(existing.OriginalText, entry.OriginalText, StringComparison.Ordinal)
                && string.Equals(existing.TranslatedText, entry.TranslatedText, StringComparison.Ordinal)
                && existing.IsFinal == entry.IsFinal;
            if (isDuplicate)
            {
                return;
            }

            existingEntries[existingIndex] = entry;
        }
        else
        {
            existingEntries.Add(entry);
        }

        var json = JsonSerializer.Serialize(existingEntries, SerializerOptions);
        File.WriteAllText(transcriptPath, json);

        var sessionId = Path.GetFileName(sessionDirectory);
        _index.UpsertTranscriptEntry(sessionId, entry);
    }

    public void DeleteTranscriptEntry(string outputAudioPath, string segmentId)
    {
        var sessionDirectory = Path.GetDirectoryName(outputAudioPath);
        if (string.IsNullOrWhiteSpace(sessionDirectory))
        {
            return;
        }

        var transcriptPath = Path.Combine(sessionDirectory, "transcript.json");
        var existingEntries = LoadTranscriptEntries(transcriptPath).ToList();
        var removed = existingEntries.RemoveAll(entry => string.Equals(entry.SegmentId, segmentId, StringComparison.Ordinal));
        if (removed == 0)
        {
            return;
        }

        var json = JsonSerializer.Serialize(existingEntries, SerializerOptions);
        File.WriteAllText(transcriptPath, json);

        var sessionId = Path.GetFileName(sessionDirectory);
        _index.DeleteTranscriptEntry(sessionId, segmentId);
    }

    public void SaveSessionMetadata(string outputAudioPath, CaptureSessionMetadata metadata)
    {
        var sessionDirectory = Path.GetDirectoryName(outputAudioPath);
        if (string.IsNullOrWhiteSpace(sessionDirectory))
        {
            return;
        }

        Directory.CreateDirectory(sessionDirectory);
        var resolvedFolder = ResolveFolderForSessionDirectory(sessionDirectory, metadata);
        metadata.FolderId = resolvedFolder.Id;
        metadata.FolderName = resolvedFolder.Name;
        metadata.FolderSlug = resolvedFolder.Slug;
        metadata.TreePath = resolvedFolder.Name;

        var metadataPath = Path.Combine(sessionDirectory, "session.json");
        var json = JsonSerializer.Serialize(metadata, SerializerOptions);
        File.WriteAllText(metadataPath, json);

        var sessionRecord = new CaptureSessionRecord
        {
            SessionId = Path.GetFileName(sessionDirectory),
            FolderId = resolvedFolder.Id,
            Title = metadata.Title,
            DirectoryPath = sessionDirectory,
            AudioPath = outputAudioPath,
            TranscriptPath = Path.Combine(sessionDirectory, "transcript.json"),
            MetadataPath = metadataPath,
            CreatedAt = metadata.CreatedAt,
        };
        _index.UpsertSession(sessionRecord, metadata);
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
            var metadata = JsonSerializer.Deserialize<CaptureSessionMetadata>(json, SerializerOptions);
            if (metadata is null)
            {
                return null;
            }

            var sessionDirectory = Path.GetDirectoryName(metadataPath);
            if (string.IsNullOrWhiteSpace(sessionDirectory))
            {
                return metadata;
            }

            var folder = ResolveFolderForSessionDirectory(sessionDirectory, metadata);
            metadata.FolderId = folder.Id;
            metadata.FolderName = folder.Name;
            metadata.FolderSlug = folder.Slug;
            metadata.TreePath = folder.Name;
            return metadata;
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

    public string GetPreferredFolderName()
    {
        var settings = _settingsStore.Load();
        var folder = ResolveFolder(settings.Storage.LastSessionFolderId);
        return folder.Name;
    }

    public void SetPreferredFolder(string? folderSelector)
    {
        var folder = ResolveFolder(folderSelector);
        var settings = _settingsStore.Load();
        settings.Storage.LastSessionFolderId = folder.Id;
        settings.Storage.LastSessionTreePath = folder.Name;
        _settingsStore.Save(settings);
    }

    public string GetPreferredFolderId()
    {
        var settings = _settingsStore.Load();
        return ResolveFolder(settings.Storage.LastSessionFolderId).Id;
    }

    public SessionFolderRecord RenameFolder(string folderId, string newName)
    {
        EnsureDefaultFolder();
        var normalizedNewName = NormalizeFolderName(newName);

        if (string.Equals(folderId, GlobalSessionFolderId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("`Global` cannot be renamed.");
        }

        var collection = LoadFolders();
        var folder = collection.Folders.FirstOrDefault(f => string.Equals(f.Id, folderId, StringComparison.OrdinalIgnoreCase));
        if (folder is null)
        {
            throw new InvalidOperationException("Folder not found.");
        }

        if (collection.Folders.Any(f => !string.Equals(f.Id, folder.Id, StringComparison.OrdinalIgnoreCase)
                                        && string.Equals(f.Name, normalizedNewName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Folder `{normalizedNewName}` already exists.");
        }

        folder.Name = normalizedNewName;
        folder.UpdatedAt = DateTimeOffset.UtcNow;
        SaveFolders(collection);
        _index.UpsertFolder(folder);
        return folder;
    }

    public int DeleteFolder(string folderId)
    {
        EnsureDefaultFolder();

        if (string.Equals(folderId, GlobalSessionFolderId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("`Global` cannot be deleted.");
        }

        var collection = LoadFolders();
        var folder = collection.Folders.FirstOrDefault(f => string.Equals(f.Id, folderId, StringComparison.OrdinalIgnoreCase));
        if (folder is null)
        {
            return 0;
        }

        var moved = 0;
        var sessions = GetSessions();
        foreach (var session in sessions.Where(s => string.Equals(s.FolderId, folder.Id, StringComparison.OrdinalIgnoreCase)))
        {
            if (MoveSessionDirectory(session.DirectoryPath, GlobalSessionFolderId) is not null)
            {
                moved += 1;
            }
        }

        collection.Folders.RemoveAll(f => string.Equals(f.Id, folder.Id, StringComparison.OrdinalIgnoreCase));
        SaveFolders(collection);
        _index.DeleteFolder(folder.Id);

        var settings = _settingsStore.Load();
        if (string.Equals(settings.Storage.LastSessionFolderId, folder.Id, StringComparison.OrdinalIgnoreCase))
        {
            settings.Storage.LastSessionFolderId = GlobalSessionFolderId;
            settings.Storage.LastSessionTreePath = GlobalSessionFolderName;
            _settingsStore.Save(settings);
        }

        // Best-effort cleanup of the corresponding slug directory.
        var folderDirectory = Path.Combine(GetSessionsRoot(), folder.Slug);
        TryDeleteDirectoryIfEmpty(folderDirectory);

        return moved;
    }

    public int MoveSessions(IEnumerable<string> sessionDirectoryPaths, string targetFolderSelector)
    {
        var moved = 0;
        foreach (var path in sessionDirectoryPaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (MoveSessionDirectory(path, targetFolderSelector) is not null)
            {
                moved += 1;
            }
        }

        return moved;
    }

    public string ResolveFolderId(string? folderSelector)
    {
        return ResolveFolder(folderSelector).Id;
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
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var entries = new List<SavedTranscriptEntry>();
            var legacyIndex = 0;
            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (TryReadSegmentTranscriptEntry(element, out var segmentEntry))
                {
                    entries.Add(segmentEntry);
                    continue;
                }

                if (TryReadLegacyTranscriptEntry(element, legacyIndex, out var legacyEntry))
                {
                    entries.Add(legacyEntry);
                    legacyIndex += 1;
                }
            }

            return entries;
        }
        catch
        {
            return [];
        }
    }

    private static bool TryReadSegmentTranscriptEntry(JsonElement element, out SavedTranscriptEntry entry)
    {
        entry = null!;

        if (!element.TryGetProperty(nameof(SavedTranscriptEntry.SegmentId), out var segmentIdElement)
            || !element.TryGetProperty(nameof(SavedTranscriptEntry.OriginalText), out var originalTextElement)
            || !element.TryGetProperty(nameof(SavedTranscriptEntry.TranslatedText), out var translatedTextElement)
            || !element.TryGetProperty(nameof(SavedTranscriptEntry.IsFinal), out var isFinalElement)
            || !element.TryGetProperty(nameof(SavedTranscriptEntry.UpdatedAt), out var updatedAtElement))
        {
            return false;
        }

        var segmentId = segmentIdElement.GetString();
        var originalText = originalTextElement.GetString();
        if (string.IsNullOrWhiteSpace(segmentId) || originalText is null)
        {
            return false;
        }

        entry = new SavedTranscriptEntry
        {
            SegmentId = segmentId,
            OriginalText = originalText,
            TranslatedText = translatedTextElement.GetString() ?? string.Empty,
            IsFinal = isFinalElement.GetBoolean(),
            UpdatedAt = updatedAtElement.GetDateTimeOffset(),
        };
        return true;
    }

    private static bool TryReadLegacyTranscriptEntry(JsonElement element, int legacyIndex, out SavedTranscriptEntry entry)
    {
        entry = null!;

        var partialText = element.TryGetProperty("PartialText", out var partialTextElement)
            ? partialTextElement.GetString() ?? string.Empty
            : string.Empty;
        var partialTranslatedText = element.TryGetProperty("PartialTranslatedText", out var partialTranslatedTextElement)
            ? partialTranslatedTextElement.GetString() ?? string.Empty
            : string.Empty;
        var finalText = element.TryGetProperty("FinalText", out var finalTextElement)
            ? finalTextElement.GetString() ?? string.Empty
            : string.Empty;
        var finalTranslatedText = element.TryGetProperty("FinalTranslatedText", out var finalTranslatedTextElement)
            ? finalTranslatedTextElement.GetString() ?? string.Empty
            : string.Empty;

        if (!element.TryGetProperty("UpdatedAt", out var updatedAtElement))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(finalText) || !string.IsNullOrWhiteSpace(finalTranslatedText))
        {
            entry = new SavedTranscriptEntry
            {
                SegmentId = $"legacy-final-{legacyIndex:D8}",
                OriginalText = finalText,
                TranslatedText = finalTranslatedText,
                IsFinal = true,
                UpdatedAt = updatedAtElement.GetDateTimeOffset(),
            };
            return true;
        }

        if (string.IsNullOrWhiteSpace(partialText) && string.IsNullOrWhiteSpace(partialTranslatedText))
        {
            return false;
        }

        entry = new SavedTranscriptEntry
        {
            SegmentId = $"legacy-draft-{legacyIndex:D8}",
            OriginalText = partialText,
            TranslatedText = partialTranslatedText,
            IsFinal = false,
            UpdatedAt = updatedAtElement.GetDateTimeOffset(),
        };
        return true;
    }

    private SessionFolderCollection LoadFolders()
    {
        var path = GetFoldersFilePath();
        if (!File.Exists(path))
        {
            return new SessionFolderCollection();
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<SessionFolderCollection>(json, SerializerOptions) ?? new SessionFolderCollection();
        }
        catch
        {
            return new SessionFolderCollection();
        }
    }

    private void SaveFolders(SessionFolderCollection collection)
    {
        var path = GetFoldersFilePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(collection, SerializerOptions);
        File.WriteAllText(path, json);
    }

    private string GetFoldersFilePath()
    {
        return Path.Combine(GetSessionsRoot(), "folders.json");
    }

    private SessionFolderRecord ResolveFolder(string? folderSelector)
    {
        EnsureDefaultFolder();
        var folders = LoadFolders().Folders;
        var normalizedSelector = folderSelector?.Trim();

        if (string.IsNullOrWhiteSpace(normalizedSelector))
        {
            return folders.First(folder => folder.IsDefault);
        }

        var byId = folders.FirstOrDefault(folder => string.Equals(folder.Id, normalizedSelector, StringComparison.OrdinalIgnoreCase));
        if (byId is not null)
        {
            return byId;
        }

        var byName = folders.FirstOrDefault(folder => string.Equals(folder.Name, normalizedSelector, StringComparison.OrdinalIgnoreCase));
        if (byName is not null)
        {
            return byName;
        }

        var bySlug = folders.FirstOrDefault(folder => string.Equals(folder.Slug, NormalizeFolderSlug(normalizedSelector), StringComparison.OrdinalIgnoreCase));
        if (bySlug is not null)
        {
            return bySlug;
        }

        return folders.First(folder => folder.IsDefault);
    }

    private SessionFolderRecord ResolveFolderForSessionDirectory(string sessionDirectory, CaptureSessionMetadata? metadata)
    {
        EnsureDefaultFolder();
        var folders = LoadFolders().Folders;

        if (metadata is not null)
        {
            var fromMetadata = ResolveFolder(metadata.FolderId);
            if (!fromMetadata.IsDefault || HasKnownFolderSelector(metadata.FolderId))
            {
                return fromMetadata;
            }

            fromMetadata = ResolveFolder(metadata.FolderName);
            if (!fromMetadata.IsDefault || HasKnownFolderSelector(metadata.FolderName))
            {
                return fromMetadata;
            }

            fromMetadata = ResolveFolder(metadata.FolderSlug);
            if (!fromMetadata.IsDefault || HasKnownFolderSelector(metadata.FolderSlug))
            {
                return fromMetadata;
            }

            fromMetadata = ResolveFolder(metadata.TreePath);
            if (!fromMetadata.IsDefault || HasKnownFolderSelector(metadata.TreePath))
            {
                return fromMetadata;
            }
        }

        var sessionsRoot = GetSessionsRoot();
        var relative = Path.GetRelativePath(sessionsRoot, sessionDirectory);
        var segments = relative.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // A session directory is expected to be: <folderSlug>/<sessionId>. For legacy nested paths,
        // collapse all folder segments into a flat selector (joined by '-').
        var folderSelector = segments.Length >= 2
            ? segments.Length == 2
                ? segments[0]
                : string.Join('-', segments.Take(segments.Length - 1))
            : string.Empty;

        var byPath = ResolveFolder(folderSelector);
        if (!byPath.IsDefault || HasKnownFolderSelector(folderSelector))
        {
            return byPath;
        }

        return folders.First(folder => folder.IsDefault);
    }

    private string? MoveSessionDirectory(string sessionDirectoryPath, string targetFolderSelector)
    {
        if (string.IsNullOrWhiteSpace(sessionDirectoryPath) || !Directory.Exists(sessionDirectoryPath))
        {
            return null;
        }

        var targetFolder = ResolveFolder(targetFolderSelector);
        var sessionsRoot = GetSessionsRoot();
        Directory.CreateDirectory(Path.Combine(sessionsRoot, targetFolder.Slug));

        var sessionId = Path.GetFileName(sessionDirectoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return null;
        }

        var targetDirectory = Path.Combine(sessionsRoot, targetFolder.Slug, sessionId);
        if (string.Equals(Path.GetFullPath(sessionDirectoryPath), Path.GetFullPath(targetDirectory), StringComparison.OrdinalIgnoreCase))
        {
            // Still ensure metadata is updated to reflect current folder fields.
            var audioPath = Path.Combine(sessionDirectoryPath, "audio.wav");
            var metadata = GetSessionMetadata(Path.Combine(sessionDirectoryPath, "session.json")) ?? new CaptureSessionMetadata();
            SaveSessionMetadata(audioPath, metadata);
            return sessionDirectoryPath;
        }

        if (Directory.Exists(targetDirectory))
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            targetDirectory = Path.Combine(sessionsRoot, targetFolder.Slug, $"{sessionId}-moved-{suffix}");
        }

        Directory.Move(sessionDirectoryPath, targetDirectory);

        var movedAudioPath = Path.Combine(targetDirectory, "audio.wav");
        var movedMetadata = GetSessionMetadata(Path.Combine(targetDirectory, "session.json")) ?? new CaptureSessionMetadata();
        // Ensure the metadata is re-written with the target folder (ResolveFolderForSessionDirectory prefers metadata).
        movedMetadata.FolderId = targetFolder.Id;
        movedMetadata.FolderName = targetFolder.Name;
        movedMetadata.FolderSlug = targetFolder.Slug;
        movedMetadata.TreePath = targetFolder.Name;
        SaveSessionMetadata(movedAudioPath, movedMetadata);

        // Best-effort cleanup of now-empty parent directories.
        TryDeleteDirectoryIfEmpty(Path.GetDirectoryName(sessionDirectoryPath));
        TryDeleteDirectoryIfEmpty(Path.GetDirectoryName(Path.GetDirectoryName(sessionDirectoryPath) ?? string.Empty));

        return targetDirectory;
    }

    private static void TryDeleteDirectoryIfEmpty(string? directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
        {
            return;
        }

        try
        {
            if (!Directory.EnumerateFileSystemEntries(directoryPath).Any())
            {
                Directory.Delete(directoryPath, false);
            }
        }
        catch
        {
            // best-effort cleanup only
        }
    }

    private bool HasKnownFolderSelector(string? selector)
    {
        if (string.IsNullOrWhiteSpace(selector))
        {
            return false;
        }

        var normalizedSelector = selector.Trim();
        var folders = LoadFolders().Folders;
        return folders.Any(folder =>
            string.Equals(folder.Id, normalizedSelector, StringComparison.OrdinalIgnoreCase)
            || string.Equals(folder.Name, normalizedSelector, StringComparison.OrdinalIgnoreCase)
            || string.Equals(folder.Slug, NormalizeFolderSlug(normalizedSelector), StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeFolderName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Folder name is required.");
        }

        var trimmed = name.Trim();
        if (trimmed.Contains('/') || trimmed.Contains('\\'))
        {
            throw new InvalidOperationException("Nested folders are not supported.");
        }

        if (string.Equals(trimmed, GlobalSessionFolderName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, GlobalSessionFolderId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("`Global` is reserved.");
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        if (trimmed.Any(ch => invalidChars.Contains(ch)))
        {
            throw new InvalidOperationException("Folder name contains invalid characters.");
        }

        return trimmed;
    }

    private static string BuildUniqueFolderSlug(IEnumerable<SessionFolderRecord> existingFolders, string folderName)
    {
        var baseSlug = NormalizeFolderSlug(folderName);
        var slug = baseSlug;
        var suffix = 2;
        while (existingFolders.Any(folder => string.Equals(folder.Slug, slug, StringComparison.OrdinalIgnoreCase)))
        {
            slug = $"{baseSlug}-{suffix}";
            suffix += 1;
        }

        return slug;
    }

    private static string NormalizeFolderSlug(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return GlobalSessionFolderSlug;
        }

        var chars = value
            .Trim()
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray();

        var slug = new string(chars).Trim('-');
        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }

        return string.IsNullOrWhiteSpace(slug) ? GlobalSessionFolderSlug : slug;
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
}
