using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Sublingual.App.Models;
using Sublingual.Domain.Transcription;

namespace Sublingual.App.Services.Translation;

public sealed class TranslateServiceLocalTranslationProvider : IRealtimeTranslationProvider, IDisposable
{
    private const int BatchMaxSize = 8;
    private const int MaxStableQueueCapacity = 100;
    private static readonly TimeSpan BatchFlushInterval = TimeSpan.FromMilliseconds(150);

    private readonly HttpClient _httpClient;
    private readonly Channel<BatchItem> _batchChannel;
    private readonly ILogger? _logger;
    private readonly CancellationTokenSource _disposeCts;
    private readonly Task _batchWorkerTask;
    private bool _disposed;

    public event EventHandler<TranslationPartialEventArgs>? TranslationPartial;

    public TranslateServiceLocalTranslationProvider(HttpClient httpClient, ILogger<TranslateServiceLocalTranslationProvider>? logger = null)
    {
        _httpClient = httpClient;
        _logger = logger;
        _disposeCts = new CancellationTokenSource();
        _batchChannel = Channel.CreateBounded<BatchItem>(new BoundedChannelOptions(MaxStableQueueCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
        });

        _batchWorkerTask = Task.Run(() => ProcessBatchQueueAsync(_disposeCts.Token));
    }

    public string Name => TranslationProviders.TranslateServiceLocal;

    public bool IsEnabled(TranslationSettings settings) => settings.TranslateServiceLocal.Enabled;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _disposeCts.Cancel();
        _batchChannel.Writer.TryComplete();
        try { _batchWorkerTask.Wait(); } catch { }
        _disposeCts.Dispose();
    }

    public async Task<ProviderTranslationResponse?> TranslateStandardDirectAsync(
        TranslationRequest request,
        TranslationSettings settings,
        CancellationToken cancellationToken = default
    )
    {
        var baseUrl = NormalizeBaseUrl(settings.TranslateServiceLocal.BaseUrl);
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return null;
        }

        using var content = new StringContent(
            JsonSerializer.Serialize(new TranslatePayload(
                request.SourceText,
                request.SourceLanguage,
                request.TargetLanguage,
                request.ContextBefore)),
            Encoding.UTF8,
            "application/json");

        using var response = await _httpClient.PostAsync(
            BuildEndpoint(baseUrl, "/translate"),
            content,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            using var errStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var errReader = new System.IO.StreamReader(errStream);
            var detail = await errReader.ReadToEndAsync(cancellationToken);
            _logger?.LogWarning("TranslateServiceLocal standard HTTP {Status}: {Body}",
                (int)response.StatusCode, detail);
            throw new HttpRequestException($"HTTP {(int)response.StatusCode}: {detail}", null, response.StatusCode);
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<TranslateResponsePayload>(stream, cancellationToken: cancellationToken);
        if (payload is null)
        {
            return new ProviderTranslationResponse(
                new TranslationResult(request.SourceText, string.Empty, request.TargetLanguage),
                [$"{Name}: null response"], false);
        }

        return !string.IsNullOrWhiteSpace(payload.TranslatedText)
            ? new ProviderTranslationResponse(
                new TranslationResult(request.SourceText, payload.TranslatedText, request.TargetLanguage),
                [$"{Name}: standard direct"], false)
            : null;
    }

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

        if (!useRealtimeEndpoint)
        {
            return await TranslateStandardAsync(request, baseUrl, cancellationToken);
        }

        return await TranslateRealtimeAsync(request, realtimeContext!, baseUrl, cancellationToken);
    }

    private async Task<ProviderTranslationResponse> TranslateStandardAsync(
        TranslationRequest request,
        string baseUrl,
        CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<ProviderTranslationResponse?>();
        var item = new BatchItem(
            request.SourceText,
            request.SourceLanguage,
            request.TargetLanguage,
            request.ContextBefore,
            tcs);

        await _batchChannel.Writer.WriteAsync(item, cancellationToken);

        var result = await tcs.Task.WaitAsync(cancellationToken);
        if (result is not null)
        {
            return result;
        }

        return new ProviderTranslationResponse(
            new TranslationResult(request.SourceText, string.Empty, request.TargetLanguage),
            [$"{Name}: empty batch response"],
            false);
    }

    private async Task<ProviderTranslationResponse> TranslateRealtimeAsync(
        TranslationRequest request,
        RealtimeTranslationContext realtimeContext,
        string baseUrl,
        CancellationToken cancellationToken)
    {
        var realtimePayload = new RealtimeTranslatePayload(
            request.SourceText,
            request.SourceLanguage,
            request.TargetLanguage,
            realtimeContext.IsFinal,
            realtimeContext.SessionId,
            realtimeContext.SegmentId,
            realtimeContext.SequenceId,
            realtimeContext.Target == TranscriptTranslationTarget.Draft ? "draft" : "stable",
            false,
            request.ContextBefore,
            realtimeContext.UseQualityModel);

        using var realtimeContent = new StringContent(
            JsonSerializer.Serialize(realtimePayload),
            Encoding.UTF8,
            "application/json");

        try
        {
            return await StreamTranslateAsync(realtimePayload, realtimeContext, baseUrl, realtimeContent, cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Fallback: server doesn't support streaming endpoint
        }

        return await FallbackTranslateAsync(request, realtimePayload, baseUrl, cancellationToken);
    }

    private async Task<ProviderTranslationResponse> StreamTranslateAsync(
        RealtimeTranslatePayload payload,
        RealtimeTranslationContext context,
        string baseUrl,
        StringContent content,
        CancellationToken ct)
    {
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, BuildEndpoint(baseUrl, "/translate/realtime/stream"))
        {
            Content = content,
        };

        using var response = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            using var errStream = await response.Content.ReadAsStreamAsync(ct);
            using var errReader = new System.IO.StreamReader(errStream);
            var detail = await errReader.ReadToEndAsync(ct);
            _logger?.LogWarning("TranslateServiceLocal /stream HTTP {Status}: {Body}",
                (int)response.StatusCode, detail);
            if ((int)response.StatusCode == 404)
            {
                throw new HttpRequestException("HTTP 404", null, response.StatusCode);
            }
            throw new HttpRequestException($"HTTP {(int)response.StatusCode}: {detail}", null, response.StatusCode);
        }

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        RealtimeTranslateResponsePayload? finalPayload = null;
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (!line.StartsWith("data: "))
                continue;

            var json = line[6..];
            var partialPayload = JsonSerializer.Deserialize<RealtimeTranslateResponsePayload>(json);
            if (partialPayload is null)
                continue;

            if (partialPayload.IsPartial)
            {
                FirePartial(context, partialPayload);
            }
            else
            {
                finalPayload = partialPayload;
            }
        }

        if (finalPayload is null)
        {
            return new ProviderTranslationResponse(
                new TranslationResult(payload.Text, string.Empty, payload.TargetLang),
                [$"{Name}: null streaming response"],
                false);
        }

        var diagnostics = finalPayload.WasSkipped
            ? new[] { $"{Name}: skipped ({finalPayload.SkipReason ?? "unknown"})" }
            : new[] { $"{Name}: success", $"{Name}: realtime kind={finalPayload.Kind} seq={finalPayload.SequenceId}" };

        return new ProviderTranslationResponse(
            new TranslationResult(payload.Text, finalPayload.TranslatedText ?? string.Empty, payload.TargetLang),
            diagnostics,
            finalPayload.CacheHit);
    }

    private async Task<ProviderTranslationResponse> FallbackTranslateAsync(
        TranslationRequest request,
        RealtimeTranslatePayload payload,
        string baseUrl,
        CancellationToken ct)
    {
        using var fallbackContent = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        using var response = await _httpClient.PostAsync(
            BuildEndpoint(baseUrl, "/translate/realtime"),
            fallbackContent,
            ct);
        if (!response.IsSuccessStatusCode)
        {
            using var errStream = await response.Content.ReadAsStreamAsync(ct);
            using var errReader = new System.IO.StreamReader(errStream);
            var detail = await errReader.ReadToEndAsync(ct);
            _logger?.LogWarning("TranslateServiceLocal /realtime HTTP {Status}: {Body}",
                (int)response.StatusCode, detail);
            throw new HttpRequestException($"HTTP {(int)response.StatusCode}: {detail}", null, response.StatusCode);
        }

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        var finalPayload = await JsonSerializer.DeserializeAsync<RealtimeTranslateResponsePayload>(stream, cancellationToken: ct);

        if (finalPayload is null)
        {
            return new ProviderTranslationResponse(
                new TranslationResult(request.SourceText, string.Empty, request.TargetLanguage),
                [$"{Name}: null realtime response"],
                false);
        }

        var diagnostics = finalPayload.WasSkipped
            ? new[] { $"{Name}: skipped ({finalPayload.SkipReason ?? "unknown"})" }
            : new[] { $"{Name}: success" };

        return new ProviderTranslationResponse(
            new TranslationResult(request.SourceText, finalPayload.TranslatedText ?? string.Empty, request.TargetLanguage),
            diagnostics,
            finalPayload.CacheHit);
    }

    private void FirePartial(RealtimeTranslationContext context, RealtimeTranslateResponsePayload payload)
    {
        if (string.IsNullOrWhiteSpace(payload.TranslatedText))
            return;

        TranslationPartial?.Invoke(this, new TranslationPartialEventArgs(
            context.SessionId,
            context.SegmentId,
            context.SequenceId,
            context.Target,
            payload.TranslatedText));
    }

    private async Task ProcessBatchQueueAsync(CancellationToken ct)
    {
        var batch = new List<BatchItem>();

        while (!ct.IsCancellationRequested)
        {
            batch.Clear();

            try
            {
                var first = await _batchChannel.Reader.ReadAsync(ct);
                batch.Add(first);

                var batchTimer = Task.Delay(BatchFlushInterval, ct);
                while (!batchTimer.IsCompleted)
                {
                    var waitTask = await Task.WhenAny(batchTimer, _batchChannel.Reader.WaitToReadAsync(ct).AsTask());
                    if (waitTask == batchTimer)
                    {
                        break;
                    }

                    while (_batchChannel.Reader.TryRead(out var item))
                    {
                        batch.Add(item);
                        if (batch.Count >= BatchMaxSize)
                        {
                            break;
                        }
                    }

                    if (batch.Count >= BatchMaxSize)
                    {
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ChannelClosedException)
            {
                return;
            }

            if (batch.Count == 0)
            {
                continue;
            }

            await FlushBatchAsync(batch, ct);
        }
    }

    private async Task FlushBatchAsync(List<BatchItem> batch, CancellationToken ct)
    {
        if (batch.Count == 1)
        {
            var single = batch[0];
            try
            {
                using var content = new StringContent(
                    JsonSerializer.Serialize(new TranslatePayload(
                        single.Text,
                        single.SourceLang,
                        single.TargetLang,
                        single.ContextBefore)),
                    Encoding.UTF8,
                    "application/json");

                using var response = await _httpClient.PostAsync(
                    BuildEndpoint(NormalizeBaseUrl(null), "/translate"),
                    content,
                    ct);
                if (!response.IsSuccessStatusCode)
                {
                    using var errStream = await response.Content.ReadAsStreamAsync(ct);
                    using var errReader = new System.IO.StreamReader(errStream);
                    var detail = await errReader.ReadToEndAsync(ct);
                    _logger?.LogWarning("TranslateServiceLocal batch single HTTP {Status}: {Body}",
                        (int)response.StatusCode, detail);
                    single.Tcs.TrySetException(new HttpRequestException($"HTTP {(int)response.StatusCode}: {detail}", null, response.StatusCode));
                    return;
                }

                using var stream = await response.Content.ReadAsStreamAsync(ct);
                var payload = await JsonSerializer.DeserializeAsync<TranslateResponsePayload>(stream, cancellationToken: ct);
                var result = !string.IsNullOrWhiteSpace(payload?.TranslatedText)
                    ? new TranslationResult(single.Text, payload.TranslatedText, single.TargetLang)
                    : null;

                single.Tcs.TrySetResult(result is not null
                    ? new ProviderTranslationResponse(result, [$"{Name}: success"], false)
                    : null);
            }
            catch (Exception ex)
            {
                single.Tcs.TrySetException(ex);
            }
            return;
        }

        try
        {
            var texts = batch.Select(item => item.Text).ToList();
            using var content = new StringContent(
                JsonSerializer.Serialize(new BatchTranslatePayload(
                    texts,
                    batch[0].SourceLang,
                    batch[0].TargetLang)),
                Encoding.UTF8,
                "application/json");

            using var response = await _httpClient.PostAsync(
                BuildEndpoint(NormalizeBaseUrl(null), "/translate/batch"),
                content,
                ct);
            if (!response.IsSuccessStatusCode)
            {
                using var errStream = await response.Content.ReadAsStreamAsync(ct);
                using var errReader = new System.IO.StreamReader(errStream);
                var detail = await errReader.ReadToEndAsync(ct);
                _logger?.LogWarning("TranslateServiceLocal batch HTTP {Status}: {Body}",
                    (int)response.StatusCode, detail);
                foreach (var item in batch)
                {
                    item.Tcs.TrySetException(new HttpRequestException($"HTTP {(int)response.StatusCode}: {detail}", null, response.StatusCode));
                }
                return;
            }

            using var stream = await response.Content.ReadAsStreamAsync(ct);
            var batchResponse = await JsonSerializer.DeserializeAsync<BatchTranslateResponsePayload>(stream, cancellationToken: ct);

            if (batchResponse?.Translations is null)
            {
                foreach (var item in batch)
                {
                    item.Tcs.TrySetResult(null);
                }
                return;
            }

            for (var i = 0; i < batch.Count && i < batchResponse.Translations.Count; i++)
            {
                var item = batch[i];
                var translation = batchResponse.Translations[i];
                var result = !string.IsNullOrWhiteSpace(translation.TranslatedText)
                    ? new TranslationResult(item.Text, translation.TranslatedText, item.TargetLang)
                    : null;

                item.Tcs.TrySetResult(result is not null
                    ? new ProviderTranslationResponse(result, [$"{Name}: batch"], false)
                    : null);
            }

            for (var i = batchResponse.Translations.Count; i < batch.Count; i++)
            {
                batch[i].Tcs.TrySetResult(null);
            }
        }
        catch (Exception ex)
        {
            foreach (var item in batch)
            {
                item.Tcs.TrySetException(ex);
            }
        }
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
            "application/json");

        using var response = await _httpClient.PostAsync(BuildEndpoint(baseUrl, "/translate/realtime/reset"), content, cancellationToken);
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

        [JsonPropertyName("context_before")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ContextBefore { get; init; }

        [JsonPropertyName("quality")]
        public bool Quality { get; init; }

        public TranslatePayload(string text, string sourceLang, string targetLang, string? contextBefore = null, bool quality = false)
        {
            Text = text;
            SourceLang = sourceLang;
            TargetLang = targetLang;
            ContextBefore = contextBefore;
            Quality = quality;
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

        [JsonPropertyName("context_before")]
        public string? ContextBefore { get; init; }

        [JsonPropertyName("quality")]
        public bool Quality { get; init; }

        public RealtimeTranslatePayload(
            string text,
            string sourceLang,
            string targetLang,
            bool isFinal,
            string sessionId,
            string segmentId,
            long sequenceId,
            string kind,
            bool force,
            string? contextBefore = null,
            bool quality = false)
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
            ContextBefore = contextBefore;
            Quality = quality;
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

        [JsonPropertyName("partial")]
        public bool IsPartial { get; set; }
    }

    public sealed record TranslationPartialEventArgs(
        string SessionId,
        string SegmentId,
        long SequenceId,
        TranscriptTranslationTarget Target,
        string PartialText
    );

    private sealed record BatchItem(
        string Text,
        string SourceLang,
        string TargetLang,
        string? ContextBefore,
        TaskCompletionSource<ProviderTranslationResponse?> Tcs
    );

    private sealed class BatchTranslatePayload
    {
        [JsonPropertyName("texts")]
        public List<string> Texts { get; init; }

        [JsonPropertyName("source_lang")]
        public string SourceLang { get; init; }

        [JsonPropertyName("target_lang")]
        public string TargetLang { get; init; }

        public BatchTranslatePayload(List<string> texts, string sourceLang, string targetLang)
        {
            Texts = texts;
            SourceLang = sourceLang;
            TargetLang = targetLang;
        }
    }

    private sealed class BatchTranslateResponsePayload
    {
        [JsonPropertyName("translations")]
        public List<BatchTranslationItemPayload>? Translations { get; set; }
    }

    private sealed class BatchTranslationItemPayload
    {
        [JsonPropertyName("source_text")]
        public string SourceText { get; set; } = string.Empty;

        [JsonPropertyName("translated_text")]
        public string TranslatedText { get; set; } = string.Empty;
    }
}
