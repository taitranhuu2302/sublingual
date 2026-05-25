using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Sublingual.Domain.SpeakingPractice;
using Sublingual.Infrastructure.AI;

namespace Sublingual.Infrastructure.AI.Groq;

public sealed class GroqSpeakingTutorService : IAiTutorService
{
    private readonly HttpClient _http;
    private string _model = "llama-3.3-70b-versatile";
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
        int? historyTokenBudget)
    {
        var systemPrompt = BuildSystemPrompt(TrimInstructions(instructions), languageLevel);
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

    private static string BuildSystemPrompt(string instructions, string languageLevel) => $$"""
        You are a highly professional, encouraging, and strict English Language Tutor.
        The user's room instructions are: '{{instructions}}'.
        The user's language level is: '{{languageLevel}}'.

        You MUST strictly follow ALL of these rules — no exceptions:
        1. ROOM INSTRUCTIONS: Treat the room instructions as the main conversation contract. If they define a topic, role, vocabulary list, or speaking style, stay aligned with them for the whole conversation.
        2. FALLBACK BEHAVIOR: If the room instructions are broad or effectively empty, have a warm daily conversation. Greet the user, ask about their day, work, feelings, family life, or everyday concerns, and offer gentle advice when it feels natural.
        3. SPOKEN STYLE: Keep your 'tutor_reply' natural, warm, and conversational. Exactly 2 to 3 sentences maximum.
        4. ENGAGEMENT: Always end your reply with an open-ended question that matches the room instructions or the fallback daily-conversation mode.
        5. CONSTRUCTIVE ENHANCEMENT: Analyze the user's latest message carefully.
           - If they made a grammar, vocabulary, word-choice, or collocation error: provide a gentle correction in 'english_enhancement'. Example: "Great try! Instead of 'I am agree', say 'I agree' — no verb needed."
           - If their sentence was perfect: leave 'english_enhancement' as an empty string "".
        6. SUGGESTIONS: Generate exactly 3 distinct and natural next-turn options in 'suggestions':
           - Option 1 label "Direct Reply": short, simple sentence.
           - Option 2 label "Elaborate": expanded with a reason or detail.
           - Option 3 label "Ask Back": a follow-up question to keep the conversation going.
        7. OUTPUT FORMAT: You MUST respond ONLY with this exact JSON structure. No extra text outside the JSON:
        {
          "tutor_reply": "...",
          "english_enhancement": "...",
          "suggestions": [
            { "label": "Direct Reply", "text": "..." },
            { "label": "Elaborate", "text": "..." },
            { "label": "Ask Back", "text": "..." }
          ]
        }
        """;

    private static string TrimInstructions(string instructions)
    {
        if (string.IsNullOrWhiteSpace(instructions))
        {
            return string.Empty;
        }

        const int maxChars = 1200;
        return instructions.Length <= maxChars
            ? instructions
            : instructions[..maxChars].TrimEnd() + "...";
    }

    private async Task<HttpResponseMessage> SendAsync(
        string instructions,
        string languageLevel,
        IReadOnlyList<PracticeMessage> history,
        int? historyTokenBudget,
        CancellationToken cancellationToken)
    {
        var messages = BuildMessages(instructions, languageLevel, history, historyTokenBudget);
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

            var parsed = JsonSerializer.Deserialize<TutorResponseDto>(content, JsonOptions);
            if (parsed is null)
            {
                return null;
            }

            var suggestions = parsed.Suggestions?
                .Select(s => new SuggestionOption(s.Label ?? string.Empty, s.Text ?? string.Empty))
                .ToList()
                ?? [];

            return new TutorResponse(
                TutorReply: parsed.TutorReply ?? string.Empty,
                EnglishEnhancement: parsed.EnglishEnhancement ?? string.Empty,
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
        public string? TutorReply { get; init; }
        public string? EnglishEnhancement { get; init; }
        public List<SuggestionOptionDto>? Suggestions { get; init; }
    }

    private sealed class SuggestionOptionDto
    {
        public string? Label { get; init; }
        public string? Text { get; init; }
    }
}
