using Sublingual.App.Models;
using Microsoft.Extensions.Logging;
using Sublingual.App.Services.Logging;
using Sublingual.Domain.Transcription;

namespace Sublingual.App.Services.Translation;

public sealed class ConfigurableTranslationService(
    IEnumerable<ITranslationProvider> providers,
    AppSettingsStore settingsStore,
    ILogger<ConfigurableTranslationService>? logger = null
) : ITranslationExecutionService
{
    private const int CacheLimit = 160;

    private readonly Dictionary<string, ITranslationProvider> _providers = providers.ToDictionary(
        provider => provider.Name,
        StringComparer.OrdinalIgnoreCase
    );
    private readonly ILogger _logger = logger ?? AppLog.CreateLogger(nameof(ConfigurableTranslationService));
    private readonly Dictionary<string, TranslationExecutionResult> _cache = new(StringComparer.Ordinal);
    private readonly Queue<string> _cacheOrder = new();
    private readonly Lock _cacheLock = new();

    public event EventHandler<TranslateServiceLocalTranslationProvider.TranslationPartialEventArgs>? TranslationPartial
    {
        add
        {
            foreach (var provider in _providers.Values)
            {
                if (provider is TranslateServiceLocalTranslationProvider local)
                {
                    local.TranslationPartial += value;
                }
            }
        }
        remove
        {
            foreach (var provider in _providers.Values)
            {
                if (provider is TranslateServiceLocalTranslationProvider local)
                {
                    local.TranslationPartial -= value;
                }
            }
        }
    }

    public void ClearCache()
    {
        lock (_cacheLock)
        {
            _cache.Clear();
            _cacheOrder.Clear();
        }
    }

    public async Task ResetRealtimeProviderSessionAsync(
        TranslationSettings settings,
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return;
        }

        foreach (var provider in ResolveProviders(settings))
        {
            if (provider is IRealtimeTranslationProvider realtimeProvider)
            {
                try
                {
                    await realtimeProvider.ResetSessionAsync(settings, sessionId, cancellationToken);
                }
                catch
                {
                    return;
                }

                return;
            }
        }
    }

    public async Task<TranslationResult> TranslateAsync(
        TranslationRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var execution = await TranslateWithDiagnosticsAsync(request, null, cancellationToken);
        return execution.Result;
    }

    public async Task<TranslationExecutionResult> TranslateWithDiagnosticsAsync(
        TranslationRequest request,
        RealtimeTranslationContext? realtimeContext = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.SourceText))
        {
            return new TranslationExecutionResult(
                new TranslationResult(request.SourceText, string.Empty, request.TargetLanguage),
                "None",
                ["Skipped: empty source text"],
                false
            );
        }

        if (TryGetCached(request, out var cached))
        {
            return cached with
            {
                AttemptLog = cached.AttemptLog.Count == 0 ? ["Cache hit"] : cached.AttemptLog,
                IsCacheHit = true,
            };
        }

        var settings = settingsStore.Load().Translation;
        var attemptLog = new List<string>();
        foreach (var provider in ResolveProviders(settings))
        {
            try
            {
                var providerResponse = provider is IRealtimeTranslationProvider realtimeProvider
                    ? await realtimeProvider.TranslateWithMetadataAsync(request, settings, realtimeContext, cancellationToken)
                    : null;
                var result = providerResponse?.Result
                    ?? await provider.TranslateAsync(request, settings, realtimeContext, cancellationToken);
                var diagnostics = providerResponse?.Diagnostics ?? Array.Empty<string>();
                var isProviderCacheHit = providerResponse?.IsCacheHit ?? false;

                if (result is not null && !string.IsNullOrWhiteSpace(result.TranslatedText))
                {
                    var execution = new TranslationExecutionResult(
                        result,
                        provider.Name,
                        diagnostics.Count == 0 ? [.. attemptLog, $"{provider.Name}: success"] : [.. attemptLog, .. diagnostics],
                        isProviderCacheHit
                    );
                    Cache(request, execution);
                    return execution;
                }

                if (result is not null && realtimeContext?.Target == TranscriptTranslationTarget.Draft)
                {
                    return new TranslationExecutionResult(
                        result,
                        provider.Name,
                        diagnostics.Count == 0 ? [.. attemptLog, $"{provider.Name}: draft response"] : [.. attemptLog, .. diagnostics],
                        isProviderCacheHit
                    );
                }

                attemptLog.Add($"{provider.Name}: empty result");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Providers are best-effort; log once per failure for diagnosis.
                _logger.LogWarning(ex, "Translation provider failed. Provider={Provider}", provider.Name);
                attemptLog.Add($"{provider.Name}: {ex.Message}");
            }
        }

        var fallback = new TranslationExecutionResult(
            new TranslationResult(request.SourceText, request.SourceText, request.TargetLanguage),
            "FallbackOriginalText",
            attemptLog.Count == 0 ? ["Fallback: no enabled provider resolved"] : [.. attemptLog, "Fallback: original text returned"],
            false
        );
        Cache(request, fallback);
        return fallback;
    }

    private IReadOnlyList<ITranslationProvider> ResolveProviders(TranslationSettings settings)
    {
        var providerNames = string.Equals(
            settings.Factory,
            TranslationFactories.FallbackChain,
            StringComparison.OrdinalIgnoreCase
        )
            ? settings.ProviderOrder
            : [settings.Factory];

        var resolved = new List<ITranslationProvider>();
        foreach (var providerName in providerNames)
        {
            if (string.IsNullOrWhiteSpace(providerName))
            {
                continue;
            }

            if (_providers.TryGetValue(providerName, out var provider) && provider.IsEnabled(settings))
            {
                resolved.Add(provider);
            }
        }

        return resolved;
    }

    private bool TryGetCached(TranslationRequest request, out TranslationExecutionResult result)
    {
        var cacheKey = BuildCacheKey(request);
        lock (_cacheLock)
        {
            return _cache.TryGetValue(cacheKey, out result!);
        }
    }

    private void Cache(TranslationRequest request, TranslationExecutionResult result)
    {
        var cacheKey = BuildCacheKey(request);
        lock (_cacheLock)
        {
            if (_cache.TryAdd(cacheKey, result))
            {
                _cacheOrder.Enqueue(cacheKey);
            }
            else
            {
                _cache[cacheKey] = result;
            }

            while (_cacheOrder.Count > CacheLimit)
            {
                var oldestKey = _cacheOrder.Dequeue();
                _cache.Remove(oldestKey);
            }
        }
    }

    private static string BuildCacheKey(TranslationRequest request)
    {
        return string.Concat(
            NormalizeCacheToken(request.SourceLanguage),
            '|',
            NormalizeCacheToken(request.TargetLanguage),
            '|',
            NormalizeCacheToken(request.SourceText)
        );
    }

    private static string NormalizeCacheToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var lowered = value.ToLowerInvariant();
        return string.Join(' ', lowered
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .TrimEnd('.', ',', '!', '?', ';', ':');
    }
}
