using System.ComponentModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.Input;
using Sublingual.App.Models;
using Sublingual.Domain.Transcription;

namespace Sublingual.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    [RelayCommand]
    private void ClearSavedSessions()
    {
        var deletedCount = _sessionStorage.ClearAllSessions();
        LoadSavedSessions();
        StatusMessage = deletedCount == 0
            ? $"No saved sessions found in {_sessionsRoot}"
            : $"Deleted {deletedCount} saved session(s) from {_sessionsRoot}";
    }

    [RelayCommand]
    private void RefreshSavedSessions()
    {
        LoadSavedSessions();
    }

    [RelayCommand]
    private void ToggleSelectAllSessions()
    {
        var target = !AreAllSessionsSelected;
        AreAllSessionsSelected = target;
    }

    partial void OnAreAllSessionsSelectedChanged(bool value)
    {
        if (_isUpdatingSessionSelection)
        {
            return;
        }

        _isUpdatingSessionSelection = true;
        try
        {
            foreach (var session in SavedSessions)
            {
                session.IsSelected = value;
            }
        }
        finally
        {
            _isUpdatingSessionSelection = false;
        }

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(HasSelectedSessions)));
        DeleteSelectedSessionsCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(HasSelectedSessions))]
    private void DeleteSelectedSessions()
    {
        var selectedPaths = SavedSessions
            .Where(session => session.IsSelected)
            .Select(session => session.DirectoryPath)
            .ToList();

        var deletedCount = _sessionStorage.DeleteSessions(selectedPaths);
        LoadSavedSessions();
        LoadSelectedSessionTranscript();
        StatusMessage = deletedCount == 0
            ? "No selected sessions were deleted."
            : $"Deleted {deletedCount} selected session(s).";
    }

    [RelayCommand(CanExecute = nameof(HasSelectedSavedSession))]
    private void OpenSelectedSessionFolder()
    {
        if (SelectedSavedSession is null)
        {
            return;
        }

        var psi = OperatingSystem.IsWindows()
            ? new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{SelectedSavedSession.DirectoryPath}\"",
                UseShellExecute = true,
            }
            : new ProcessStartInfo
            {
                FileName = OperatingSystem.IsMacOS() ? "open" : "xdg-open",
                Arguments = $"\"{SelectedSavedSession.DirectoryPath}\"",
                UseShellExecute = true,
            };

        Process.Start(psi);
    }

    [RelayCommand(CanExecute = nameof(HasSelectedSavedSession))]
    private void PlaySelectedSessionAudio()
    {
        if (SelectedSavedSession is null || !File.Exists(SelectedSavedSession.AudioPath))
        {
            StatusMessage = "Selected session audio file was not found.";
            return;
        }

        var psi = new ProcessStartInfo
        {
            FileName = SelectedSavedSession.AudioPath,
            UseShellExecute = true,
        };

        Process.Start(psi);
    }

    [RelayCommand(CanExecute = nameof(HasSelectedSavedSession))]
    private void DeleteCurrentSession()
    {
        if (SelectedSavedSession is null)
        {
            return;
        }

        var deletedCount = _sessionStorage.DeleteSessions([SelectedSavedSession.DirectoryPath]);
        LoadSavedSessions();
        ActiveSessionsPage = "list";
        StatusMessage = deletedCount == 0
            ? "Current session was not deleted."
            : "Current session deleted.";
    }

    [RelayCommand(CanExecute = nameof(HasSelectedSavedSession))]
    private void ExportSelectedSessionTranscriptAsTxt()
    {
        if (SelectedSavedSession is null)
        {
            return;
        }

        var transcriptEntries = BuildExportTranscriptEntries(_sessionStorage.GetTranscriptEntries(SelectedSavedSession.TranscriptPath));
        var exportPath = Path.Combine(SelectedSavedSession.DirectoryPath, "transcript.txt");
        var lines = transcriptEntries.SelectMany(entry =>
        {
            var result = new List<string> { $"[{entry.UpdatedAt:yyyy-MM-dd HH:mm:ss}]" };
            if (!string.IsNullOrWhiteSpace(entry.PartialText)) result.Add($"Partial: {entry.PartialText}");
            if (!string.IsNullOrWhiteSpace(entry.FinalText)) result.Add($"Final: {entry.FinalText}");
            if (!string.IsNullOrWhiteSpace(entry.FinalTranslatedText)) result.Add($"Final Translation: {entry.FinalTranslatedText}");
            result.Add(string.Empty);
            return result;
        });

        File.WriteAllLines(exportPath, lines);
        StatusMessage = $"Exported transcript txt to {exportPath}";
    }

    [RelayCommand(CanExecute = nameof(HasSelectedSavedSession))]
    private void ExportSelectedSessionTranscriptAsJson()
    {
        if (SelectedSavedSession is null)
        {
            return;
        }

        var exportPath = Path.Combine(SelectedSavedSession.DirectoryPath, "transcript-export.json");
        var transcriptEntries = BuildExportTranscriptEntries(_sessionStorage.GetTranscriptEntries(SelectedSavedSession.TranscriptPath));
        var json = System.Text.Json.JsonSerializer.Serialize(transcriptEntries, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
        });
        File.WriteAllText(exportPath, json);
        StatusMessage = $"Exported transcript json to {exportPath}";
    }

    [RelayCommand(CanExecute = nameof(CanGoToPreviousSessionsPage))]
    private void PreviousSessionsPage()
    {
        if (!CanGoToPreviousSessionsPage)
        {
            return;
        }

        SessionsPageIndex -= 1;
    }

    [RelayCommand(CanExecute = nameof(CanGoToNextSessionsPage))]
    private void NextSessionsPage()
    {
        if (!CanGoToNextSessionsPage)
        {
            return;
        }

        SessionsPageIndex += 1;
    }

    private void LoadSavedSessions()
    {
        var preferredSelectedPath = SelectedSavedSession?.DirectoryPath;

        foreach (var existingSession in _allSavedSessions)
        {
            existingSession.PropertyChanged -= OnSavedSessionPropertyChanged;
        }

        _allSavedSessions.Clear();
        foreach (var session in _sessionStorage.GetSessions())
        {
            var item = new CaptureSessionItemViewModel(session);
            item.PropertyChanged += OnSavedSessionPropertyChanged;
            _allSavedSessions.Add(item);
        }

        AreAllSessionsSelected = false;

        LoadSessionFolders();
        ApplySavedSessionsFilter(preferredSelectedPath);
    }

    partial void OnSessionSearchTextChanged(string value)
    {
        SessionsPageIndex = 0;
        ApplySavedSessionsFilter(SelectedSavedSession?.DirectoryPath);
    }

    partial void OnSessionsPageIndexChanged(int value)
    {
        ApplySavedSessionsFilter(SelectedSavedSession?.DirectoryPath);
    }

    private void ApplySavedSessionsFilter(string? preferredSelectedPath)
    {
        SavedSessions.Clear();

        var filtered = GetFilteredSessions();
        var pageCount = Math.Max(1, (int)Math.Ceiling((double)filtered.Count / SessionsPageSize));

        if (SessionsPageIndex >= pageCount)
        {
            SessionsPageIndex = Math.Max(0, pageCount - 1);
            return;
        }

        var paged = filtered
            .Skip(SessionsPageIndex * SessionsPageSize)
            .Take(SessionsPageSize)
            .ToList();

        foreach (var session in paged)
        {
            SavedSessions.Add(session);
        }

        SelectedSavedSession = SavedSessions.FirstOrDefault(session =>
                string.Equals(session.DirectoryPath, preferredSelectedPath, StringComparison.OrdinalIgnoreCase))
            ?? SavedSessions.FirstOrDefault();

        LoadSelectedSessionTranscript();

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(HasSavedSessions)));
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(NoSavedSessions)));
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(NoSearchResults)));
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(SessionsPageCount)));
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(SessionsPageText)));
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(CanGoToPreviousSessionsPage)));
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(CanGoToNextSessionsPage)));
        DeleteSelectedSessionsCommand.NotifyCanExecuteChanged();
        PreviousSessionsPageCommand.NotifyCanExecuteChanged();
        NextSessionsPageCommand.NotifyCanExecuteChanged();
    }

    private List<CaptureSessionItemViewModel> GetFilteredSessions()
    {
        var selectedFolderId = NormalizeSessionFolderId(SessionFolderId);
        var baseList = _allSavedSessions
            .Where(session => string.Equals(session.FolderId, selectedFolderId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (string.IsNullOrWhiteSpace(SessionSearchText))
        {
            return baseList;
        }

        return baseList.Where(session =>
                session.SessionId.Contains(SessionSearchText, StringComparison.OrdinalIgnoreCase)
                || session.Title.Contains(SessionSearchText, StringComparison.OrdinalIgnoreCase)
                || session.TreePath.Contains(SessionSearchText, StringComparison.OrdinalIgnoreCase)
                || session.AudioPath.Contains(SessionSearchText, StringComparison.OrdinalIgnoreCase)
                || session.CreatedAtText.Contains(SessionSearchText, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private void LoadSelectedSessionTranscript()
    {
        SelectedSessionTranscriptEntries.Clear();

        if (SelectedSavedSession is null)
        {
            SelectedSessionModelName = "Unknown";
            SelectedSessionDeviceName = "Unknown";
            SelectedSessionLanguage = "en";
            SelectedSessionTreePath = string.Empty;
            SelectedSessionDurationText = "0.0 s";
            SelectedSessionAudioPath = string.Empty;
            SelectedSessionTranscriptPath = string.Empty;
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(HasSelectedSavedSession)));
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(HasSelectedSessionTranscriptEntries)));
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(NoSelectedSessionTranscriptEntries)));
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(SelectedSessionEntryCountText)));
            OpenSelectedSessionFolderCommand.NotifyCanExecuteChanged();
            PlaySelectedSessionAudioCommand.NotifyCanExecuteChanged();
            DeleteCurrentSessionCommand.NotifyCanExecuteChanged();
            ExportSelectedSessionTranscriptAsTxtCommand.NotifyCanExecuteChanged();
            ExportSelectedSessionTranscriptAsJsonCommand.NotifyCanExecuteChanged();
            return;
        }

        var metadata = _sessionStorage.GetSessionMetadata(SelectedSavedSession.MetadataPath);
        SelectedSessionModelName = metadata?.ModelName ?? "Unknown";
        SelectedSessionDeviceName = metadata?.DeviceName ?? "Unknown";
        SelectedSessionLanguage = metadata?.Language ?? "en";
        SelectedSessionTreePath = metadata?.TreePath ?? SelectedSavedSession.TreePath;
        SelectedSessionDurationText = $"{(metadata?.DurationSeconds ?? 0):0.0} s";
        SelectedSessionAudioPath = SelectedSavedSession.AudioPath;
        SelectedSessionTranscriptPath = SelectedSavedSession.TranscriptPath;

        foreach (var entry in _sessionStorage.GetTranscriptEntries(SelectedSavedSession.TranscriptPath))
        {
            SelectedSessionTranscriptEntries.Add(new SavedTranscriptEntryViewModel(entry));
        }

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(HasSelectedSavedSession)));
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(HasSelectedSessionTranscriptEntries)));
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(NoSelectedSessionTranscriptEntries)));
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(SelectedSessionEntryCountText)));
        OpenSelectedSessionFolderCommand.NotifyCanExecuteChanged();
        PlaySelectedSessionAudioCommand.NotifyCanExecuteChanged();
        DeleteCurrentSessionCommand.NotifyCanExecuteChanged();
        ExportSelectedSessionTranscriptAsTxtCommand.NotifyCanExecuteChanged();
        ExportSelectedSessionTranscriptAsJsonCommand.NotifyCanExecuteChanged();
    }

    private void OnSavedSessionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CaptureSessionItemViewModel.IsSelected))
        {
            if (_isUpdatingSessionSelection)
            {
                return;
            }

            _isUpdatingSessionSelection = true;
            try
            {
                AreAllSessionsSelected = SavedSessions.Count > 0 && SavedSessions.All(session => session.IsSelected);
            }
            finally
            {
                _isUpdatingSessionSelection = false;
            }

            OnPropertyChanged(new PropertyChangedEventArgs(nameof(HasSelectedSessions)));
            DeleteSelectedSessionsCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand]
    private void ToggleSessionSelection(CaptureSessionItemViewModel? session)
    {
        if (session is null)
        {
            return;
        }

        session.IsSelected = !session.IsSelected;
    }

    private static IReadOnlyList<SavedTranscriptEntry> BuildExportTranscriptEntries(IReadOnlyList<SavedTranscriptEntry> entries)
    {
        var cleaned = entries
            .GroupBy(entry => entry.SegmentId, StringComparer.Ordinal)
            .Select(group => group
                .OrderByDescending(entry => entry.IsFinal)
                .ThenByDescending(entry => !string.IsNullOrWhiteSpace(entry.TranslatedText))
                .ThenByDescending(entry => entry.UpdatedAt)
                .First())
            .OrderBy(entry => entry.UpdatedAt)
            .ToList();

        if (cleaned.Any(entry => entry.IsFinal))
        {
            return cleaned.Where(entry => entry.IsFinal).ToList();
        }

        return cleaned
            .TakeLast(1)
            .ToList();
    }
}
