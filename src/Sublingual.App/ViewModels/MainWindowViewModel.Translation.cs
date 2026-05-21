using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sublingual.App.Models;
using Sublingual.App.Services.Translation;
using Sublingual.Domain.Transcription;

namespace Sublingual.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    [RelayCommand(CanExecute = nameof(CanTestTranslation))]
    private async Task TestTranslationAsync()
    {
        if (_translationService is null)
        {
            TranslationTestError = "Translation service is not available.";
            TranslationTestResult = string.Empty;
            return;
        }

        try
        {
            IsTestingTranslation = true;
            TranslationTestError = string.Empty;
            TranslationTestResult = string.Empty;

            var sourceLanguage = string.IsNullOrWhiteSpace(TranslationTestSourceLanguage)
                ? "en"
                : TranslationTestSourceLanguage.Trim();
            var targetLanguage = string.IsNullOrWhiteSpace(TranslationTestTargetLanguage)
                ? "vi"
                : TranslationTestTargetLanguage.Trim();

            var result = await _translationService.TranslateAsync(
                new TranslationRequest(TranslationTestSourceText.Trim(), sourceLanguage, targetLanguage)
            );

            TranslationTestResult = result.TranslatedText;
            TranslationTestError = string.Empty;
        }
        catch (Exception ex)
        {
            TranslationTestResult = string.Empty;
            TranslationTestError = ex.Message;
        }
        finally
        {
            IsTestingTranslation = false;
            TestTranslationCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(HasTranslationTestResult)));
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(HasTranslationTestError)));
        }
    }

    private void LoadTranslationSettings()
    {
        var appSettings = _settingsStore.Load();
        var settings = appSettings.Translation;

        _suspendStorageSettingsSave = true;
        SessionsDirectoryPath = _sessionStorage.GetSessionsRoot();
        SpeechToTextModelsDirectoryPath = _modelCatalog.GetManagedModelsRoot();
        SessionFolderId = _sessionStorage.GetPreferredFolderId();
        _suspendStorageSettingsSave = false;
        LoadSpeechToTextSettings();

        SelectedTranslationFactory = NormalizeTranslationFactory(settings.Factory);

        var providerOrder = settings.ProviderOrder
            .Where(static provider => !string.IsNullOrWhiteSpace(provider))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        TranslationPrimaryProvider = NormalizeTranslationProvider(
            providerOrder.ElementAtOrDefault(0),
            TranslationProviders.GoogleTranslateFreeApi
        );

        TranslationSecondaryProvider = NormalizeTranslationProvider(
            providerOrder.FirstOrDefault(provider =>
                !string.Equals(provider, TranslationPrimaryProvider, StringComparison.OrdinalIgnoreCase)),
            string.Equals(TranslationPrimaryProvider, TranslationProviders.GoogleTranslateFreeApi, StringComparison.OrdinalIgnoreCase)
                ? TranslationProviders.LibreTranslate
                : TranslationProviders.GoogleTranslateFreeApi
        );

        GoogleTranslateFreeApiEnabled = settings.GoogleTranslateFreeApi.Enabled;
        GoogleTranslateFreeApiEndpoint = string.IsNullOrWhiteSpace(settings.GoogleTranslateFreeApi.Endpoint)
            ? "https://translate.googleapis.com/translate_a/single"
            : settings.GoogleTranslateFreeApi.Endpoint;

        LibreTranslateEnabled = settings.LibreTranslate.Enabled;
        LibreTranslateEndpoint = string.IsNullOrWhiteSpace(settings.LibreTranslate.Endpoint)
            ? "https://libretranslate.com/translate"
            : settings.LibreTranslate.Endpoint;
        LibreTranslateApiKey = settings.LibreTranslate.ApiKey ?? string.Empty;
        TranslatePartials = settings.TranslatePartials;

        TranslationStatus = BuildTranslationStatus();
        UpdateSpeechToTextStatus();
    }

    private void SaveTranslationSettings()
    {
        var settings = _settingsStore.Load();
        settings.Translation.Factory = NormalizeTranslationFactory(SelectedTranslationFactory);
        settings.Translation.ProviderOrder = BuildTranslationProviderOrder();
        settings.Translation.GoogleTranslateFreeApi.Enabled = GoogleTranslateFreeApiEnabled;
        settings.Translation.GoogleTranslateFreeApi.Endpoint = GoogleTranslateFreeApiEndpoint.Trim();
        settings.Translation.LibreTranslate.Enabled = LibreTranslateEnabled;
        settings.Translation.LibreTranslate.Endpoint = LibreTranslateEndpoint.Trim();
        settings.Translation.LibreTranslate.ApiKey = LibreTranslateApiKey.Trim();
        settings.Translation.TranslatePartials = TranslatePartials;
        _settingsStore.Save(settings);
        TranslationStatus = BuildTranslationStatus();
        UpdateSpeechToTextStatus();
    }

    private List<string> BuildTranslationProviderOrder()
    {
        var providers = new List<string>();
        foreach (var provider in new[] { TranslationPrimaryProvider, TranslationSecondaryProvider })
        {
            var normalized = NormalizeTranslationProvider(provider, TranslationProviders.GoogleTranslateFreeApi);
            if (!providers.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                providers.Add(normalized);
            }
        }

        return providers;
    }

    private string BuildTranslationStatus()
    {
        var factory = NormalizeTranslationFactory(SelectedTranslationFactory);
        if (string.Equals(factory, TranslationFactories.FallbackChain, StringComparison.OrdinalIgnoreCase))
        {
            return $"Fallback chain: {string.Join(" -> ", BuildTranslationProviderOrder())} | {(TranslatePartials ? "partials on" : "final-only")}";
        }

        return $"Active provider: {factory} | {(TranslatePartials ? "partials on" : "final-only")}";
    }

    private static string NormalizeTranslationFactory(string? factory)
    {
        return string.Equals(factory, TranslationProviders.GoogleTranslateFreeApi, StringComparison.OrdinalIgnoreCase)
            ? TranslationProviders.GoogleTranslateFreeApi
            : string.Equals(factory, TranslationProviders.LibreTranslate, StringComparison.OrdinalIgnoreCase)
                ? TranslationProviders.LibreTranslate
                : TranslationFactories.FallbackChain;
    }

    private static string NormalizeTranslationProvider(string? provider, string fallback)
    {
        return string.Equals(provider, TranslationProviders.LibreTranslate, StringComparison.OrdinalIgnoreCase)
            ? TranslationProviders.LibreTranslate
            : string.Equals(provider, TranslationProviders.GoogleTranslateFreeApi, StringComparison.OrdinalIgnoreCase)
                ? TranslationProviders.GoogleTranslateFreeApi
                : fallback;
    }
}
