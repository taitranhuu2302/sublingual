using CommunityToolkit.Mvvm.ComponentModel;

namespace Sublingual.App.ViewModels;

/// <summary>
/// A single caption pair: one original-language line + one translated line.
/// IsCommitted = false means it is the live draft still being spoken.
/// </summary>
public sealed partial class OverlayCaptionLine : ObservableObject
{
    [ObservableProperty] private string originalText = string.Empty;
    [ObservableProperty] private string translatedText = string.Empty;
    [ObservableProperty] private bool isCommitted;
}
