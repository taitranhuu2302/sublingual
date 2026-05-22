using System.Diagnostics;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Sublingual.App.Models;
using Sublingual.App.Services;
using Sublingual.Domain.Transcription;
using Sublingual.Infrastructure.Audio.Processing;

namespace Sublingual.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    [RelayCommand]
    private async Task ImportSpeechToTextModelAsync()
    {
        if (PickSpeechToTextModelDirectoryAsync is null)
        {
            StatusMessage = "Model import is not available in the current UI context.";
            return;
        }

        var selectedDirectory = await PickSpeechToTextModelDirectoryAsync();
        if (string.IsNullOrWhiteSpace(selectedDirectory))
        {
            return;
        }

        await RunBusyOperationAsync(() => ImportSpeechToTextModelCoreAsync(selectedDirectory));
    }

    [RelayCommand]
    private async Task ImportSpeechToTextModelZipAsync()
    {
        if (PickSpeechToTextModelZipFileAsync is null)
        {
            StatusMessage = "Zip model import is not available in the current UI context.";
            return;
        }

        var selectedFile = await PickSpeechToTextModelZipFileAsync();
        if (string.IsNullOrWhiteSpace(selectedFile))
        {
            return;
        }

        await RunBusyOperationAsync(() => ImportSpeechToTextModelZipCoreAsync(selectedFile));
    }

    [RelayCommand]
    private void OpenSpeechToTextModelsFolder()
    {
        var modelsRoot = _modelCatalog.GetManagedModelsRoot();
        Directory.CreateDirectory(modelsRoot);

        var psi = OperatingSystem.IsWindows()
            ? new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{modelsRoot}\"",
                UseShellExecute = true,
            }
            : new ProcessStartInfo
            {
                FileName = OperatingSystem.IsMacOS() ? "open" : "xdg-open",
                Arguments = $"\"{modelsRoot}\"",
                UseShellExecute = true,
            };

        Process.Start(psi);
    }

    [RelayCommand]
    private void OpenInstallSpeechModelsDialog()
    {
        LoadInstallableSpeechModels();
        IsInstallSpeechModelsDialogOpen = true;
    }

    [RelayCommand]
    private void CloseInstallSpeechModelsDialog()
    {
        IsInstallSpeechModelsDialogOpen = false;
    }

    [RelayCommand]
    private async Task InstallSpeechModelAsync(SpeechToTextInstallableModelViewModel? model)
    {
        if (model is null)
        {
            return;
        }

        var source = _modelSourceCatalog.GetDefaultModels().FirstOrDefault(entry =>
            string.Equals(entry.ModelName, model.ModelName, StringComparison.OrdinalIgnoreCase));
        if (source is null)
        {
            StatusMessage = $"No download source is configured for {model.ModelName}.";
            return;
        }

        await RunBusyOperationAsync(async () =>
        {
            SetSpeechModelDownloadState(model.ModelName, isDownloading: true);
            SetSpeechModelError(model.ModelName, string.Empty);
            SpeechModelDownloadPercent = 0;
            SpeechModelDownloadStatus = $"Downloading {source.DisplayName}... 0%";
            SpeechModelDownloadErrorMessage = string.Empty;
            SpeechToTextStatus = $"Downloading {source.DisplayName}...";

            try
            {
                var progress = new Progress<int>(percent =>
                {
                    SpeechModelDownloadPercent = percent;
                    SpeechModelDownloadStatus = $"Downloading {source.DisplayName}... {percent}%";
                });

                var importedPath = await _defaultModelInstaller.InstallAsync(source, progress);
                LoadSpeechToTextModels();
                LoadInstallableSpeechModels();

                SelectedSpeechToTextModel = SpeechToTextModels.FirstOrDefault(item =>
                    string.Equals(item.Name, source.ModelName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(item.Path, importedPath, StringComparison.OrdinalIgnoreCase));

                SpeechToTextStatus = SelectedSpeechToTextModel is null
                    ? $"Installed {source.ModelName}, but it could not be selected."
                    : BuildSpeechToTextStatus(SelectedSpeechToTextModel.Name);
                StatusMessage = $"Installed speech-to-text model {source.ModelName}.";
                SpeechModelDownloadPercent = 100;
                SpeechModelDownloadStatus = $"Installed {source.ModelName}.";
                SpeechModelDownloadErrorMessage = string.Empty;

                await Task.Delay(1500);

                if (string.Equals(SpeechModelDownloadStatus, $"Installed {source.ModelName}.", StringComparison.Ordinal))
                {
                    SpeechModelDownloadPercent = 0;
                    SpeechModelDownloadStatus = string.Empty;
                }
            }
            catch (Exception ex)
            {
                SpeechModelDownloadStatus = $"Failed to install {source.ModelName}.";
                SpeechModelDownloadErrorMessage = ex.Message;
                SpeechToTextStatus = $"Install failed for {source.ModelName}: {ex.Message}";
                StatusMessage = $"Install failed for {source.ModelName}.";
                SetSpeechModelError(source.ModelName, ex.Message);
            }
            finally
            {
                SetSpeechModelDownloadState(source.ModelName, isDownloading: false);
            }
        });
    }

    private void LoadSpeechToTextModels()
    {
        SpeechToTextModels.Clear();
        foreach (var model in _modelCatalog.GetAvailableModels())
        {
            SpeechToTextModels.Add(model);
        }

        var selectedModelName = _settingsStore.Load().SpeechToText.SelectedModel;
        SelectedSpeechToTextModel = SpeechToTextModels.FirstOrDefault(model =>
                string.Equals(model.Name, selectedModelName, StringComparison.OrdinalIgnoreCase))
            ?? SpeechToTextModels.FirstOrDefault(model =>
                string.Equals(model.Name, "default", StringComparison.OrdinalIgnoreCase))
            ?? SpeechToTextModels.FirstOrDefault();

        if (SelectedSpeechToTextModel is not null)
        {
            UpdateSpeechToTextStatus();
            return;
        }

        SpeechToTextStatus = "No local speech model found.";
    }

    private void LoadInstallableSpeechModels()
    {
        var installedModelNames = SpeechToTextModels
            .Select(model => model.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var currentStates = InstallableSpeechModels.ToDictionary(
            model => model.ModelName,
            model => (model.IsDownloading, model.ErrorMessage),
            StringComparer.OrdinalIgnoreCase);

        InstallableSpeechModels.Clear();

        foreach (var model in _modelSourceCatalog.GetDefaultModels())
        {
            currentStates.TryGetValue(model.ModelName, out var state);

            InstallableSpeechModels.Add(new SpeechToTextInstallableModelViewModel
            {
                Language = model.Language,
                ModelName = model.ModelName,
                DisplayName = string.IsNullOrWhiteSpace(model.DisplayName) ? model.ModelName : model.DisplayName,
                ZipUrl = model.ZipUrl,
                IsInstalled = installedModelNames.Contains(model.ModelName),
                IsDownloading = state.IsDownloading,
                ErrorMessage = state.ErrorMessage ?? string.Empty,
            });
        }

        OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(HasInstallableSpeechModels)));
        OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(NoInstallableSpeechModels)));
    }

    private void SetSpeechModelDownloadState(string modelName, bool isDownloading)
    {
        foreach (var item in InstallableSpeechModels)
        {
            item.IsDownloading = isDownloading && string.Equals(item.ModelName, modelName, StringComparison.OrdinalIgnoreCase);
        }

        OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(InstallableSpeechModels)));
    }

    private void SetSpeechModelError(string modelName, string errorMessage)
    {
        foreach (var item in InstallableSpeechModels)
        {
            if (!string.Equals(item.ModelName, modelName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            item.ErrorMessage = errorMessage;
        }

        OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(InstallableSpeechModels)));
    }

    private void LoadSpeechToTextSettings()
    {
        var settings = _settingsStore.Load().SpeechToText;
        SelectedSpeechToTextChunkPreset = SpeechToTextRuntimeOptions.NormalizeChunkPreset(settings.RealtimeChunkPreset);
        _speechToTextRuntimeOptions.ApplyChunkPreset(SelectedSpeechToTextChunkPreset);
        PipelineSummary = BuildPipelineSummary(_speechToTextRuntimeOptions.ChunkWindow);
    }

    private static string BuildPipelineSummary(TimeSpan chunkWindow)
    {
        return $"16kHz mono PCM16, {(int)Math.Round(chunkWindow.TotalMilliseconds)}ms fixed chunks";
    }

    private async Task PreloadSelectedSpeechToTextModelAsync(string modelName)
    {
        if (_transcriptionService is not VoskTranscriptionService vosk)
        {
            await Dispatcher.UIThread.InvokeAsync(UpdateSpeechToTextStatus);
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() => SpeechToTextStatus = $"Loading model: {modelName}");

        try
        {
            await vosk.PreloadModelAsync(modelName);

            if (!string.Equals(SelectedSpeechToTextModel?.Name, modelName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                SpeechToTextStatus = BuildSpeechToTextStatus(modelName);
            });
        }
        catch (Exception ex)
        {
            if (!string.Equals(SelectedSpeechToTextModel?.Name, modelName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                SpeechToTextStatus = $"Selected model: {modelName} | Load failed: {ex.Message}";
            });
        }
    }

    private Task ImportSpeechToTextModelCoreAsync(string selectedDirectory)
    {
        var importedPath = _modelImporter.ImportFromDirectory(selectedDirectory);
        LoadSpeechToTextModels();

        var importedModelName = Path.GetFileName(importedPath);
        SelectedSpeechToTextModel = SpeechToTextModels.FirstOrDefault(model =>
            string.Equals(model.Name, importedModelName, StringComparison.OrdinalIgnoreCase));

        SpeechToTextStatus = SelectedSpeechToTextModel is null
            ? "Model imported but could not be selected."
            : BuildSpeechToTextStatus(SelectedSpeechToTextModel.Name);
        StatusMessage = $"Imported speech-to-text model from {selectedDirectory}.";

        return Task.CompletedTask;
    }

    private Task ImportSpeechToTextModelZipCoreAsync(string selectedFile)
    {
        var importedPath = _modelImporter.ImportFromZip(selectedFile);
        LoadSpeechToTextModels();

        var importedModelName = Path.GetFileName(importedPath);
        SelectedSpeechToTextModel = SpeechToTextModels.FirstOrDefault(model =>
            string.Equals(model.Name, importedModelName, StringComparison.OrdinalIgnoreCase));

        SpeechToTextStatus = SelectedSpeechToTextModel is null
            ? "Zip model imported but could not be selected."
            : BuildSpeechToTextStatus(SelectedSpeechToTextModel.Name);
        StatusMessage = $"Imported zipped speech-to-text model from {selectedFile}.";

        return Task.CompletedTask;
    }

    private string BuildSpeechToTextStatus(string modelName)
    {
        var warning = GetSourceLanguageModelWarning(modelName, SelectedSourceLanguage);
        return string.IsNullOrWhiteSpace(warning)
            ? $"Selected model: {modelName}"
            : $"Selected model: {modelName} | Warning: {warning}";
    }

    private static string GetSourceLanguageModelWarning(string? modelName, string sourceLanguage)
    {
        if (string.IsNullOrWhiteSpace(modelName))
        {
            return string.Empty;
        }

        var normalizedName = modelName.ToLowerInvariant();
        var expectedHints = sourceLanguage switch
        {
            "zh" => new[] { "zh", "cn", "chinese", "mandarin" },
            "vi" => new[] { "vi", "vn", "viet", "vietnamese" },
            _ => new[] { "en", "eng", "english" },
        };

        var conflictingHints = sourceLanguage switch
        {
            "zh" => new[] { "english", "vietnamese", "viet" },
            "vi" => new[] { "english", "chinese", "mandarin" },
            _ => new[] { "vietnamese", "viet", "chinese", "mandarin" },
        };

        var hasExpectedHint = expectedHints.Any(normalizedName.Contains);
        var hasConflictingHint = conflictingHints.Any(normalizedName.Contains);

        if (!hasExpectedHint && hasConflictingHint)
        {
            return $"source language `{sourceLanguage}` may not match the current Vosk model";
        }

        return string.Empty;
    }
}
