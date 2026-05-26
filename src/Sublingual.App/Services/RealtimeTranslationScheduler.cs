using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Sublingual.App.Services.Logging;
using Sublingual.App.Services.Translation;
using Sublingual.Domain.Transcription;

namespace Sublingual.App.Services;

public sealed class RealtimeTranslationScheduler : IDisposable
{
    private static readonly TimeSpan DraftDebounce = TimeSpan.FromMilliseconds(300);

    private readonly ITranslationExecutionService _translationService;
    private readonly ILogger _logger;
    private readonly ConcurrentQueue<StableTranslationRequest> _stableQueue = new();
    private readonly SemaphoreSlim _stableSignal = new(0);
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly Task _stableWorkerTask;
    private readonly object _draftLock = new();

    private DraftTranslationRequest? _latestDraft;
    private CancellationTokenSource? _draftRequestCts;
    private Task? _draftWorkerTask;
    private bool _disposed;

    public RealtimeTranslationScheduler(ITranslationExecutionService translationService, ILogger<RealtimeTranslationScheduler>? logger = null)
    {
        _translationService = translationService;
        _logger = logger ?? AppLog.CreateLogger(nameof(RealtimeTranslationScheduler));
        _stableWorkerTask = Task.Run(ProcessStableQueueAsync);
    }

    public event EventHandler<RealtimeTranslationCompleted>? TranslationCompleted;

    public void EnqueueDraft(DraftTranslationRequest request)
    {
        _logger.LogDebug(
            "Enqueue draft translation. Session={SessionId} Segment={SegmentId} Seq={Seq} Final={IsFinal} Len={Len}",
            request.SessionId,
            request.SegmentId,
            request.SequenceId,
            request.IsFinal,
            request.SourceText?.Length ?? 0);

        lock (_draftLock)
        {
            _latestDraft = request;
            _draftRequestCts?.Cancel();
            _draftRequestCts?.Dispose();
            _draftRequestCts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCts.Token);

            if (_draftWorkerTask is null || _draftWorkerTask.IsCompleted)
            {
                _draftWorkerTask = Task.Run(() => ProcessDraftAsync(_draftRequestCts.Token));
            }
        }
    }

    public void EnqueueStable(StableTranslationRequest request)
    {
        _logger.LogInformation(
            "Enqueue stable translation. Session={SessionId} Segment={SegmentId} Seq={Seq} Len={Len}",
            request.SessionId,
            request.SegmentId,
            request.SequenceId,
            request.SourceText?.Length ?? 0);

        _stableQueue.Enqueue(request);
        _stableSignal.Release();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _logger.LogInformation("Disposing translation scheduler");

        _disposeCts.Cancel();

        lock (_draftLock)
        {
            _draftRequestCts?.Cancel();
            _draftRequestCts?.Dispose();
            _draftRequestCts = null;
        }

        try
        {
            _draftWorkerTask?.Wait();
            _stableSignal.Release();
            _stableWorkerTask.Wait();
        }
        catch (AggregateException ex) when (ex.InnerExceptions.All(inner => inner is OperationCanceledException))
        {
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _stableSignal.Dispose();
            _disposeCts.Dispose();
            _disposed = true;
        }
    }

    private async Task ProcessDraftAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            DraftTranslationRequest? request;
            lock (_draftLock)
            {
                request = _latestDraft;
            }

            if (request is null)
            {
                return;
            }

            await Task.Delay(DraftDebounce, cancellationToken);

            lock (_draftLock)
            {
                if (!ReferenceEquals(_latestDraft, request))
                {
                    continue;
                }

                _latestDraft = null;
            }

            var result = await TranslateAsync(
                request.SessionGeneration,
                request.SessionId,
                request.SegmentId,
                request.SequenceId,
                TranscriptTranslationTarget.Draft,
                request.SourceText,
                request.SourceLanguage,
                request.TargetLanguage,
                request.IsFinal,
                cancellationToken);
            if (result is not null)
            {
                _logger.LogDebug(
                    "Draft translated. Provider={Provider} Cache={CacheHit} Session={SessionId} Segment={SegmentId}",
                    result.ProviderName,
                    result.IsCacheHit,
                    result.SessionId,
                    result.SegmentId);
                TranslationCompleted?.Invoke(this, result);
            }
        }
    }

    private async Task ProcessStableQueueAsync()
    {
        while (!_disposeCts.IsCancellationRequested)
        {
            await _stableSignal.WaitAsync(_disposeCts.Token);

            while (_stableQueue.TryDequeue(out var request))
            {
                var result = await TranslateAsync(
                    request.SessionGeneration,
                    request.SessionId,
                    request.SegmentId,
                    request.SequenceId,
                    TranscriptTranslationTarget.StableSegment,
                    request.SourceText,
                    request.SourceLanguage,
                    request.TargetLanguage,
                    request.IsFinal,
                    _disposeCts.Token);
                if (result is not null)
                {
                    _logger.LogInformation(
                        "Stable translated. Provider={Provider} Cache={CacheHit} Session={SessionId} Segment={SegmentId}",
                        result.ProviderName,
                        result.IsCacheHit,
                        result.SessionId,
                        result.SegmentId);
                    TranslationCompleted?.Invoke(this, result);
                }
            }
        }
    }

    private async Task<RealtimeTranslationCompleted?> TranslateAsync(
        long sessionGeneration,
        string sessionId,
        string segmentId,
        long sequenceId,
        TranscriptTranslationTarget target,
        string sourceText,
        string sourceLanguage,
        string targetLanguage,
        bool isFinal,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sourceText))
        {
            return null;
        }

        if (string.Equals(sourceLanguage, targetLanguage, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug(
                "Skip translation (same language). Session={SessionId} Segment={SegmentId} Lang={Lang}",
                sessionId,
                segmentId,
                sourceLanguage);
            return new RealtimeTranslationCompleted(
                sessionGeneration,
                sessionId,
                segmentId,
                sequenceId,
                target,
                sourceText,
                string.Empty,
                "SkippedSameLanguage",
                false,
                DateTimeOffset.Now,
                ["Skipped: source and target languages match"]);
        }

        var translation = await _translationService.TranslateWithDiagnosticsAsync(
            new TranslationRequest(sourceText, sourceLanguage, targetLanguage),
            new RealtimeTranslationContext(sessionId, segmentId, sequenceId, target, isFinal),
            cancellationToken);

        if (translation.ProviderName == "FallbackOriginalText")
        {
            _logger.LogWarning(
                "Translation fallback used. Session={SessionId} Segment={SegmentId} Attempts={Attempts}",
                sessionId,
                segmentId,
                translation.AttemptLog.Count);
        }

        return new RealtimeTranslationCompleted(
            sessionGeneration,
            sessionId,
            segmentId,
            sequenceId,
            target,
            sourceText,
            translation.Result.TranslatedText,
            translation.ProviderName,
            translation.IsCacheHit,
            DateTimeOffset.Now,
            translation.AttemptLog);
    }
}

public sealed record DraftTranslationRequest(
    long SessionGeneration,
    string SessionId,
    string SegmentId,
    long SequenceId,
    string SourceText,
    string SourceLanguage,
    string TargetLanguage,
    bool IsFinal
);

public sealed record StableTranslationRequest(
    long SessionGeneration,
    string SessionId,
    string SegmentId,
    long SequenceId,
    string SourceText,
    string SourceLanguage,
    string TargetLanguage,
    bool IsFinal
);

public sealed record RealtimeTranslationCompleted(
    long SessionGeneration,
    string SessionId,
    string SegmentId,
    long SequenceId,
    TranscriptTranslationTarget Target,
    string SourceText,
    string TranslatedText,
    string ProviderName,
    bool IsCacheHit,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<string> AttemptLog
);
