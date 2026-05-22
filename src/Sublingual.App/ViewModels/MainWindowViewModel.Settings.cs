using System.ComponentModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.Input;
using Sublingual.Infrastructure.Audio.Processing;

namespace Sublingual.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    [RelayCommand]
    private async Task BrowseSessionsDirectoryAsync()
    {
        if (PickSessionsDirectoryAsync is null)
        {
            StatusMessage = "Folder picker is not available in the current UI context.";
            return;
        }

        var selectedDirectory = await PickSessionsDirectoryAsync();
        if (string.IsNullOrWhiteSpace(selectedDirectory))
        {
            StatusMessage = "Sessions folder selection cancelled.";
            return;
        }

        SessionsDirectoryPath = selectedDirectory;
        StatusMessage = $"Sessions folder set to {selectedDirectory}.";
    }

    [RelayCommand]
    private async Task BrowseSpeechToTextModelsDirectoryAsync()
    {
        if (PickSpeechToTextModelsRootDirectoryAsync is null)
        {
            StatusMessage = "Folder picker is not available in the current UI context.";
            return;
        }

        var selectedDirectory = await PickSpeechToTextModelsRootDirectoryAsync();
        if (string.IsNullOrWhiteSpace(selectedDirectory))
        {
            StatusMessage = "Managed models folder selection cancelled.";
            return;
        }

        SpeechToTextModelsDirectoryPath = selectedDirectory;
        StatusMessage = $"Managed models folder set to {selectedDirectory}.";
    }

    [RelayCommand]
    private void SetOverlayLineHeightPreset(string preset)
    {
        OverlayLineHeightPreset = preset;
        OverlayLineHeight = preset switch
        {
            "Compact" => 1.05,
            "Relaxed" => 1.65,
            _ => 1.35,
        };
    }

    private void SaveStorageSettings()
    {
        if (_suspendStorageSettingsSave)
        {
            return;
        }

        var settings = _settingsStore.Load();
        settings.Storage.SessionsRoot = SessionsDirectoryPath.Trim();
        settings.Storage.SpeechToTextModelsRoot = SpeechToTextModelsDirectoryPath.Trim();
        var normalizedFolderId = NormalizeSessionFolderId(SessionFolderId);
        settings.Storage.LastSessionFolderId = normalizedFolderId;
        settings.Storage.LastSessionTreePath = _sessionStorage.NormalizeSessionTreePath(normalizedFolderId);
        _settingsStore.Save(settings);

        // Normalize storage roots and refresh view model.
        _suspendStorageSettingsSave = true;
        SessionsDirectoryPath = _sessionStorage.GetSessionsRoot();
        SpeechToTextModelsDirectoryPath = _modelCatalog.GetManagedModelsRoot();
        SessionFolderId = _sessionStorage.GetPreferredFolderId();
        _suspendStorageSettingsSave = false;
        LoadSpeechToTextModels();
        LoadSavedSessions();
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(IsBusy) or nameof(IsCapturing))
        {
            StartCaptureCommand.NotifyCanExecuteChanged();
            StopCaptureCommand.NotifyCanExecuteChanged();
        }

        if (e.PropertyName == nameof(SelectedDevice) && SelectedDevice is not null)
        {
            SelectedDeviceId = SelectedDevice.Id;
            SelectedDeviceName = SelectedDevice.Name;
            StartCaptureCommand.NotifyCanExecuteChanged();
        }

        if (e.PropertyName == nameof(SelectedDeviceId))
            StartCaptureCommand.NotifyCanExecuteChanged();

        if (e.PropertyName == nameof(SelectedSpeechToTextModel) && SelectedSpeechToTextModel is not null)
        {
            var settings = _settingsStore.Load();
            settings.SpeechToText.SelectedModel = SelectedSpeechToTextModel.Name;
            _settingsStore.Save(settings);
            _ = PreloadSelectedSpeechToTextModelAsync(SelectedSpeechToTextModel.Name);
            RefreshDefaultSpeechModelState();
        }

        if (e.PropertyName == nameof(SelectedSpeechToTextChunkPreset))
        {
            var normalizedPreset = SpeechToTextRuntimeOptions.NormalizeChunkPreset(SelectedSpeechToTextChunkPreset);
            if (!string.Equals(SelectedSpeechToTextChunkPreset, normalizedPreset, StringComparison.Ordinal))
            {
                SelectedSpeechToTextChunkPreset = normalizedPreset;
                return;
            }

            var settings = _settingsStore.Load();
            settings.SpeechToText.RealtimeChunkPreset = normalizedPreset;
            _settingsStore.Save(settings);
            _speechToTextRuntimeOptions.ApplyChunkPreset(normalizedPreset);
            PipelineSummary = BuildPipelineSummary(_speechToTextRuntimeOptions.ChunkWindow);
        }

        if (e.PropertyName is nameof(SessionsDirectoryPath) or nameof(SpeechToTextModelsDirectoryPath) or nameof(SessionFolderId))
        {
            SaveStorageSettings();
        }

        if (e.PropertyName == nameof(SessionFolderId))
        {
            LoadSessionFolders();
            SessionsPageIndex = 0;
            ApplySavedSessionsFilter(null);
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsGlobalSessionFolderSelected)));
        }

        if (e.PropertyName == nameof(SelectedSourceLanguage))
        {
            RefreshDefaultSpeechModelState();
            UpdateSpeechToTextStatus();
        }

        if (e.PropertyName == nameof(SelectedSavedSession))
        {
            LoadSelectedSessionTranscript();
        }

        if (e.PropertyName == nameof(IsCreateSessionDialogOpen))
        {
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(CanCreateSessionFolder)));
        }

        if (e.PropertyName == nameof(IsInstallSpeechModelsDialogOpen) && IsInstallSpeechModelsDialogOpen)
        {
            LoadInstallableSpeechModels();
        }

        if (e.PropertyName == nameof(ActiveSessionsPage))
        {
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsSessionsListPageActive)));
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsSessionsDetailPageActive)));
        }

        if (e.PropertyName == nameof(OverlayLineHeight))
        {
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(OverlayLineHeightPreviewPixels)));
        }

        if (e.PropertyName == nameof(OverlayLineHeightPreset))
        {
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsCompactOverlayLineHeightPreset)));
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsDefaultOverlayLineHeightPreset)));
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsRelaxedOverlayLineHeightPreset)));
        }

        if (e.PropertyName == nameof(ActiveSettingsTab))
        {
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsGeneralSettingsTabActive)));
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsSpeechSettingsTabActive)));
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsTranslationSettingsTabActive)));
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsOverlaySettingsTabActive)));
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsGeneralSettingsTabInactive)));
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsSpeechSettingsTabInactive)));
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsTranslationSettingsTabInactive)));
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsOverlaySettingsTabInactive)));
        }

        if (e.PropertyName == nameof(SelectedTranslationFactory))
        {
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsFallbackTranslationFactory)));
            SaveTranslationSettings();
        }

        if (e.PropertyName is nameof(TranslationPrimaryProvider)
            or nameof(TranslationSecondaryProvider)
            or nameof(GoogleTranslateFreeApiEnabled)
            or nameof(GoogleTranslateFreeApiEndpoint)
            or nameof(LibreTranslateEnabled)
            or nameof(LibreTranslateEndpoint)
            or nameof(LibreTranslateApiKey)
            or nameof(TranslatePartials)
            or nameof(SelectedSourceLanguage)
            or nameof(SelectedTargetLanguage))
        {
            SaveTranslationSettings();
        }

        if (e.PropertyName is nameof(TranslationTestSourceText) or nameof(IsTestingTranslation))
        {
            TestTranslationCommand.NotifyCanExecuteChanged();
        }

        if (e.PropertyName == nameof(TranslationTestResult))
        {
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(HasTranslationTestResult)));
        }

        if (e.PropertyName == nameof(TranslationTestError))
        {
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(HasTranslationTestError)));
        }

        if (e.PropertyName is nameof(PartialTranscript) or nameof(FinalTranscript))
        {
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(HasTranscript)));
        }

        if (e.PropertyName == nameof(PartialTranslatedTranscript))
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(HasPartialTranslation)));

        if (e.PropertyName == nameof(FinalTranslatedTranscript))
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(HasFinalTranslation)));

        if (e.PropertyName == nameof(IsOverlayVisible))
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(OverlayToggleLabel)));

        if (e.PropertyName == nameof(ActiveTab))
        {
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsCaptureTabActive)));
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsSessionsTabActive)));
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsSettingsTabActive)));
        }
    }
}
