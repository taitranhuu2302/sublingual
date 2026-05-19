using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Sublingual.App.Views;

public partial class OverlayWindow : Window
{
    public event EventHandler? OverlayHidden;

    public OverlayWindow()
    {
        InitializeComponent();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        // Keep the shared overlay instance alive. Manual close should behave like hide.
        if (!e.IsProgrammatic)
        {
            e.Cancel = true;
            Hide();
            OverlayHidden?.Invoke(this, EventArgs.Empty);
            return;
        }

        base.OnClosing(e);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e)
    {
        Hide();
        OverlayHidden?.Invoke(this, EventArgs.Empty);
    }
}
