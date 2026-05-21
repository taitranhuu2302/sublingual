using CommunityToolkit.Mvvm.ComponentModel;

namespace Sublingual.App.ViewModels;

public sealed partial class AudioLevelBarViewModel : ObservableObject
{
    [ObservableProperty] private double height = 10;
    [ObservableProperty] private double opacity = 0.30;
}
