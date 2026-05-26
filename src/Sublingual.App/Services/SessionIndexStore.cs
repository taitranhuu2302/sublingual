using System.Globalization;
using Microsoft.Data.Sqlite;
using Sublingual.App.Models;

namespace Sublingual.App.Services;

/// <summary>
/// SQLite-backed index for capture sessions, folders, and transcript entries.
/// Audio/transcript/metadata files remain on disk as the source of truth.
/// This index enables fast listing, search, and paging without scanning the filesystem.
/// All writes are best-effort: failures are swallowed so the main capture pipeline is never blocked.
/// </summary>
public sealed class SessionIndexStore
{
    private readonly LocalSqliteDatabase _db;

    public SessionIndexStore(LocalSqliteDatabase db)
    {
        _db = db;
    }

    // ── Folders ───────────────────────────────────────────────────────────────

    public void UpsertFolder(SessionFolderRecord folder)
    {
        try
        {
            using var connection = _db.OpenConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO session_folders(id, name, slug, is_default, created_at, updated_at)
                VALUES ($id, $name, $slug, $is_default, $created_at, $updated_at)
                ON CONFLICT(id) DO UPDATE SET
                  name       = excluded.name,
                  slug       = excluded.slug,
                  is_default = excluded.is_default,
                  updated_at = excluded.updated_at;
                """;
            cmd.Parameters.AddWithValue("$id", folder.Id);
            cmd.Parameters.AddWithValue("$name", folder.Name);
            cmd.Parameters.AddWithValue("$slug", folder.Slug);
            cmd.Parameters.AddWithValue("$is_default", folder.IsDefault ? 1 : 0);
            cmd.Parameters.AddWithValue("$created_at", ToDbTime(folder.CreatedAt));
            cmd.Parameters.AddWithValue("$updated_at", ToDbTime(folder.UpdatedAt));
            cmd.ExecuteNonQuery();
        }
        catch
        {
            // Best-effort index; never block the caller.
        }
    }

    public void DeleteFolder(string folderId)
    {
        try
        {
            using var connection = _db.OpenConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM session_folders WHERE id = $id;";
            cmd.Parameters.AddWithValue("$id", folderId);
            cmd.ExecuteNonQuery();
        }
        catch { }
    }

    public IReadOnlyList<SessionFolderRecord> GetFolders()
    {
        try
        {
            using var connection = _db.OpenConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                SELECT id, name, slug, is_default, created_at, updated_at
                FROM session_folders
                ORDER BY is_default DESC, name ASC;
                """;
            using var reader = cmd.ExecuteReader();
            var result = new List<SessionFolderRecord>();
            while (reader.Read())
            {
                result.Add(new SessionFolderRecord
                {
                    Id = reader.GetString(0),
                    Name = reader.GetString(1),
                    Slug = reader.GetString(2),
                    IsDefault = reader.GetInt64(3) != 0,
                    CreatedAt = ParseDbTime(reader.GetString(4)),
                    UpdatedAt = ParseDbTime(reader.GetString(5)),
                });
            }

            return result;
        }
        catch
        {
            return [];
        }
    }

    // ── Sessions ──────────────────────────────────────────────────────────────

    public void UpsertSession(CaptureSessionRecord session, CaptureSessionMetadata? metadata)
    {
        try
        {
            using var connection = _db.OpenConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO capture_sessions(
                  session_id, folder_id, title, directory_path,
                  audio_path, transcript_path, metadata_path,
                  model_name, device_name, language, duration_seconds, created_at)
                VALUES (
                  $session_id, $folder_id, $title, $directory_path,
                  $audio_path, $transcript_path, $metadata_path,
                  $model_name, $device_name, $language, $duration_seconds, $created_at)
                ON CONFLICT(session_id) DO UPDATE SET
                  folder_id        = excluded.folder_id,
                  title            = excluded.title,
                  directory_path   = excluded.directory_path,
                  audio_path       = excluded.audio_path,
                  transcript_path  = excluded.transcript_path,
                  metadata_path    = excluded.metadata_path,
                  model_name       = excluded.model_name,
                  device_name      = excluded.device_name,
                  language         = excluded.language,
                  duration_seconds = excluded.duration_seconds;
                """;
            cmd.Parameters.AddWithValue("$session_id", session.SessionId);
            cmd.Parameters.AddWithValue("$folder_id", session.FolderId);
            cmd.Parameters.AddWithValue("$title", session.Title);
            cmd.Parameters.AddWithValue("$directory_path", session.DirectoryPath);
            cmd.Parameters.AddWithValue("$audio_path", session.AudioPath);
            cmd.Parameters.AddWithValue("$transcript_path", session.TranscriptPath);
            cmd.Parameters.AddWithValue("$metadata_path", session.MetadataPath);
            cmd.Parameters.AddWithValue("$model_name", metadata?.ModelName ?? string.Empty);
            cmd.Parameters.AddWithValue("$device_name", metadata?.DeviceName ?? string.Empty);
            cmd.Parameters.AddWithValue("$language", metadata?.Language ?? string.Empty);
            cmd.Parameters.AddWithValue("$duration_seconds", metadata?.DurationSeconds ?? 0.0);
            cmd.Parameters.AddWithValue("$created_at", ToDbTime(session.CreatedAt));
            cmd.ExecuteNonQuery();
        }
        catch { }
    }

    public void DeleteSession(string sessionId)
    {
        try
        {
            using var connection = _db.OpenConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM capture_sessions WHERE session_id = $id;";
            cmd.Parameters.AddWithValue("$id", sessionId);
            cmd.ExecuteNonQuery();
        }
        catch { }
    }

    public void DeleteSessions(IEnumerable<string> sessionIds)
    {
        var ids = sessionIds.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (ids.Count == 0)
        {
            return;
        }

        try
        {
            using var connection = _db.OpenConnection();
            using var tx = connection.BeginTransaction();
            using var cmd = connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "DELETE FROM capture_sessions WHERE session_id = $id;";
            var param = cmd.CreateParameter();
            param.ParameterName = "$id";
            cmd.Parameters.Add(param);

            foreach (var id in ids)
            {
                param.Value = id;
                cmd.ExecuteNonQuery();
            }

            tx.Commit();
        }
        catch { }
    }

    public IReadOnlyList<CaptureSessionRecord> GetSessions(string? folderId = null)
    {
        try
        {
            using var connection = _db.OpenConnection();
            using var cmd = connection.CreateCommand();
            if (string.IsNullOrWhiteSpace(folderId))
            {
                cmd.CommandText = """
                    SELECT session_id, folder_id, title, directory_path, audio_path, transcript_path, metadata_path, created_at
                    FROM capture_sessions
                    ORDER BY created_at DESC;
                    """;
            }
            else
            {
                cmd.CommandText = """
                    SELECT session_id, folder_id, title, directory_path, audio_path, transcript_path, metadata_path, created_at
                    FROM capture_sessions
                    WHERE folder_id = $folder_id
                    ORDER BY created_at DESC;
                    """;
                cmd.Parameters.AddWithValue("$folder_id", folderId);
            }

            using var reader = cmd.ExecuteReader();
            var result = new List<CaptureSessionRecord>();
            while (reader.Read())
            {
                result.Add(new CaptureSessionRecord
                {
                    SessionId = reader.GetString(0),
                    FolderId = reader.GetString(1),
                    Title = reader.GetString(2),
                    DirectoryPath = reader.GetString(3),
                    AudioPath = reader.GetString(4),
                    TranscriptPath = reader.GetString(5),
                    MetadataPath = reader.GetString(6),
                    CreatedAt = ParseDbTime(reader.GetString(7)),
                });
            }

            return result;
        }
        catch
        {
            return [];
        }
    }

    // ── Transcript entries ─────────────────────────────────────────────────────

    public void UpsertTranscriptEntry(string sessionId, SavedTranscriptEntry entry)
    {
        try
        {
            using var connection = _db.OpenConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO transcript_entries(segment_id, session_id, original_text, translated_text, is_final, updated_at)
                VALUES ($segment_id, $session_id, $original_text, $translated_text, $is_final, $updated_at)
                ON CONFLICT(segment_id, session_id) DO UPDATE SET
                  original_text   = excluded.original_text,
                  translated_text = excluded.translated_text,
                  is_final        = excluded.is_final,
                  updated_at      = excluded.updated_at;
                """;
            cmd.Parameters.AddWithValue("$segment_id", entry.SegmentId);
            cmd.Parameters.AddWithValue("$session_id", sessionId);
            cmd.Parameters.AddWithValue("$original_text", entry.OriginalText);
            cmd.Parameters.AddWithValue("$translated_text", entry.TranslatedText);
            cmd.Parameters.AddWithValue("$is_final", entry.IsFinal ? 1 : 0);
            cmd.Parameters.AddWithValue("$updated_at", ToDbTime(entry.UpdatedAt));
            cmd.ExecuteNonQuery();
        }
        catch { }
    }

    public void DeleteTranscriptEntry(string sessionId, string segmentId)
    {
        try
        {
            using var connection = _db.OpenConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM transcript_entries WHERE segment_id = $seg AND session_id = $sid;";
            cmd.Parameters.AddWithValue("$seg", segmentId);
            cmd.Parameters.AddWithValue("$sid", sessionId);
            cmd.ExecuteNonQuery();
        }
        catch { }
    }

    public void DeleteTranscriptEntriesForSession(string sessionId)
    {
        try
        {
            using var connection = _db.OpenConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM transcript_entries WHERE session_id = $sid;";
            cmd.Parameters.AddWithValue("$sid", sessionId);
            cmd.ExecuteNonQuery();
        }
        catch { }
    }

    public IReadOnlyList<SavedTranscriptEntry> GetTranscriptEntries(string sessionId)
    {
        try
        {
            using var connection = _db.OpenConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                SELECT segment_id, original_text, translated_text, is_final, updated_at
                FROM transcript_entries
                WHERE session_id = $session_id
                ORDER BY updated_at ASC;
                """;
            cmd.Parameters.AddWithValue("$session_id", sessionId);
            using var reader = cmd.ExecuteReader();
            var result = new List<SavedTranscriptEntry>();
            while (reader.Read())
            {
                result.Add(new SavedTranscriptEntry
                {
                    SegmentId = reader.GetString(0),
                    OriginalText = reader.GetString(1),
                    TranslatedText = reader.GetString(2),
                    IsFinal = reader.GetInt64(3) != 0,
                    UpdatedAt = ParseDbTime(reader.GetString(4)),
                });
            }

            return result;
        }
        catch
        {
            return [];
        }
    }

    // ── One-time indexer: scan existing sessions folder into DB ───────────────

    /// <summary>
    /// Scans the filesystem sessions root and upserts any sessions not yet in the index.
    /// Called once on startup; safe to call multiple times.
    /// </summary>
    public void IndexExistingSessionsFromFilesystem(
        IReadOnlyList<CaptureSessionRecord> allSessions,
        IReadOnlyList<SessionFolderRecord> allFolders,
        Func<string, CaptureSessionMetadata?> getMetadata)
    {
        try
        {
            foreach (var folder in allFolders)
            {
                UpsertFolder(folder);
            }

            foreach (var session in allSessions)
            {
                var metadata = getMetadata(session.MetadataPath);
                UpsertSession(session, metadata);
            }
        }
        catch { }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string ToDbTime(DateTimeOffset value) =>
        value.ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset ParseDbTime(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
