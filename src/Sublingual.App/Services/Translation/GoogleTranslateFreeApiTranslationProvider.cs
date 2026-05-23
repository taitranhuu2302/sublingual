using System.Text;
using System.Text.Json;
using Sublingual.App.Models;
using Sublingual.Domain.Transcription;

namespace Sublingual.App.Services.Translation;

public sealed class GoogleTranslateFreeApiTranslationProvider(HttpClient httpClient) : ITranslationProvider
{
    public string Name => TranslationProviders.GoogleTranslateFreeApi;

    public bool IsEnabled(TranslationSettings settings) => settings.GoogleTranslateFreeApi.Enabled;

    public async Task<TranslationResult?> TranslateAsync(
        TranslationRequest request,
        TranslationSettings settings,
        RealtimeTranslationContext? realtimeContext = null,
        CancellationToken cancellationToken = default
    )
    {
        var endpoint = settings.GoogleTranslateFreeApi.Endpoint;
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return null;
        }

        var uri = BuildUri(endpoint, request);
        using var response = await httpClient.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() == 0)
        {
            return null;
        }

        var translated = new StringBuilder();
        foreach (var segment in document.RootElement[0].EnumerateArray())
        {
            if (segment.ValueKind != JsonValueKind.Array || segment.GetArrayLength() == 0)
            {
                continue;
            }

            var text = segment[0].GetString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                translated.Append(text);
            }
        }

        var translatedText = translated.ToString().Trim();
        return string.IsNullOrWhiteSpace(translatedText)
            ? null
            : new TranslationResult(request.SourceText, translatedText, request.TargetLanguage);
    }

    private static Uri BuildUri(string endpoint, TranslationRequest request)
    {
        var separator = endpoint.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        var query = string.Join(
            "&",
            [
                "client=gtx",
                $"sl={Uri.EscapeDataString(request.SourceLanguage)}",
                $"tl={Uri.EscapeDataString(request.TargetLanguage)}",
                "dt=t",
                $"q={Uri.EscapeDataString(request.SourceText)}",
            ]
        );

        return new Uri($"{endpoint}{separator}{query}", UriKind.Absolute);
    }
}
