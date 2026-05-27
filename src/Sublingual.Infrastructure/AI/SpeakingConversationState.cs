using System.Text.Json;
using Sublingual.Domain.SpeakingPractice;

namespace Sublingual.Infrastructure.AI;

public static class SpeakingConversationState
{
    private const int RecentTopicWindow = 6;
    private const int MaxTopicLength = 36;

    public static string BuildJson(IReadOnlyList<PracticeMessage> history, string? preferencesJson)
    {
        var lastAssistantAskedQuestion = LastAssistantAskedQuestion(history);
        var userEnergy = EstimateUserEnergy(history);
        var userEmotion = EstimateUserEmotion(history);
        var conversationDepth = EstimateConversationDepth(history);
        var recentTopics = ExtractRecentTopics(history);
        var preferenceMemory = SpeakingPreferenceMemory.BuildMemorySummary(preferencesJson);

        var payload = new
        {
            user_energy = userEnergy,
            user_emotion = userEmotion,
            conversation_depth = conversationDepth,
            last_turn_questioned = lastAssistantAskedQuestion,
            recent_topics = recentTopics,
            user_preferences = preferenceMemory,
        };

        return JsonSerializer.Serialize(payload);
    }

    private static bool LastAssistantAskedQuestion(IReadOnlyList<PracticeMessage> history)
    {
        for (var i = history.Count - 1; i >= 0; i--)
        {
            if (history[i].Sender == MessageSender.Ai)
            {
                return ContainsQuestion(history[i].Text);
            }
        }

        return false;
    }

    private static string EstimateUserEnergy(IReadOnlyList<PracticeMessage> history)
    {
        var lastUser = GetLastUserMessage(history);
        if (lastUser is null)
        {
            return "neutral";
        }

        var text = lastUser.Text.Trim();
        if (text.Length <= 6)
        {
            return "low";
        }

        if (CountWords(text) >= 18 || CountExclamations(text) >= 2)
        {
            return "high";
        }

        return "medium";
    }

    private static string EstimateUserEmotion(IReadOnlyList<PracticeMessage> history)
    {
        var lastUser = GetLastUserMessage(history);
        if (lastUser is null)
        {
            return "neutral";
        }

        var text = lastUser.Text.Trim();
        var lower = text.ToLowerInvariant();

        if (ContainsAny(lower, ["tired", "exhausted", "sleepy", "burned out", "overwhelmed", "stressed", "stress", "sad", "down", "upset"]))
        {
            return "tired_or_stressed";
        }

        if (ContainsAny(lower, ["excited", "happy", "great", "amazing", "love", "fun", "thrilled", "awesome"]))
        {
            return "excited";
        }

        if (ContainsAny(lower, ["angry", "mad", "frustrated", "annoyed", "irritated"]))
        {
            return "frustrated";
        }

        if (ContainsQuestion(text))
        {
            return "curious";
        }

        return "neutral";
    }

    private static string EstimateConversationDepth(IReadOnlyList<PracticeMessage> history)
    {
        if (history.Count < 4)
        {
            return "casual";
        }

        var userWordAverage = AverageUserWordCount(history, sample: 4);
        return userWordAverage >= 14 ? "deep" : "casual";
    }

    private static List<string> ExtractRecentTopics(IReadOnlyList<PracticeMessage> history)
    {
        var topics = new List<string>();
        for (var i = history.Count - 1; i >= 0 && topics.Count < RecentTopicWindow; i--)
        {
            var message = history[i];
            if (message.Sender != MessageSender.User)
            {
                continue;
            }

            var topic = ExtractTopic(message.Text);
            if (string.IsNullOrWhiteSpace(topic))
            {
                continue;
            }

            if (!topics.Any(t => string.Equals(t, topic, StringComparison.OrdinalIgnoreCase)))
            {
                topics.Add(topic);
            }
        }

        topics.Reverse();
        return topics;
    }

    private static string ExtractTopic(string text)
    {
        var cleaned = text.Trim();
        if (cleaned.Length == 0)
        {
            return string.Empty;
        }

        var punctuation = new[] { '.', '!', '?', ';' };
        var stop = cleaned.IndexOfAny(punctuation);
        if (stop > 0)
        {
            cleaned = cleaned[..stop];
        }

        cleaned = cleaned.Trim().Trim('"', '\'', '“', '”');
        if (cleaned.Length > MaxTopicLength)
        {
            cleaned = cleaned[..MaxTopicLength].TrimEnd();
        }

        return cleaned;
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

    private static bool ContainsQuestion(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return text.Contains('?') || text.TrimEnd().EndsWith("?", StringComparison.Ordinal);
    }

    private static int CountWords(string text)
    {
        return text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;
    }

    private static int CountExclamations(string text)
    {
        var count = 0;
        foreach (var ch in text)
        {
            if (ch == '!') count++;
        }

        return count;
    }

    private static bool ContainsAny(string text, IReadOnlyList<string> keywords)
    {
        foreach (var keyword in keywords)
        {
            if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static int AverageUserWordCount(IReadOnlyList<PracticeMessage> history, int sample)
    {
        var total = 0;
        var count = 0;
        for (var i = history.Count - 1; i >= 0 && count < sample; i--)
        {
            if (history[i].Sender != MessageSender.User)
            {
                continue;
            }

            total += CountWords(history[i].Text ?? string.Empty);
            count++;
        }

        if (count == 0)
        {
            return 0;
        }

        return total / count;
    }
}
