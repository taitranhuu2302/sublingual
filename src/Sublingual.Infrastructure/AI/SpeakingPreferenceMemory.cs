using System.Text.Json;
using Sublingual.Domain.SpeakingPractice;

namespace Sublingual.Infrastructure.AI;

public static class SpeakingPreferenceMemory
{
    private const int MaxItems = 6;
    private static readonly string[] PositiveMarkers = ["love", "like", "enjoy", "favorite", "favourite", "prefer", "really into", "am into", "i'm into"];
    private static readonly string[] NegativeMarkers = ["hate", "dislike", "don't like", "do not like", "can't stand", "avoid", "not into", "i'm not into"];

    public static string MergePreferencesJson(string? existingJson, IReadOnlyList<PracticeMessage> history)
    {
        var current = Deserialize(existingJson);
        var latestUser = GetLastUserMessage(history);
        if (latestUser is null || string.IsNullOrWhiteSpace(latestUser.Text))
        {
            return Serialize(current);
        }

        var extracted = ExtractPreference(latestUser.Text);
        if (extracted is null)
        {
            return Serialize(current);
        }

        if (string.Equals(extracted.Sentiment, "positive", StringComparison.OrdinalIgnoreCase))
        {
            current.Likes = Upsert(current.Likes, extracted.Topic);
            current.Dislikes = Remove(current.Dislikes, extracted.Topic);
        }
        else
        {
            current.Dislikes = Upsert(current.Dislikes, extracted.Topic);
            current.Likes = Remove(current.Likes, extracted.Topic);
        }

        return Serialize(current);
    }

    public static string BuildMemorySummary(string? preferencesJson)
    {
        var prefs = Deserialize(preferencesJson);
        var likes = prefs.Likes.Count == 0 ? "none" : string.Join(", ", prefs.Likes);
        var dislikes = prefs.Dislikes.Count == 0 ? "none" : string.Join(", ", prefs.Dislikes);
        return $"likes: {likes}; dislikes: {dislikes}";
    }

    private static PreferenceMemory Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new PreferenceMemory();
        }

        try
        {
            return JsonSerializer.Deserialize<PreferenceMemory>(json) ?? new PreferenceMemory();
        }
        catch
        {
            return new PreferenceMemory();
        }
    }

    private static string Serialize(PreferenceMemory memory)
    {
        return JsonSerializer.Serialize(memory);
    }

    private static PreferenceExtraction? ExtractPreference(string text)
    {
        var lower = text.ToLowerInvariant();
        var positive = PositiveMarkers.Any(marker => lower.Contains(marker, StringComparison.Ordinal));
        var negative = NegativeMarkers.Any(marker => lower.Contains(marker, StringComparison.Ordinal));
        if (!positive && !negative)
        {
            return null;
        }

        var sentiment = negative ? "negative" : "positive";
        var topic = ExtractTopic(text);
        if (string.IsNullOrWhiteSpace(topic))
        {
            return null;
        }

        return new PreferenceExtraction(sentiment, topic);
    }

    private static string ExtractTopic(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        var tokens = trimmed
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => t.Length > 1)
            .ToList();

        if (tokens.Count == 0)
        {
            return string.Empty;
        }

        var start = Math.Max(0, tokens.Count - 4);
        var candidate = string.Join(' ', tokens.Skip(start));
        candidate = candidate.Trim().TrimEnd('.', '!', '?', ',');
        return candidate.Length > 48 ? candidate[..48].TrimEnd() : candidate;
    }

    private static List<string> Upsert(List<string> list, string topic)
    {
        if (list.Any(item => string.Equals(item, topic, StringComparison.OrdinalIgnoreCase)))
        {
            return list;
        }

        list.Insert(0, topic);
        if (list.Count > MaxItems)
        {
            list.RemoveRange(MaxItems, list.Count - MaxItems);
        }

        return list;
    }

    private static List<string> Remove(List<string> list, string topic)
    {
        list.RemoveAll(item => string.Equals(item, topic, StringComparison.OrdinalIgnoreCase));
        return list;
    }

    private static PracticeMessage? GetLastUserMessage(IReadOnlyList<PracticeMessage> history)
    {
        for (var i = history.Count - 1; i >= 0; i--)
        {
            if (history[i].Sender == MessageSender.User)
            {
                return history[i];
            }
        }

        return null;
    }

    private sealed record PreferenceExtraction(string Sentiment, string Topic);

    private sealed class PreferenceMemory
    {
        public List<string> Likes { get; set; } = [];
        public List<string> Dislikes { get; set; } = [];
    }
}
