using Microsoft.Data.Sqlite;

namespace Sublingual.App.Services;

/// <summary>
/// Small helper for the app's local SQLite database.
/// Owns schema initialization and versioned migrations.
/// </summary>
public sealed class LocalSqliteDatabase
{
    private readonly string _dbPath;
    private readonly Lock _gate = new();
    private bool _initialized;

    public LocalSqliteDatabase(string? dbPath = null)
    {
        _dbPath = string.IsNullOrWhiteSpace(dbPath)
            ? AppPathHelper.GetDefaultDatabasePath()
            : dbPath.Trim();
    }

    public SqliteConnection OpenConnection()
    {
        EnsureInitialized();
        var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();
        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        pragma.ExecuteNonQuery();
        return connection;
    }

    private void EnsureInitialized()
    {
        lock (_gate)
        {
            if (_initialized)
            {
                return;
            }

            var directory = Path.GetDirectoryName(_dbPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();
            using var pragma = connection.CreateCommand();
            pragma.CommandText = "PRAGMA foreign_keys = ON;";
            pragma.ExecuteNonQuery();

            using var tx = connection.BeginTransaction();

            EnsureMigrationsTable(connection, tx);
            var version = GetSchemaVersion(connection, tx);
            if (version < 1)
            {
                ApplyMigrationV1(connection, tx);
                SetSchemaVersion(connection, tx, 1);
            }

            if (version < 2)
            {
                ApplyMigrationV2(connection, tx);
                SetSchemaVersion(connection, tx, 2);
            }

            tx.Commit();
            _initialized = true;
        }
    }

    private static void EnsureMigrationsTable(SqliteConnection connection, SqliteTransaction tx)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "CREATE TABLE IF NOT EXISTS schema_migrations (version INTEGER PRIMARY KEY);";
        cmd.ExecuteNonQuery();
    }

    private static int GetSchemaVersion(SqliteConnection connection, SqliteTransaction tx)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT COALESCE(MAX(version), 0) FROM schema_migrations;";
        var result = cmd.ExecuteScalar();
        return result is long l ? (int)l : 0;
    }

    private static void SetSchemaVersion(SqliteConnection connection, SqliteTransaction tx, int version)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "INSERT INTO schema_migrations(version) VALUES ($version);";
        cmd.Parameters.AddWithValue("$version", version);
        cmd.ExecuteNonQuery();
    }

    private static void ApplyMigrationV1(SqliteConnection connection, SqliteTransaction tx)
    {
        // Speaking Practice
        ExecuteNonQuery(connection, tx, """
            CREATE TABLE IF NOT EXISTS practice_rooms (
              id TEXT PRIMARY KEY,
              name TEXT NOT NULL,
              instructions TEXT NOT NULL,
              created_at TEXT NOT NULL,
              updated_at TEXT NOT NULL
            );
            """);

        ExecuteNonQuery(connection, tx, """
            CREATE TABLE IF NOT EXISTS practice_messages (
              id TEXT PRIMARY KEY,
              room_id TEXT NOT NULL,
              sender TEXT NOT NULL,
              text TEXT NOT NULL,
              timestamp TEXT NOT NULL,
              is_spoken INTEGER NOT NULL,
              enhancement_advice TEXT NULL,
              FOREIGN KEY(room_id) REFERENCES practice_rooms(id) ON DELETE CASCADE
            );
            """);

        ExecuteNonQuery(connection, tx, """
            CREATE TABLE IF NOT EXISTS practice_suggestions (
              message_id TEXT NOT NULL,
              label TEXT NOT NULL,
              text TEXT NOT NULL,
              FOREIGN KEY(message_id) REFERENCES practice_messages(id) ON DELETE CASCADE
            );
            """);

        ExecuteNonQuery(connection, tx, "CREATE INDEX IF NOT EXISTS idx_practice_rooms_updated_at ON practice_rooms(updated_at);");
        ExecuteNonQuery(connection, tx, "CREATE INDEX IF NOT EXISTS idx_practice_messages_room_ts ON practice_messages(room_id, timestamp);");
        ExecuteNonQuery(connection, tx, "CREATE INDEX IF NOT EXISTS idx_practice_suggestions_message ON practice_suggestions(message_id);");
    }

    private static void ApplyMigrationV2(SqliteConnection connection, SqliteTransaction tx)
    {
        // Session folders
        ExecuteNonQuery(connection, tx, """
            CREATE TABLE IF NOT EXISTS session_folders (
              id TEXT PRIMARY KEY,
              name TEXT NOT NULL,
              slug TEXT NOT NULL,
              is_default INTEGER NOT NULL DEFAULT 0,
              created_at TEXT NOT NULL,
              updated_at TEXT NOT NULL
            );
            """);

        // Capture sessions metadata index (audio/transcript/metadata remain as files)
        ExecuteNonQuery(connection, tx, """
            CREATE TABLE IF NOT EXISTS capture_sessions (
              session_id TEXT PRIMARY KEY,
              folder_id TEXT NOT NULL,
              title TEXT NOT NULL,
              directory_path TEXT NOT NULL,
              audio_path TEXT NOT NULL,
              transcript_path TEXT NOT NULL,
              metadata_path TEXT NOT NULL,
              model_name TEXT NOT NULL,
              device_name TEXT NOT NULL,
              language TEXT NOT NULL,
              duration_seconds REAL NOT NULL,
              created_at TEXT NOT NULL
            );
            """);

        // Transcript entries index
        ExecuteNonQuery(connection, tx, """
            CREATE TABLE IF NOT EXISTS transcript_entries (
              segment_id TEXT NOT NULL,
              session_id TEXT NOT NULL,
              original_text TEXT NOT NULL,
              translated_text TEXT NOT NULL,
              is_final INTEGER NOT NULL,
              updated_at TEXT NOT NULL,
              PRIMARY KEY (segment_id, session_id)
            );
            """);

        ExecuteNonQuery(connection, tx, "CREATE INDEX IF NOT EXISTS idx_capture_sessions_folder ON capture_sessions(folder_id);");
        ExecuteNonQuery(connection, tx, "CREATE INDEX IF NOT EXISTS idx_capture_sessions_created ON capture_sessions(created_at DESC);");
        ExecuteNonQuery(connection, tx, "CREATE INDEX IF NOT EXISTS idx_transcript_entries_session ON transcript_entries(session_id);");
    }

    private static void ExecuteNonQuery(SqliteConnection connection, SqliteTransaction tx, string sql)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
