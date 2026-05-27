using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Sublingual.App.Models;
using Sublingual.Domain.SpeakingPractice;

namespace Sublingual.App.Services;

public sealed class SpeakingPracticeRoomStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _legacyJsonPath;
    private readonly LocalSqliteDatabase _db;
    private readonly Lock _gate = new();
    private bool _migrationChecked;

    public SpeakingPracticeRoomStore(LocalSqliteDatabase db)
    {
        var appRoot = AppPathHelper.GetDefaultAppRoot();
        _legacyJsonPath = Path.Combine(appRoot, "speaking-practice-rooms.json");
        _db = db;
    }

    public SpeakingPracticeRoomStore() : this(new LocalSqliteDatabase())
    {
    }

    public IReadOnlyList<SpeakingPracticeRoomRecord> GetRooms()
    {
        lock (_gate)
        {
            EnsureMigratedFromJsonIfNeeded();
            using var connection = _db.OpenConnection();
            return LoadRooms(connection);
        }
    }

    public SpeakingPracticeRoomRecord CreateRoom(string title, string instructions)
    {
        var normalizedTitle = title.Trim();
        var normalizedInstructions = instructions.Trim();
        var record = new SpeakingPracticeRoomRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            Instructions = normalizedInstructions,
            Name = BuildRoomName(normalizedTitle, normalizedInstructions),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        lock (_gate)
        {
            EnsureMigratedFromJsonIfNeeded();
            using var connection = _db.OpenConnection();
            using var tx = connection.BeginTransaction();

            using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = """
                    INSERT INTO practice_rooms(id, name, instructions, created_at, updated_at)
                    VALUES ($id, $name, $instructions, $created_at, $updated_at);
                    """;
                cmd.Parameters.AddWithValue("$id", record.Id);
                cmd.Parameters.AddWithValue("$name", record.Name);
                cmd.Parameters.AddWithValue("$instructions", record.Instructions);
                cmd.Parameters.AddWithValue("$created_at", ToDbTime(record.CreatedAt));
                cmd.Parameters.AddWithValue("$updated_at", ToDbTime(record.UpdatedAt));
                cmd.ExecuteNonQuery();
            }

            tx.Commit();
        }

        return CloneRoom(record);
    }

    public void DeleteRoom(string roomId)
    {
        lock (_gate)
        {
            EnsureMigratedFromJsonIfNeeded();
            using var connection = _db.OpenConnection();
            using var tx = connection.BeginTransaction();
            using var cmd = connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "DELETE FROM practice_rooms WHERE id = $id;";
            cmd.Parameters.AddWithValue("$id", roomId);
            cmd.ExecuteNonQuery();
            tx.Commit();
        }
    }

    public int DeleteRooms(IEnumerable<string> roomIds)
    {
        var ids = roomIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);

        if (ids.Count == 0)
        {
            return 0;
        }

        lock (_gate)
        {
            EnsureMigratedFromJsonIfNeeded();
            using var connection = _db.OpenConnection();
            using var tx = connection.BeginTransaction();

            var removed = 0;
            using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "DELETE FROM practice_rooms WHERE id = $id;";
                var idParam = cmd.CreateParameter();
                idParam.ParameterName = "$id";
                cmd.Parameters.Add(idParam);

                foreach (var id in ids)
                {
                    idParam.Value = id;
                    removed += cmd.ExecuteNonQuery();
                }
            }

            tx.Commit();
            return removed;
        }
    }

    public SpeakingPracticeRoomRecord? GetRoom(string roomId)
    {
        lock (_gate)
        {
            EnsureMigratedFromJsonIfNeeded();
            using var connection = _db.OpenConnection();
            return LoadRoom(connection, roomId);
        }
    }

    public SpeakingPracticeRoomMemoryRecord? GetRoomMemory(string roomId)
    {
        lock (_gate)
        {
            EnsureMigratedFromJsonIfNeeded();
            using var connection = _db.OpenConnection();
            return LoadRoomMemory(connection, roomId);
        }
    }

    public void UpsertRoomMemory(string roomId, string preferencesJson)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            return;
        }

        var payload = string.IsNullOrWhiteSpace(preferencesJson) ? "{}" : preferencesJson.Trim();

        lock (_gate)
        {
            EnsureMigratedFromJsonIfNeeded();
            using var connection = _db.OpenConnection();
            using var tx = connection.BeginTransaction();

            using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = """
                    INSERT INTO practice_room_memory(room_id, preferences_json, updated_at)
                    VALUES ($room_id, $preferences_json, $updated_at)
                    ON CONFLICT(room_id)
                    DO UPDATE SET preferences_json = $preferences_json, updated_at = $updated_at;
                    """;
                cmd.Parameters.AddWithValue("$room_id", roomId);
                cmd.Parameters.AddWithValue("$preferences_json", payload);
                cmd.Parameters.AddWithValue("$updated_at", ToDbTime(DateTimeOffset.UtcNow));
                cmd.ExecuteNonQuery();
            }

            tx.Commit();
        }
    }

    public SpeakingPracticeRoomRecord? UpdateRoom(string roomId, string title, string instructions)
    {
        var normalizedTitle = title.Trim();
        var normalizedInstructions = instructions.Trim();

        lock (_gate)
        {
            EnsureMigratedFromJsonIfNeeded();
            using var connection = _db.OpenConnection();
            using var tx = connection.BeginTransaction();

            var existing = LoadRoom(connection, roomId);
            if (existing is null)
            {
                tx.Rollback();
                return null;
            }

            var updatedName = BuildRoomName(normalizedTitle, normalizedInstructions);
            var updatedAt = DateTimeOffset.UtcNow;

            using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = """
                    UPDATE practice_rooms
                    SET name = $name,
                        instructions = $instructions,
                        updated_at = $updated_at
                    WHERE id = $id;
                    """;
                cmd.Parameters.AddWithValue("$id", roomId);
                cmd.Parameters.AddWithValue("$name", updatedName);
                cmd.Parameters.AddWithValue("$instructions", normalizedInstructions);
                cmd.Parameters.AddWithValue("$updated_at", ToDbTime(updatedAt));
                cmd.ExecuteNonQuery();
            }

            tx.Commit();
            return LoadRoom(connection, roomId);
        }
    }

    public void ReplaceMessages(string roomId, IReadOnlyList<PracticeMessage> messages, Func<PracticeMessage, bool>? isSpokenMessage = null)
    {
        lock (_gate)
        {
            EnsureMigratedFromJsonIfNeeded();
            using var connection = _db.OpenConnection();
            using var tx = connection.BeginTransaction();

            // Ensure room exists.
            using (var existsCmd = connection.CreateCommand())
            {
                existsCmd.Transaction = tx;
                existsCmd.CommandText = "SELECT 1 FROM practice_rooms WHERE id = $id LIMIT 1;";
                existsCmd.Parameters.AddWithValue("$id", roomId);
                var exists = existsCmd.ExecuteScalar();
                if (exists is null)
                {
                    tx.Rollback();
                    return;
                }
            }

            // Clear prior messages for the room (suggestions cascade via FK).
            using (var deleteCmd = connection.CreateCommand())
            {
                deleteCmd.Transaction = tx;
                deleteCmd.CommandText = "DELETE FROM practice_messages WHERE room_id = $room_id;";
                deleteCmd.Parameters.AddWithValue("$room_id", roomId);
                deleteCmd.ExecuteNonQuery();
            }

            // Insert messages and suggestions.
            using (var insertMsg = connection.CreateCommand())
            using (var insertSug = connection.CreateCommand())
            {
                insertMsg.Transaction = tx;
                insertMsg.CommandText = """
                    INSERT INTO practice_messages(id, room_id, sender, text, timestamp, is_spoken, enhancement_advice)
                    VALUES ($id, $room_id, $sender, $text, $timestamp, $is_spoken, $enhancement_advice);
                    """;
                var msgId = insertMsg.CreateParameter(); msgId.ParameterName = "$id"; insertMsg.Parameters.Add(msgId);
                var msgRoom = insertMsg.CreateParameter(); msgRoom.ParameterName = "$room_id"; insertMsg.Parameters.Add(msgRoom);
                var msgSender = insertMsg.CreateParameter(); msgSender.ParameterName = "$sender"; insertMsg.Parameters.Add(msgSender);
                var msgText = insertMsg.CreateParameter(); msgText.ParameterName = "$text"; insertMsg.Parameters.Add(msgText);
                var msgTs = insertMsg.CreateParameter(); msgTs.ParameterName = "$timestamp"; insertMsg.Parameters.Add(msgTs);
                var msgSpoken = insertMsg.CreateParameter(); msgSpoken.ParameterName = "$is_spoken"; insertMsg.Parameters.Add(msgSpoken);
                var msgEnh = insertMsg.CreateParameter(); msgEnh.ParameterName = "$enhancement_advice"; insertMsg.Parameters.Add(msgEnh);

                insertSug.Transaction = tx;
                insertSug.CommandText = """
                    INSERT INTO practice_suggestions(message_id, label, text)
                    VALUES ($message_id, $label, $text);
                    """;
                var sugMsgId = insertSug.CreateParameter(); sugMsgId.ParameterName = "$message_id"; insertSug.Parameters.Add(sugMsgId);
                var sugLabel = insertSug.CreateParameter(); sugLabel.ParameterName = "$label"; insertSug.Parameters.Add(sugLabel);
                var sugText = insertSug.CreateParameter(); sugText.ParameterName = "$text"; insertSug.Parameters.Add(sugText);

                foreach (var message in messages)
                {
                    msgId.Value = message.Id;
                    msgRoom.Value = roomId;
                    msgSender.Value = message.Sender.ToString();
                    msgText.Value = message.Text;
                    msgTs.Value = ToDbTime(message.Timestamp);
                    msgSpoken.Value = (isSpokenMessage?.Invoke(message) ?? false) ? 1 : 0;
                    msgEnh.Value = (object?)message.EnhancementAdvice ?? DBNull.Value;
                    insertMsg.ExecuteNonQuery();

                    if (message.Suggestions is not null)
                    {
                        foreach (var suggestion in message.Suggestions)
                        {
                            if (string.IsNullOrWhiteSpace(suggestion.Text))
                            {
                                continue;
                            }

                            sugMsgId.Value = message.Id;
                            sugLabel.Value = suggestion.Label;
                            sugText.Value = suggestion.Text;
                            insertSug.ExecuteNonQuery();
                        }
                    }
                }
            }

            if (messages.Count > 0)
            {
                var updatedAt = messages.Max(m => m.Timestamp);
                using var updateRoom = connection.CreateCommand();
                updateRoom.Transaction = tx;
                updateRoom.CommandText = "UPDATE practice_rooms SET updated_at = $updated_at WHERE id = $id;";
                updateRoom.Parameters.AddWithValue("$id", roomId);
                updateRoom.Parameters.AddWithValue("$updated_at", ToDbTime(updatedAt));
                updateRoom.ExecuteNonQuery();
            }

            tx.Commit();
        }
    }

    public static string BuildRoomName(string title, string instructions)
    {
        if (!string.IsNullOrWhiteSpace(title))
        {
            return title.Trim();
        }

        if (string.IsNullOrWhiteSpace(instructions))
        {
            return "Daily conversation";
        }

        var compact = string.Join(' ', instructions
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        if (compact.Length <= 48)
        {
            return compact;
        }

        return compact[..48].TrimEnd() + "...";
    }

    public static IReadOnlyList<PracticeMessage> ToDomainMessages(SpeakingPracticeRoomRecord room)
    {
        return room.Messages
            .OrderBy(message => message.Timestamp)
            .Select(message => new PracticeMessage(
                message.Id,
                ParseSender(message.Sender),
                message.Text,
                message.EnhancementAdvice,
                message.Timestamp,
                message.Suggestions?.Select(s => new SuggestionOption(s.Label, s.Text)).ToList()))
            .ToList();
    }

    private void EnsureMigratedFromJsonIfNeeded()
    {
        if (_migrationChecked)
        {
            return;
        }

        // Must be called under _gate.
        using var connection = _db.OpenConnection();

        try
        {
            var legacyExists = File.Exists(_legacyJsonPath);
            if (!legacyExists)
            {
                _migrationChecked = true;
                return;
            }

            if (GetRoomCount(connection) > 0)
            {
                _migrationChecked = true;
                return;
            }

            var document = LoadLegacyJsonDocument();
            if (document.Rooms.Count == 0)
            {
                _migrationChecked = true;
                return;
            }

            using var tx = connection.BeginTransaction();
            InsertLegacyRooms(connection, tx, document);
            tx.Commit();

            TryBackupLegacyJson();
            _migrationChecked = true;
        }
        catch
        {
            // If migration fails, keep running with empty DB and leave JSON untouched.
            _migrationChecked = true;
        }
    }

    private SpeakingPracticeRoomsDocument LoadLegacyJsonDocument()
    {
        try
        {
            if (!File.Exists(_legacyJsonPath))
            {
                return new SpeakingPracticeRoomsDocument();
            }

            var json = File.ReadAllText(_legacyJsonPath);
            return JsonSerializer.Deserialize<SpeakingPracticeRoomsDocument>(json, SerializerOptions)
                   ?? new SpeakingPracticeRoomsDocument();
        }
        catch
        {
            return new SpeakingPracticeRoomsDocument();
        }
    }

    private void TryBackupLegacyJson()
    {
        try
        {
            if (!File.Exists(_legacyJsonPath))
            {
                return;
            }

            var target = _legacyJsonPath + ".bak";
            if (File.Exists(target))
            {
                target = _legacyJsonPath + $".bak.{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
            }

            File.Move(_legacyJsonPath, target);
        }
        catch
        {
            // Best-effort.
        }
    }

    private static int GetRoomCount(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM practice_rooms;";
        var result = cmd.ExecuteScalar();
        return result is long l ? (int)l : 0;
    }

    private static void InsertLegacyRooms(SqliteConnection connection, SqliteTransaction tx, SpeakingPracticeRoomsDocument document)
    {
        using var insertRoom = connection.CreateCommand();
        insertRoom.Transaction = tx;
        insertRoom.CommandText = """
            INSERT INTO practice_rooms(id, name, instructions, created_at, updated_at)
            VALUES ($id, $name, $instructions, $created_at, $updated_at);
            """;
        var roomId = insertRoom.CreateParameter(); roomId.ParameterName = "$id"; insertRoom.Parameters.Add(roomId);
        var roomName = insertRoom.CreateParameter(); roomName.ParameterName = "$name"; insertRoom.Parameters.Add(roomName);
        var roomInstr = insertRoom.CreateParameter(); roomInstr.ParameterName = "$instructions"; insertRoom.Parameters.Add(roomInstr);
        var roomCreated = insertRoom.CreateParameter(); roomCreated.ParameterName = "$created_at"; insertRoom.Parameters.Add(roomCreated);
        var roomUpdated = insertRoom.CreateParameter(); roomUpdated.ParameterName = "$updated_at"; insertRoom.Parameters.Add(roomUpdated);

        using var insertMsg = connection.CreateCommand();
        insertMsg.Transaction = tx;
        insertMsg.CommandText = """
            INSERT INTO practice_messages(id, room_id, sender, text, timestamp, is_spoken, enhancement_advice)
            VALUES ($id, $room_id, $sender, $text, $timestamp, $is_spoken, $enhancement_advice);
            """;
        var msgId = insertMsg.CreateParameter(); msgId.ParameterName = "$id"; insertMsg.Parameters.Add(msgId);
        var msgRoom = insertMsg.CreateParameter(); msgRoom.ParameterName = "$room_id"; insertMsg.Parameters.Add(msgRoom);
        var msgSender = insertMsg.CreateParameter(); msgSender.ParameterName = "$sender"; insertMsg.Parameters.Add(msgSender);
        var msgText = insertMsg.CreateParameter(); msgText.ParameterName = "$text"; insertMsg.Parameters.Add(msgText);
        var msgTs = insertMsg.CreateParameter(); msgTs.ParameterName = "$timestamp"; insertMsg.Parameters.Add(msgTs);
        var msgSpoken = insertMsg.CreateParameter(); msgSpoken.ParameterName = "$is_spoken"; insertMsg.Parameters.Add(msgSpoken);
        var msgEnh = insertMsg.CreateParameter(); msgEnh.ParameterName = "$enhancement_advice"; insertMsg.Parameters.Add(msgEnh);

        using var insertSug = connection.CreateCommand();
        insertSug.Transaction = tx;
        insertSug.CommandText = """
            INSERT INTO practice_suggestions(message_id, label, text)
            VALUES ($message_id, $label, $text);
            """;
        var sugMsgId = insertSug.CreateParameter(); sugMsgId.ParameterName = "$message_id"; insertSug.Parameters.Add(sugMsgId);
        var sugLabel = insertSug.CreateParameter(); sugLabel.ParameterName = "$label"; insertSug.Parameters.Add(sugLabel);
        var sugText = insertSug.CreateParameter(); sugText.ParameterName = "$text"; insertSug.Parameters.Add(sugText);

        foreach (var room in document.Rooms)
        {
            roomId.Value = room.Id;
            roomName.Value = room.Name;
            roomInstr.Value = room.Instructions;
            roomCreated.Value = ToDbTime(room.CreatedAt);
            roomUpdated.Value = ToDbTime(room.UpdatedAt);
            insertRoom.ExecuteNonQuery();

            foreach (var message in room.Messages.OrderBy(m => m.Timestamp))
            {
                msgId.Value = message.Id;
                msgRoom.Value = room.Id;
                msgSender.Value = message.Sender;
                msgText.Value = message.Text;
                msgTs.Value = ToDbTime(message.Timestamp);
                msgSpoken.Value = message.IsSpoken ? 1 : 0;
                msgEnh.Value = (object?)message.EnhancementAdvice ?? DBNull.Value;
                insertMsg.ExecuteNonQuery();

                if (message.Suggestions is not null)
                {
                    foreach (var suggestion in message.Suggestions)
                    {
                        if (string.IsNullOrWhiteSpace(suggestion.Text))
                        {
                            continue;
                        }

                        sugMsgId.Value = message.Id;
                        sugLabel.Value = suggestion.Label;
                        sugText.Value = suggestion.Text;
                        insertSug.ExecuteNonQuery();
                    }
                }
            }
        }
    }

    private static IReadOnlyList<SpeakingPracticeRoomRecord> LoadRooms(SqliteConnection connection)
    {
        var rooms = new List<SpeakingPracticeRoomRecord>();
        var memoryLookup = LoadRoomMemories(connection);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT id, name, instructions, created_at, updated_at
            FROM practice_rooms
            ORDER BY updated_at DESC, created_at DESC;
            """;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var room = new SpeakingPracticeRoomRecord
            {
                Id = reader.GetString(0),
                Name = reader.GetString(1),
                Instructions = reader.GetString(2),
                CreatedAt = ParseDbTime(reader.GetString(3)),
                UpdatedAt = ParseDbTime(reader.GetString(4)),
                Messages = [],
                Memory = memoryLookup.TryGetValue(reader.GetString(0), out var memory) ? memory : null,
            };

            room.Messages = LoadMessagesForRoom(connection, room.Id);
            rooms.Add(room);
        }

        return rooms;
    }

    private static Dictionary<string, SpeakingPracticeRoomMemoryRecord> LoadRoomMemories(SqliteConnection connection)
    {
        var memories = new Dictionary<string, SpeakingPracticeRoomMemoryRecord>(StringComparer.Ordinal);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT room_id, preferences_json, updated_at
            FROM practice_room_memory;
            """;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var roomId = reader.GetString(0);
            memories[roomId] = new SpeakingPracticeRoomMemoryRecord
            {
                PreferencesJson = reader.GetString(1),
                UpdatedAt = ParseDbTime(reader.GetString(2)),
            };
        }

        return memories;
    }

    private static SpeakingPracticeRoomRecord? LoadRoom(SqliteConnection connection, string roomId)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT id, name, instructions, created_at, updated_at
            FROM practice_rooms
            WHERE id = $id
            LIMIT 1;
            """;
        cmd.Parameters.AddWithValue("$id", roomId);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        var room = new SpeakingPracticeRoomRecord
        {
            Id = reader.GetString(0),
            Name = reader.GetString(1),
            Instructions = reader.GetString(2),
            CreatedAt = ParseDbTime(reader.GetString(3)),
            UpdatedAt = ParseDbTime(reader.GetString(4)),
            Messages = LoadMessagesForRoom(connection, roomId),
            Memory = LoadRoomMemory(connection, roomId),
        };
        return room;
    }

    private static SpeakingPracticeRoomMemoryRecord? LoadRoomMemory(SqliteConnection connection, string roomId)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT preferences_json, updated_at
            FROM practice_room_memory
            WHERE room_id = $room_id
            LIMIT 1;
            """;
        cmd.Parameters.AddWithValue("$room_id", roomId);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new SpeakingPracticeRoomMemoryRecord
        {
            PreferencesJson = reader.GetString(0),
            UpdatedAt = ParseDbTime(reader.GetString(1)),
        };
    }

    private static List<SpeakingPracticeMessageRecord> LoadMessagesForRoom(SqliteConnection connection, string roomId)
    {
        var messages = new List<SpeakingPracticeMessageRecord>();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT id, sender, text, enhancement_advice, timestamp, is_spoken
            FROM practice_messages
            WHERE room_id = $room_id
            ORDER BY timestamp ASC;
            """;
        cmd.Parameters.AddWithValue("$room_id", roomId);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var messageId = reader.GetString(0);
            var record = new SpeakingPracticeMessageRecord
            {
                Id = messageId,
                Sender = reader.GetString(1),
                Text = reader.GetString(2),
                EnhancementAdvice = reader.IsDBNull(3) ? null : reader.GetString(3),
                Timestamp = ParseDbTime(reader.GetString(4)),
                IsSpoken = reader.GetInt64(5) != 0,
                Suggestions = LoadSuggestionsForMessage(connection, messageId),
            };
            messages.Add(record);
        }

        return messages;
    }

    private static List<SpeakingPracticeSuggestionOptionRecord>? LoadSuggestionsForMessage(SqliteConnection connection, string messageId)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT label, text
            FROM practice_suggestions
            WHERE message_id = $message_id;
            """;
        cmd.Parameters.AddWithValue("$message_id", messageId);
        using var reader = cmd.ExecuteReader();
        List<SpeakingPracticeSuggestionOptionRecord>? result = null;
        while (reader.Read())
        {
            result ??= [];
            result.Add(new SpeakingPracticeSuggestionOptionRecord
            {
                Label = reader.GetString(0),
                Text = reader.GetString(1),
            });
        }

        return result;
    }

    private static string ToDbTime(DateTimeOffset value) => value.ToString("O", CultureInfo.InvariantCulture);
    private static DateTimeOffset ParseDbTime(string value) => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private static MessageSender ParseSender(string sender)
    {
        return Enum.TryParse<MessageSender>(sender, true, out var parsed)
            ? parsed
            : MessageSender.User;
    }

    private static SpeakingPracticeRoomRecord CloneRoom(SpeakingPracticeRoomRecord source)
    {
        return new SpeakingPracticeRoomRecord
        {
            Id = source.Id,
            Name = source.Name,
            Instructions = source.Instructions,
            CreatedAt = source.CreatedAt,
            UpdatedAt = source.UpdatedAt,
            Messages = source.Messages
                .Select(message => new SpeakingPracticeMessageRecord
                {
                    Id = message.Id,
                    Sender = message.Sender,
                    Text = message.Text,
                    EnhancementAdvice = message.EnhancementAdvice,
                    Timestamp = message.Timestamp,
                    IsSpoken = message.IsSpoken,
                    Suggestions = message.Suggestions?
                        .Select(s => new SpeakingPracticeSuggestionOptionRecord { Label = s.Label, Text = s.Text })
                        .ToList(),
                })
                .ToList(),
        };
    }
}
