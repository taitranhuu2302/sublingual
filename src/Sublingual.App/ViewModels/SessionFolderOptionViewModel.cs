using Sublingual.App.Services;

namespace Sublingual.App.ViewModels;

public sealed class SessionFolderOptionViewModel
{
    public SessionFolderOptionViewModel(string folderId, string name)
    {
        FolderId = folderId;
        Name = name;
    }

    public string FolderId { get; }
    public string Name { get; }
    public string DisplayName => string.Equals(FolderId, CaptureSessionStorage.GlobalSessionFolderId, StringComparison.OrdinalIgnoreCase)
        ? "Global"
        : Name;
}
