using System.Text;
using System.Text.Json;
using Sublingual.App.Models;
using Sublingual.Domain.Transcription;

namespace Sublingual.App.Services.Translation;

public sealed class LibreTranslateTranslationProvider(HttpClient httpClient) : ITranslationProvider
{
    public string Name => TranslationProviders.LibreTranslate;

    public bool IsEnabled(TranslationSettings settings) => settings.LibreTranslate.Enabled;

    public async Task<TranslationResult?> TranslateAsync(
        TranslationRequest request,
        TranslationSettings settings,
        CancellationToken cancellationToken = default
    )
    {
        var endpoint = settings.LibreTranslate.Endpoint;
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return null;
        }

        using var content = new StringContent(
            JsonSerializer.Serialize(new LibreTranslatePayload(
                request.SourceText,
                request.SourceLanguage,
                request.TargetLanguage,
                "text",
                string.IsNullOrWhiteSpace(settings.LibreTranslate.ApiKey) ? null : settings.LibreTranslate.ApiKey
            )),
            Encoding.UTF8,
            "application/json"
        );

        using var response = await httpClient.PostAsync(endpoint, content, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<LibreTranslateResponse>(stream, cancellationToken: cancellationToken);
        if (string.IsNullOrWhiteSpace(payload?.TranslatedText))
        {
            return null;
        }

        return new TranslationResult(request.SourceText, payload.TranslatedText, request.TargetLanguage);
    }

    private sealed record LibreTranslatePayload(
        string Q,
        string Source,
        string Target,
        string Format,
        string? ApiKey
    );

    private sealed class LibreTranslateResponse
    {
        public string TranslatedText { get; set; } = string.Empty;
    }
}
