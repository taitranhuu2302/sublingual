using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Sublingual.Domain.SpeakingPractice;
using Sublingual.Infrastructure.AI;

namespace Sublingual.Infrastructure.AI.Groq;

public sealed class GroqSpeakingTutorService : IAiTutorService
{
    private readonly HttpClient _http;
    private string _model = "qwen/qwen3-32b";
    private const int RetryHistoryTokenBudget = 1200;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public GroqSpeakingTutorService(HttpClient http)
    {
        _http = http;
    }

    public async Task<TutorResponse?> GetResponseAsync(
        string instructions,
        string languageLevel,
        IReadOnlyList<PracticeMessage> history,
        string? preferencesJson,
        CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(instructions, languageLevel, history, preferencesJson, null, cancellationToken);
        if (IsContextLimitStatus(response.StatusCode))
        {
            response.Dispose();
            response = await SendAsync(instructions, languageLevel, history, preferencesJson, RetryHistoryTokenBudget, cancellationToken);
        }

        using (response)
        {
            response.EnsureSuccessStatusCode();
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            return ParseTutorResponse(responseJson);
        }
    }

    public async Task<string> GetDirectCorrectionAsync(
        string sentence,
        CancellationToken cancellationToken = default)
    {
        var systemPrompt = SpeakingTutorPrompts.BuildDirectCorrectionSystemPrompt();
        var requestBody = new
        {
            model = _model,
            temperature = 0.3,
            max_tokens = 256,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = sentence }
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions");
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var root = JsonNode.Parse(responseJson);
        var result = root?["choices"]?[0]?["message"]?["content"]?.GetValue<string>();
        return result?.Trim() ?? sentence;
    }

    public void ConfigureApiKey(string apiKey)
    {
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", apiKey);
    }

    public void ConfigureModel(string model)
    {
        if (!string.IsNullOrWhiteSpace(model))
        {
            _model = model;
        }
    }

    private static object[] BuildMessages(
        string instructions,
        string languageLevel,
        IReadOnlyList<PracticeMessage> history,
        string? preferencesJson,
        int? historyTokenBudget)
    {
        var conversationStateJson = SpeakingConversationState.BuildJson(history, preferencesJson);
        var systemPrompt = SpeakingTutorPrompts.BuildTutorSystemPrompt(
            SpeakingTutorPrompts.TrimInstructions(instructions),
            languageLevel,
            conversationStateJson);
        var messages = new List<object>
        {
            new { role = "system", content = systemPrompt },
        };

        var budget = historyTokenBudget ?? SpeakingHistoryWindowing.DefaultHistoryTokenBudget;
        foreach (var msg in SpeakingHistoryWindowing.SelectRecentMessagesWithinBudget(history, budget))
        {
            var role = msg.Sender == MessageSender.User ? "user" : "assistant";
            messages.Add(new { role, content = msg.Text });
        }

        return [.. messages];
    }

    private async Task<HttpResponseMessage> SendAsync(
        string instructions,
        string languageLevel,
        IReadOnlyList<PracticeMessage> history,
        string? preferencesJson,
        int? historyTokenBudget,
        CancellationToken cancellationToken)
    {
        var messages = BuildMessages(instructions, languageLevel, history, preferencesJson, historyTokenBudget);
        var requestBody = new
        {
            model = _model,
            temperature = 0.7,
            max_tokens = 512,
            response_format = new { type = "json_object" },
            messages,
        };

        var json = JsonSerializer.Serialize(requestBody);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions");
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        return await _http.SendAsync(request, cancellationToken);
    }

    private static bool IsContextLimitStatus(System.Net.HttpStatusCode statusCode)
    {
        return statusCode == System.Net.HttpStatusCode.RequestEntityTooLarge
               || statusCode == System.Net.HttpStatusCode.BadRequest;
    }

    private static string CleanJsonString(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        
        // Strip any <think>...</think> or <thinking>...</thinking> tags and their contents
        input = System.Text.RegularExpressions.Regex.Replace(
            input, 
            @"<(?:think|thinking)\b[^>]*>.*?</(?:think|thinking)>", 
            string.Empty, 
            System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );
        
        var start = input.IndexOf('{');
        var end = input.LastIndexOf('}');
        if (start >= 0 && end > start)
        {
            return input.Substring(start, end - start + 1);
        }
        return input;
    }

    private static TutorResponse? ParseTutorResponse(string rawJson)
    {
        try
        {
            var root = JsonNode.Parse(rawJson);
            var content = root?["choices"]?[0]?["message"]?["content"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(content))
            {
                return null;
            }

            var cleaned = CleanJsonString(content);
            var parsed = JsonSerializer.Deserialize<TutorResponseDto>(cleaned, JsonOptions);
            if (parsed is null)
            {
                return null;
            }

            var suggestions = parsed.Suggestions?
                .Select(s => new SuggestionOption(s.Label ?? string.Empty, s.Text ?? string.Empty))
                .Where(s => !string.IsNullOrWhiteSpace(s.Text))
                .ToList()
                ?? [];

            return new TutorResponse(
                TutorReply: parsed.TutorReply ?? string.Empty,
                EnglishEnhancement: string.Empty,
                Suggestions: suggestions
            );
        }
        catch
        {
            return null;
        }
    }

    private sealed class TutorResponseDto
    {
        [JsonPropertyName("tutor_reply")]
        public string? TutorReply { get; init; }

        [JsonPropertyName("suggestions")]
        public List<SuggestionOptionDto>? Suggestions { get; init; }
    }

    private sealed class SuggestionOptionDto
    {
        public string? Label { get; init; }
        public string? Text { get; init; }
    }
}
