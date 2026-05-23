using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Sublingual.Domain.SpeakingPractice;

namespace Sublingual.Infrastructure.AI.Gemini;

public sealed class GeminiSpeakingTutorService : IAiTutorService
{
    private readonly HttpClient _http;
    private string _apiKey = string.Empty;
    private string _model = "gemini-2.5-flash";

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
        string topic,
        string languageLevel,
        IReadOnlyList<PracticeMessage> history,
        string userText,
        CancellationToken cancellationToken = default)
    {
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";

        var contents = BuildContents(history, userText);
        var systemInstruction = BuildSystemPrompt(topic, languageLevel);

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

        using var response = await _http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseTutorResponse(responseJson);
    }

    private static object[] BuildContents(IReadOnlyList<PracticeMessage> history, string userText)
    {
        var contents = new List<object>();

        foreach (var msg in history.TakeLast(14))
        {
            var role = msg.Sender == MessageSender.User ? "user" : "model";
            contents.Add(new { role, parts = new[] { new { text = msg.Text } } });
        }

        contents.Add(new { role = "user", parts = new[] { new { text = userText } } });

        return [.. contents];
    }

    private static string BuildSystemPrompt(string topic, string languageLevel) => $$"""
        You are a highly professional, encouraging, and strict English Language Tutor.
        The user is practicing speaking on the topic: '{{topic}}' at language level: '{{languageLevel}}'.

        You MUST strictly follow ALL of these rules — no exceptions:
        1. TOPIC ADHERENCE: Stay 100% focused on the topic '{{topic}}'. Never deviate under any circumstances.
        2. SPOKEN STYLE: Keep your 'tutor_reply' natural, warm, and conversational. Exactly 2 to 3 sentences maximum.
        3. ENGAGEMENT: Always end your reply with an open-ended question related to the topic.
        4. CONSTRUCTIVE ENHANCEMENT: Analyze the user's latest message carefully.
           - If they made a grammar, vocabulary, word-choice, or collocation error: provide a gentle correction in 'english_enhancement'. Example: "Great try! Instead of 'I am agree', say 'I agree' — no verb needed."
           - If their sentence was perfect: leave 'english_enhancement' as an empty string "".
        5. SUGGESTIONS: Generate exactly 3 distinct and natural next-turn options in 'suggestions':
           - Option 1 label "Direct Reply": short, simple sentence.
           - Option 2 label "Elaborate": expanded with a reason or detail.
           - Option 3 label "Ask Back": a follow-up question to keep the conversation going.
        6. OUTPUT FORMAT: Respond ONLY with this exact JSON structure:
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
