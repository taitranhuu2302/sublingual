using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sublingual.App.Services;

namespace Sublingual.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    public bool HasRenameSessionFolderValidationError => !string.IsNullOrWhiteSpace(RenameSessionFolderValidationError);

    [RelayCommand]
    private void OpenCreateSessionDialog()
    {
        NewSessionFolderName = string.Empty;
        IsCreateSessionDialogOpen = true;
        CreateSessionFolderCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void CloseCreateSessionDialog()
    {
        IsCreateSessionDialogOpen = false;
    }

    [RelayCommand(CanExecute = nameof(CanCreateSessionFolder))]
    private void CreateSessionFolder()
    {
        var created = _sessionStorage.CreateFolder(NewSessionFolderName);
        SessionFolderId = created.Id;
        IsCreateSessionDialogOpen = false;
        NewSessionFolderName = string.Empty;
        LoadSessionFolders();

        _sessionStorage.SetPreferredFolder(SessionFolderId);

        StatusMessage = $"Created session folder: {created.Name}.";
    }

    [RelayCommand]
    private void OpenRenameSessionFolderDialog()
    {
        if (SelectedSessionFolder is null)
        {
            RuntimeLog = "Select a folder.";
            return;
        }

        if (string.Equals(SelectedSessionFolder.FolderId, CaptureSessionStorage.GlobalSessionFolderId, StringComparison.OrdinalIgnoreCase))
        {
            RuntimeLog = "Global folder cannot be renamed.";
            return;
        }

        RenameSessionFolderName = SelectedSessionFolder.Name;
        RenameSessionFolderValidationError = string.Empty;
        IsRenameSessionFolderDialogOpen = true;
        RenameSessionFolderCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void CloseRenameSessionFolderDialog()
    {
        IsRenameSessionFolderDialogOpen = false;
    }

    [RelayCommand(CanExecute = nameof(CanRenameSessionFolder))]
    private void RenameSessionFolder()
    {
        if (SelectedSessionFolder is null)
        {
            IsRenameSessionFolderDialogOpen = false;
            return;
        }

        try
        {
            _sessionStorage.RenameFolder(SelectedSessionFolder.FolderId, RenameSessionFolderName);
            IsRenameSessionFolderDialogOpen = false;
            LoadSessionFolders();
            LoadSavedSessions();
            StatusMessage = "Folder renamed.";
        }
        catch (Exception ex)
        {
            RenameSessionFolderValidationError = ex.Message;
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(HasRenameSessionFolderValidationError)));
            RenameSessionFolderCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand]
    private void OpenDeleteSessionFolderDialog()
    {
        if (SelectedSessionFolder is null)
        {
            RuntimeLog = "Select a folder.";
            return;
        }

        if (string.Equals(SelectedSessionFolder.FolderId, CaptureSessionStorage.GlobalSessionFolderId, StringComparison.OrdinalIgnoreCase))
        {
            RuntimeLog = "Global folder cannot be deleted.";
            return;
        }

        IsDeleteSessionFolderDialogOpen = true;
    }

    [RelayCommand]
    private void CloseDeleteSessionFolderDialog()
    {
        IsDeleteSessionFolderDialogOpen = false;
    }

    [RelayCommand]
    private void DeleteSessionFolder()
    {
        if (SelectedSessionFolder is null)
        {
            IsDeleteSessionFolderDialogOpen = false;
            return;
        }

        try
        {
            var movedCount = _sessionStorage.DeleteFolder(SelectedSessionFolder.FolderId);
            SessionFolderId = CaptureSessionStorage.GlobalSessionFolderId;
            IsDeleteSessionFolderDialogOpen = false;
            LoadSessionFolders();
            LoadSavedSessions();
            RuntimeLog = movedCount == 0
                ? "Folder deleted."
                : $"Folder deleted. Moved {movedCount} session(s) to Global.";
        }
        catch (Exception ex)
        {
            RuntimeLog = ex.Message;
        }
    }

    [RelayCommand]
    private void OpenMoveSelectedSessionsDialog()
    {
        if (!HasSelectedSessions)
        {
            RuntimeLog = "No sessions selected.";
            return;
        }

        LoadSessionFolders();
        MoveTargetSessionFolder = SelectedSessionFolder;
        IsMoveSessionsDialogOpen = true;
    }

    [RelayCommand]
    private void CloseMoveSessionsDialog()
    {
        IsMoveSessionsDialogOpen = false;
    }

    [RelayCommand]
    private void MoveSelectedSessions()
    {
        if (!HasSelectedSessions)
        {
            IsMoveSessionsDialogOpen = false;
            return;
        }

        var target = MoveTargetSessionFolder;
        if (target is null)
        {
            RuntimeLog = "Select a target folder.";
            return;
        }

        var selectedPaths = SavedSessions
            .Where(session => session.IsSelected)
            .Select(session => session.DirectoryPath)
            .ToList();

        var movedCount = _sessionStorage.MoveSessions(selectedPaths, target.FolderId);
        IsMoveSessionsDialogOpen = false;
        LoadSavedSessions();
        RuntimeLog = movedCount == 0
            ? "No sessions were moved."
            : $"Moved {movedCount} session(s) to {target.DisplayName}.";
    }

    private void LoadSessionFolders()
    {
        SessionFolders.Clear();

        foreach (var folder in _sessionStorage.GetFolders())
        {
            SessionFolders.Add(new SessionFolderOptionViewModel(folder.Id, folder.Name));
        }

        SyncSelectedSessionFolderWithId(SessionFolderId);
    }

    private void SyncSelectedSessionFolderWithId(string? folderId)
    {
        var normalized = NormalizeSessionFolderId(folderId);
        var matched = SessionFolders.FirstOrDefault(folder =>
            string.Equals(folder.FolderId, normalized, StringComparison.OrdinalIgnoreCase));

        if (!ReferenceEquals(SelectedSessionFolder, matched))
        {
            SelectedSessionFolder = matched;
        }
    }

    partial void OnNewSessionFolderNameChanged(string value)
    {
        NewSessionFolderValidationError = ValidateNewSessionFolderName(value);
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(HasNewSessionFolderValidationError)));
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(CanCreateSessionFolder)));
        CreateSessionFolderCommand.NotifyCanExecuteChanged();
    }

    partial void OnRenameSessionFolderNameChanged(string value)
    {
        RenameSessionFolderValidationError = ValidateNewSessionFolderName(value);
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(HasRenameSessionFolderValidationError)));
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(CanRenameSessionFolder)));
        RenameSessionFolderCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedSessionFolderChanged(SessionFolderOptionViewModel? value)
    {
        if (value is null)
        {
            return;
        }

        var normalized = NormalizeSessionFolderId(value.FolderId);
        if (!string.Equals(SessionFolderId, normalized, StringComparison.OrdinalIgnoreCase))
        {
            SessionFolderId = normalized;
        }

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(CanRenameSessionFolder)));
        RenameSessionFolderCommand.NotifyCanExecuteChanged();
    }

    private static string ValidateNewSessionFolderName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Folder name is required.";
        }

        var trimmed = value.Trim();
        var invalidChars = Path.GetInvalidFileNameChars();

        if (trimmed.Contains('/') || trimmed.Contains('\\'))
            return "Nested folders are not supported.";

        if (string.Equals(trimmed, CaptureSessionStorage.GlobalSessionFolderName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, CaptureSessionStorage.GlobalSessionFolderId, StringComparison.OrdinalIgnoreCase))
            return "`Global` is reserved.";

        if (trimmed.Any(ch => invalidChars.Contains(ch)))
            return "Folder name contains invalid characters.";

        return string.Empty;
    }

    private string NormalizeSessionFolderId(string? folderId)
    {
        return string.IsNullOrWhiteSpace(folderId)
            ? CaptureSessionStorage.GlobalSessionFolderId
            : _sessionStorage.ResolveFolderId(folderId);
    }

    private string FormatSessionFolderLabel(string folderId)
    {
        var name = _sessionStorage.NormalizeSessionTreePath(folderId);
        return string.Equals(folderId, CaptureSessionStorage.GlobalSessionFolderId, StringComparison.OrdinalIgnoreCase)
            ? "Global"
            : name;
    }
}
