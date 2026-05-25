using System.Text.Json;
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

    private readonly string _filePath;
    private readonly Lock _gate = new();

    public SpeakingPracticeRoomStore()
    {
        var appRoot = AppPathHelper.GetDefaultAppRoot();
        _filePath = Path.Combine(appRoot, "speaking-practice-rooms.json");
    }

    public IReadOnlyList<SpeakingPracticeRoomRecord> GetRooms()
    {
        lock (_gate)
        {
            return LoadInternal()
                .Rooms
                .OrderByDescending(room => room.UpdatedAt)
                .ThenByDescending(room => room.CreatedAt)
                .Select(CloneRoom)
                .ToList();
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
            var document = LoadInternal();
            document.Rooms.Insert(0, record);
            SaveInternal(document);
        }

        return CloneRoom(record);
    }

    public void DeleteRoom(string roomId)
    {
        lock (_gate)
        {
            var document = LoadInternal();
            document.Rooms.RemoveAll(room => string.Equals(room.Id, roomId, StringComparison.Ordinal));
            SaveInternal(document);
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
            var document = LoadInternal();
            var removed = document.Rooms.RemoveAll(room => ids.Contains(room.Id));
            if (removed > 0)
            {
                SaveInternal(document);
            }

            return removed;
        }
    }

    public SpeakingPracticeRoomRecord? GetRoom(string roomId)
    {
        lock (_gate)
        {
            var room = LoadInternal().Rooms.FirstOrDefault(room => string.Equals(room.Id, roomId, StringComparison.Ordinal));
            return room is null ? null : CloneRoom(room);
        }
    }

    public SpeakingPracticeRoomRecord? UpdateRoom(string roomId, string title, string instructions)
    {
        var normalizedTitle = title.Trim();
        var normalizedInstructions = instructions.Trim();

        lock (_gate)
        {
            var document = LoadInternal();
            var room = document.Rooms.FirstOrDefault(item => string.Equals(item.Id, roomId, StringComparison.Ordinal));
            if (room is null)
            {
                return null;
            }

            room.Name = BuildRoomName(normalizedTitle, normalizedInstructions);
            room.Instructions = normalizedInstructions;
            room.UpdatedAt = DateTimeOffset.UtcNow;
            SaveInternal(document);
            return CloneRoom(room);
        }
    }

    public void ReplaceMessages(string roomId, IReadOnlyList<PracticeMessage> messages, Func<PracticeMessage, bool>? isSpokenMessage = null)
    {
        lock (_gate)
        {
            var document = LoadInternal();
            var room = document.Rooms.FirstOrDefault(item => string.Equals(item.Id, roomId, StringComparison.Ordinal));
            if (room is null)
            {
                return;
            }

            room.Messages = messages
                .Select(message => new SpeakingPracticeMessageRecord
                {
                    Id = message.Id,
                    Sender = message.Sender.ToString(),
                    Text = message.Text,
                    EnhancementAdvice = message.EnhancementAdvice,
                    Timestamp = message.Timestamp,
                    IsSpoken = isSpokenMessage?.Invoke(message) ?? false,
                })
                .ToList();

            if (room.Messages.Count > 0)
            {
                room.UpdatedAt = room.Messages.Max(message => message.Timestamp);
            }

            SaveInternal(document);
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
                message.Timestamp))
            .ToList();
    }

    private SpeakingPracticeRoomsDocument LoadInternal()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return new SpeakingPracticeRoomsDocument();
            }

            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<SpeakingPracticeRoomsDocument>(json, SerializerOptions)
                ?? new SpeakingPracticeRoomsDocument();
        }
        catch
        {
            return new SpeakingPracticeRoomsDocument();
        }
    }

    private void SaveInternal(SpeakingPracticeRoomsDocument document)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(document, SerializerOptions);
        File.WriteAllText(_filePath, json);
    }

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
                })
                .ToList(),
        };
    }
}
