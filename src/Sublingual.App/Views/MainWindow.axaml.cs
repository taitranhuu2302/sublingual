using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Sublingual.App.ViewModels;
using SukiUI.Controls;

namespace Sublingual.App.Views;

public partial class MainWindow : SukiWindow
{
    public MainWindow()
    {
        InitializeComponent();

        Opened += (_, _) =>
        {
            if (DataContext is MainWindowViewModel viewModel)
            {
                viewModel.PickSessionsDirectoryAsync = PickSessionsDirectoryAsync;
                viewModel.PickSpeechToTextModelsRootDirectoryAsync = PickSpeechToTextModelsRootDirectoryAsync;
                viewModel.PickSpeechToTextModelDirectoryAsync = PickSpeechToTextModelDirectoryAsync;
                viewModel.PickSpeechToTextModelZipFileAsync = PickSpeechToTextModelZipFileAsync;
            }
        };
    }

    private async Task<string?> PickSessionsDirectoryAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select sessions folder",
            AllowMultiple = false,
        });

        return folders.Count == 0 ? null : folders[0].TryGetLocalPath();
    }

    private async Task<string?> PickSpeechToTextModelDirectoryAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select speech-to-text model folder",
            AllowMultiple = false,
        });

        return folders.Count == 0 ? null : folders[0].TryGetLocalPath();
    }

    private async Task<string?> PickSpeechToTextModelsRootDirectoryAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select managed models folder",
            AllowMultiple = false,
        });

        return folders.Count == 0 ? null : folders[0].TryGetLocalPath();
    }

    private async Task<string?> PickSpeechToTextModelZipFileAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select zipped speech-to-text model",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Zip archive")
                {
                    Patterns = ["*.zip"],
                    MimeTypes = ["application/zip"],
                },
            ],
        });

        return files.Count == 0 ? null : files[0].TryGetLocalPath();
    }
}
