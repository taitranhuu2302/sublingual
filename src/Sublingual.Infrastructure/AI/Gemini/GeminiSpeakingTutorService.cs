using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Sublingual.Domain.SpeakingPractice;
using Sublingual.Infrastructure.AI;

namespace Sublingual.Infrastructure.AI.Gemini;

public sealed class GeminiSpeakingTutorService : IAiTutorService
{
    private readonly HttpClient _http;
    private string _apiKey = string.Empty;
    private string _model = "gemini-2.5-flash";
    private const int RetryHistoryTokenBudget = 1200;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public GeminiSpeakingTutorService(HttpClient http)
    {
        _http = http;
    }

    public void Configure(string apiKey, string model)
    {
        _apiKey = apiKey;
        _model = model;
    }

    public async Task<TutorResponse?> GetResponseAsync(
        string instructions,
        string languageLevel,
        IReadOnlyList<PracticeMessage> history,
        CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(instructions, languageLevel, history, null, cancellationToken);
        if (IsContextLimitStatus(response.StatusCode))
        {
            response.Dispose();
            response = await SendAsync(instructions, languageLevel, history, RetryHistoryTokenBudget, cancellationToken);
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
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";
        var systemInstruction = SpeakingTutorPrompts.BuildDirectCorrectionSystemPrompt();
        var requestBody = new
        {
            system_instruction = new { parts = new[] { new { text = systemInstruction } } },
            contents = new[] { new { role = "user", parts = new[] { new { text = sentence } } } },
            generation_config = new
            {
                temperature = 0.3,
                max_output_tokens = 256,
            },
        };

        var json = JsonSerializer.Serialize(requestBody);
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var root = JsonNode.Parse(responseJson);
        var result = root?["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.GetValue<string>();
        return result?.Trim() ?? sentence;
    }

    private static object[] BuildContents(IReadOnlyList<PracticeMessage> history, int? historyTokenBudget)
    {
        var contents = new List<object>();

        var budget = historyTokenBudget ?? SpeakingHistoryWindowing.DefaultHistoryTokenBudget;
        foreach (var msg in SpeakingHistoryWindowing.SelectRecentMessagesWithinBudget(history, budget))
        {
            var role = msg.Sender == MessageSender.User ? "user" : "model";
            contents.Add(new { role, parts = new[] { new { text = msg.Text } } });
        }

        return [.. contents];
    }

    private async Task<HttpResponseMessage> SendAsync(
        string instructions,
        string languageLevel,
        IReadOnlyList<PracticeMessage> history,
        int? historyTokenBudget,
        CancellationToken cancellationToken)
    {
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";
        var contents = BuildContents(history, historyTokenBudget);
        var systemInstruction = SpeakingTutorPrompts.BuildTutorSystemPrompt(
            SpeakingTutorPrompts.TrimInstructions(instructions),
            languageLevel);

        var requestBody = new
        {
            system_instruction = new { parts = new[] { new { text = systemInstruction } } },
            contents,
            generation_config = new
            {
                temperature = 0.7,
                max_output_tokens = 512,
                response_mime_type = "application/json",
            },
        };

        var json = JsonSerializer.Serialize(requestBody);
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
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
            var content = root?["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.GetValue<string>();
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
