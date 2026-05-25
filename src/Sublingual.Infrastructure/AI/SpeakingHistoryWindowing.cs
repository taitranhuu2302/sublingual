using Sublingual.Domain.SpeakingPractice;

namespace Sublingual.Infrastructure.AI;

internal static class SpeakingHistoryWindowing
{
    // Conservative approximation for mixed-language chat text.
    // We over-estimate to stay below provider context limits.
    private const int CharactersPerTokenEstimate = 3;

    internal const int DefaultHistoryTokenBudget = 2400;
    internal const int MaxMessageCharacters = 900;

    public static IReadOnlyList<PracticeMessage> SelectRecentMessagesWithinBudget(
        IReadOnlyList<PracticeMessage> history,
        int tokenBudget = DefaultHistoryTokenBudget)
    {
        if (history.Count == 0 || tokenBudget <= 0)
        {
            return [];
        }

        var selected = new List<PracticeMessage>();
        var usedTokens = 0;

        for (var i = history.Count - 1; i >= 0; i--)
        {
            var item = history[i];
            var truncatedText = TruncateForContext(item.Text, MaxMessageCharacters);
            var estimatedTokens = EstimateTokens(truncatedText) + 8; // role + json overhead

            if (selected.Count > 0 && usedTokens + estimatedTokens > tokenBudget)
            {
                break;
            }

            usedTokens += estimatedTokens;
            selected.Add(item with { Text = truncatedText });
        }

        selected.Reverse();
        return selected;
    }

    private static int EstimateTokens(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 1;
        }

        return Math.Max(1, (int)Math.Ceiling(text.Length / (double)CharactersPerTokenEstimate));
    }

    private static string TruncateForContext(string text, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length <= maxChars)
        {
            return text;
        }

        return text[..maxChars].TrimEnd() + "...";
    }
}
