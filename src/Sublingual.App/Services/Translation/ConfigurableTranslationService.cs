using Sublingual.App.Models;
using Sublingual.Domain.Transcription;

namespace Sublingual.App.Services.Translation;

public sealed class ConfigurableTranslationService(
    IEnumerable<ITranslationProvider> providers,
    AppSettingsStore settingsStore
) : ITranslationExecutionService
{
    private const int CacheLimit = 160;

    private readonly Dictionary<string, ITranslationProvider> _providers = providers.ToDictionary(
        provider => provider.Name,
        StringComparer.OrdinalIgnoreCase
    );
    private readonly Dictionary<string, TranslationExecutionResult> _cache = new(StringComparer.Ordinal);
    private readonly Queue<string> _cacheOrder = new();
    private readonly Lock _cacheLock = new();

    public void ClearCache()
    {
        lock (_cacheLock)
        {
            _cache.Clear();
            _cacheOrder.Clear();
        }
    }

    public async Task<TranslationResult> TranslateAsync(
        TranslationRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var execution = await TranslateWithDiagnosticsAsync(request, cancellationToken);
        return execution.Result;
    }

    public async Task<TranslationExecutionResult> TranslateWithDiagnosticsAsync(
        TranslationRequest request,
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
                var result = await provider.TranslateAsync(request, settings, cancellationToken);
                if (result is not null && !string.IsNullOrWhiteSpace(result.TranslatedText))
                {
                    var execution = new TranslationExecutionResult(
                        result,
                        provider.Name,
                        [.. attemptLog, $"{provider.Name}: success"],
                        false
                    );
                    Cache(request, execution);
                    return execution;
                }

                attemptLog.Add($"{provider.Name}: empty result");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
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
            request.SourceLanguage.Trim(),
            '|',
            request.TargetLanguage.Trim(),
            '|',
            request.SourceText.Trim()
        );
    }
}
