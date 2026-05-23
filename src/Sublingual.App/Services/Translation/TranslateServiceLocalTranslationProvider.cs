using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sublingual.App.Models;
using Sublingual.Domain.Transcription;

namespace Sublingual.App.Services.Translation;

public sealed class TranslateServiceLocalTranslationProvider(HttpClient httpClient) : IRealtimeTranslationProvider
{
    public string Name => TranslationProviders.TranslateServiceLocal;

    public bool IsEnabled(TranslationSettings settings) => settings.TranslateServiceLocal.Enabled;

    public async Task<TranslationResult?> TranslateAsync(
        TranslationRequest request,
        TranslationSettings settings,
        RealtimeTranslationContext? realtimeContext = null,
        CancellationToken cancellationToken = default
    )
    {
        var response = await TranslateWithMetadataAsync(request, settings, realtimeContext, cancellationToken);
        return response?.Result;
    }

    public async Task<ProviderTranslationResponse?> TranslateWithMetadataAsync(
        TranslationRequest request,
        TranslationSettings settings,
        RealtimeTranslationContext? realtimeContext = null,
        CancellationToken cancellationToken = default
    )
    {
        var baseUrl = NormalizeBaseUrl(settings.TranslateServiceLocal.BaseUrl);
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return null;
        }

        var useRealtimeEndpoint = realtimeContext is not null
            && (realtimeContext.Target == TranscriptTranslationTarget.Draft
                || settings.TranslateServiceLocal.UseRealtimeEndpointForFinals);

        var endpoint = useRealtimeEndpoint
            ? BuildEndpoint(baseUrl, "/translate/realtime")
            : BuildEndpoint(baseUrl, "/translate");

        if (!useRealtimeEndpoint)
        {
            using var content = new StringContent(
                JsonSerializer.Serialize(new TranslatePayload(
                    request.SourceText,
                    request.SourceLanguage,
                    request.TargetLanguage
                )),
                Encoding.UTF8,
                "application/json"
            );

            using var response = await httpClient.PostAsync(endpoint, content, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var payload = await JsonSerializer.DeserializeAsync<TranslateResponsePayload>(stream, cancellationToken: cancellationToken);
            return string.IsNullOrWhiteSpace(payload?.TranslatedText)
                ? null
                : new ProviderTranslationResponse(
                    new TranslationResult(request.SourceText, payload.TranslatedText, request.TargetLanguage),
                    [$"{Name}: success"],
                    false
                );
        }

        var realtimePayload = new RealtimeTranslatePayload(
            request.SourceText,
            request.SourceLanguage,
            request.TargetLanguage,
            realtimeContext!.IsFinal,
            realtimeContext.SessionId,
            realtimeContext.SegmentId,
            realtimeContext.SequenceId,
            realtimeContext.Target == TranscriptTranslationTarget.Draft ? "draft" : "stable",
            false
        );

        using var realtimeContent = new StringContent(
            JsonSerializer.Serialize(realtimePayload),
            Encoding.UTF8,
            "application/json"
        );

        using var realtimeResponse = await httpClient.PostAsync(endpoint, realtimeContent, cancellationToken);
        realtimeResponse.EnsureSuccessStatusCode();

        await using var realtimeStream = await realtimeResponse.Content.ReadAsStreamAsync(cancellationToken);
        var payloadRealtime = await JsonSerializer.DeserializeAsync<RealtimeTranslateResponsePayload>(realtimeStream, cancellationToken: cancellationToken);
        if (payloadRealtime is null)
        {
            return null;
        }

        var diagnostics = payloadRealtime.WasSkipped
            ? new[] { $"{Name}: skipped ({payloadRealtime.SkipReason ?? "unknown"})" }
            : new[] { $"{Name}: success", $"{Name}: realtime kind={payloadRealtime.Kind} seq={payloadRealtime.SequenceId}" };

        return new ProviderTranslationResponse(
            new TranslationResult(request.SourceText, payloadRealtime.TranslatedText ?? string.Empty, request.TargetLanguage),
            diagnostics,
            payloadRealtime.CacheHit
        );
    }

    public async Task ResetSessionAsync(TranslationSettings settings, string sessionId, CancellationToken cancellationToken = default)
    {
        var baseUrl = NormalizeBaseUrl(settings.TranslateServiceLocal.BaseUrl);
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(sessionId))
        {
            return;
        }

        using var content = new StringContent(
            JsonSerializer.Serialize(new RealtimeSessionResetPayload(sessionId)),
            Encoding.UTF8,
            "application/json"
        );

        using var response = await httpClient.PostAsync(BuildEndpoint(baseUrl, "/translate/realtime/reset"), content, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private static string BuildEndpoint(string baseUrl, string path)
    {
        return $"{baseUrl.TrimEnd('/')}{path}";
    }

    public static string NormalizeBaseUrl(string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return "http://127.0.0.1:3333";
        }

        var trimmed = baseUrl.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return trimmed;
        }

        if (!string.Equals(uri.Host, "0.0.0.0", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(uri.Host, "::", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(uri.Host, "[::]", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        var builder = new UriBuilder(uri)
        {
            Host = "127.0.0.1",
        };
        return builder.Uri.ToString().TrimEnd('/');
    }

    private sealed class TranslatePayload
    {
        [JsonPropertyName("text")]
        public string Text { get; init; }

        [JsonPropertyName("source_lang")]
        public string SourceLang { get; init; }

        [JsonPropertyName("target_lang")]
        public string TargetLang { get; init; }

        public TranslatePayload(string text, string sourceLang, string targetLang)
        {
            Text = text;
            SourceLang = sourceLang;
            TargetLang = targetLang;
        }
    }

    private sealed class RealtimeTranslatePayload
    {
        [JsonPropertyName("text")]
        public string Text { get; init; }

        [JsonPropertyName("source_lang")]
        public string SourceLang { get; init; }

        [JsonPropertyName("target_lang")]
        public string TargetLang { get; init; }

        [JsonPropertyName("is_final")]
        public bool IsFinal { get; init; }

        [JsonPropertyName("session_id")]
        public string SessionId { get; init; }

        [JsonPropertyName("segment_id")]
        public string SegmentId { get; init; }

        [JsonPropertyName("sequence_id")]
        public long SequenceId { get; init; }

        [JsonPropertyName("kind")]
        public string Kind { get; init; }

        [JsonPropertyName("force")]
        public bool Force { get; init; }

        public RealtimeTranslatePayload(
            string text,
            string sourceLang,
            string targetLang,
            bool isFinal,
            string sessionId,
            string segmentId,
            long sequenceId,
            string kind,
            bool force)
        {
            Text = text;
            SourceLang = sourceLang;
            TargetLang = targetLang;
            IsFinal = isFinal;
            SessionId = sessionId;
            SegmentId = segmentId;
            SequenceId = sequenceId;
            Kind = kind;
            Force = force;
        }
    }

    private sealed class RealtimeSessionResetPayload
    {
        [JsonPropertyName("session_id")]
        public string SessionId { get; init; }

        public RealtimeSessionResetPayload(string sessionId)
        {
            SessionId = sessionId;
        }
    }

    private sealed class TranslateResponsePayload
    {
        [JsonPropertyName("translated_text")]
        public string TranslatedText { get; set; } = string.Empty;
    }

    private sealed class RealtimeTranslateResponsePayload
    {
        [JsonPropertyName("translated_text")]
        public string TranslatedText { get; set; } = string.Empty;

        [JsonPropertyName("was_skipped")]
        public bool WasSkipped { get; set; }

        [JsonPropertyName("skip_reason")]
        public string? SkipReason { get; set; }

        [JsonPropertyName("kind")]
        public string Kind { get; set; } = string.Empty;

        [JsonPropertyName("sequence_id")]
        public long SequenceId { get; set; }

        [JsonPropertyName("cache_hit")]
        public bool CacheHit { get; set; }
    }
}
