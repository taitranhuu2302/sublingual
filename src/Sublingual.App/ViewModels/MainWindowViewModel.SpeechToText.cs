using System.Diagnostics;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
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
