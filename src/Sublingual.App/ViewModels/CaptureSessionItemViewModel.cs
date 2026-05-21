using CommunityToolkit.Mvvm.ComponentModel;
using Sublingual.App.Models;
using Sublingual.App.Services;

namespace Sublingual.App.ViewModels;

public sealed partial class CaptureSessionItemViewModel : ObservableObject
{
    public CaptureSessionItemViewModel(CaptureSessionRecord record)
    {
        SessionId = record.SessionId;
        FolderId = record.FolderId;
        Title = record.Title;
        TreePath = record.TreePath;
        DirectoryPath = record.DirectoryPath;
        AudioPath = record.AudioPath;
        TranscriptPath = record.TranscriptPath;
        MetadataPath = record.MetadataPath;
        CreatedAt = record.CreatedAt;
    }

    [ObservableProperty] private bool isSelected;

    public string SessionId { get; }
    public string FolderId { get; }
    public string Title { get; }
    public string TreePath { get; }
    public string DirectoryPath { get; }
    public string AudioPath { get; }
    public string TranscriptPath { get; }
    public string MetadataPath { get; }
    public DateTimeOffset CreatedAt { get; }
    public string CreatedAtText => CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
    public string DisplayTitle => string.IsNullOrWhiteSpace(Title) ? SessionId : Title;
    public string DisplayTreePath => string.IsNullOrWhiteSpace(TreePath)
        || string.Equals(TreePath, CaptureSessionStorage.GlobalSessionFolderName, StringComparison.OrdinalIgnoreCase)
            ? "Global"
            : TreePath.Replace('\\', '/');
    public string DisplaySessionId => SessionId.Length <= 16 ? SessionId : $"{SessionId[..8]}...{SessionId[^6..]}";
    public string DisplayAudioFileName => Path.GetFileName(AudioPath);
}
