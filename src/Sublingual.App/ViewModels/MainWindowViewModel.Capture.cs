using System.ComponentModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Sublingual.App.Services;
using Sublingual.Domain.Audio;
using Sublingual.Domain.Transcription;

namespace Sublingual.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    [RelayCommand(CanExecute = nameof(CanStartCapture))]
    private async Task StartCaptureAsync()
    {
        await RunBusyOperationAsync(async () =>
        {
            var normalizedFolderId = NormalizeSessionFolderId(SessionFolderId);
            SessionFolderId = normalizedFolderId;
            _outputPath = _sessionStorage.CreateSessionOutputPath(SessionName, normalizedFolderId);
            _chunkCount = 0;
            _totalBytesCaptured = 0;
            ChunkCount = 0;
            TotalBytesText = "0 bytes";
            AudioLevel = 0;
            PeakAudioLevel = 0;
            AudioLevelText = "Listening...";
            PartialTranscript = string.Empty;
            FinalTranscript = string.Empty;
            PartialTranslatedTranscript = string.Empty;
            FinalTranslatedTranscript = string.Empty;
            TranscriptStatus = string.Empty;
            TranslationRuntimeStatus = "Starting capture...";
            TranslationRuntimeDiagnostics = "Waiting for transcript updates.";
            OutputFilePath = _outputPath;

            await _session.StartAsync(
                string.IsNullOrWhiteSpace(SelectedDevice?.Id) ? null : SelectedDevice.Id,
                SelectedDeviceName,
                _outputPath,
                string.Empty,
                normalizedFolderId);

            EnsureOverlayVisibleAction?.Invoke();

            LoadSavedSessions();
            SelectedSavedSession = SavedSessions.FirstOrDefault(session =>
                string.Equals(session.AudioPath, _outputPath, StringComparison.OrdinalIgnoreCase));
            _sessionStorage.SetPreferredFolder(normalizedFolderId);

            IsCapturing = true;
            CaptureState = _session.State.ToString();
            StatusMessage = $"Capturing on {CurrentPlatform} into {FormatSessionFolderLabel(normalizedFolderId)}.";
        });
    }

    [RelayCommand(CanExecute = nameof(CanStopCapture))]
    private async Task StopCaptureAsync()
    {
        await RunBusyOperationAsync(async () =>
        {
            await _session.StopAsync();
            IsCapturing = false;
            CaptureState = _session.State.ToString();
            AudioLevel = 0;
            AudioLevelText = "Silence";
            StatusMessage = $"Capture stopped. Saved {ChunkCount} chunks to {FormatSessionFolderLabel(NormalizeSessionFolderId(SessionFolderId))}.";
            TranslationRuntimeStatus = "Capture stopped.";
        });
    }

    [RelayCommand]
    private async Task RefreshDevicesAsync()
    {
        await RunBusyOperationAsync(LoadDevicesAsync);
    }

    [RelayCommand]
    private void ToggleOverlay()
    {
        ToggleOverlayAction?.Invoke();
    }

    private async Task LoadDevicesAsync()
    {
        var devices = await _session.GetAvailableDevicesAsync();

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            Devices.Clear();
            foreach (var device in devices)
            {
                Devices.Add(new AudioDeviceItemViewModel(device));
            }

            var selected = Devices.FirstOrDefault(d => d.IsDefault) ?? Devices.FirstOrDefault();
            SelectedDevice = selected;
            SelectedDeviceId = selected?.Id ?? string.Empty;
            SelectedDeviceName = selected?.Name ?? "No device selected.";
            CaptureState = _session.State.ToString();
            StatusMessage = devices.Count == 0
                ? $"No audio devices found for {CurrentPlatform}."
                : $"Found {devices.Count} device(s).";
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(HasDevices)));
            StartCaptureCommand.NotifyCanExecuteChanged();
        });
    }

    private async Task RunBusyOperationAsync(Func<Task> action)
    {
        if (IsBusy) return;
        try
        {
            IsBusy = true;
            StartCaptureCommand.NotifyCanExecuteChanged();
            StopCaptureCommand.NotifyCanExecuteChanged();
            await action();
        }
        catch (Exception ex)
        {
            CaptureState = AudioCaptureState.Faulted.ToString();
            StatusMessage = $"Operation failed: {ex.Message}";
            IsCapturing = false;
        }
        finally
        {
            IsBusy = false;
            CaptureState = _session.State.ToString();
            StartCaptureCommand.NotifyCanExecuteChanged();
            StopCaptureCommand.NotifyCanExecuteChanged();
        }
    }

    private void OnChunkObserved(object? sender, AudioChunk chunk)
    {
        _chunkCount += 1;
        _totalBytesCaptured += chunk.Data.Length;

        _ = Dispatcher.UIThread.InvokeAsync(() =>
        {
            ChunkCount = _chunkCount;
            TotalBytesText = $"{_totalBytesCaptured:N0} bytes";
            CaptureState = _session.State.ToString();
            var level = CalculateAudioLevelPercent(chunk);
            AudioLevel = level;
            PeakAudioLevel = Math.Max(PeakAudioLevel * 0.90, level);
            AudioLevelText = $"{level:0}%";
            PushWaveformSample(level);
        });
    }

    private void OnTranscriptPreviewUpdated(object? sender, TranscriptPreviewUpdate update)
    {
        _ = Dispatcher.UIThread.InvokeAsync(() =>
        {
            PartialTranscript = update.PartialText ?? string.Empty;
            PartialTranslatedTranscript = update.PartialTranslatedText ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(update.FinalText))
                FinalTranscript = update.FinalText;
            if (!string.IsNullOrWhiteSpace(update.FinalTranslatedText))
                FinalTranslatedTranscript = update.FinalTranslatedText;
            if (!string.IsNullOrWhiteSpace(update.FinalText) || !string.IsNullOrWhiteSpace(update.PartialText))
                TranscriptStatus = BuildTranscriptStatus(update);

            TranslationRuntimeStatus = BuildTranslationRuntimeStatus(update);
            TranslationRuntimeDiagnostics = string.IsNullOrWhiteSpace(update.TranslationDiagnostics)
                ? "No diagnostics available."
                : update.TranslationDiagnostics;

            if (SelectedSavedSession is not null
                && string.Equals(SelectedSavedSession.AudioPath, _outputPath, StringComparison.OrdinalIgnoreCase))
            {
                LoadSelectedSessionTranscript();
            }
        });
    }

    private static string BuildTranscriptStatus(TranscriptPreviewUpdate update)
    {
        return $"Updated {update.UpdatedAt:HH:mm:ss} | {update.TranslationProvider}{(update.TranslationCacheHit ? " | cache" : string.Empty)}";
    }

    private static string BuildTranslationRuntimeStatus(TranscriptPreviewUpdate update)
    {
        var mode = !string.IsNullOrWhiteSpace(update.FinalText)
            ? "final"
            : !string.IsNullOrWhiteSpace(update.PartialText)
                ? "partial"
                : "idle";
        var outcome = string.IsNullOrWhiteSpace(update.TranslationDiagnostics)
            ? "no diagnostics"
            : update.TranslationDiagnostics.Contains("skipped", StringComparison.OrdinalIgnoreCase)
                ? "skipped"
                : update.TranslationDiagnostics.Contains("success", StringComparison.OrdinalIgnoreCase)
                    ? "translated"
                    : "updated";
        return $"{mode} | {update.TranslationProvider} | {outcome}{(update.TranslationCacheHit ? " | cache" : string.Empty)}";
    }
}
