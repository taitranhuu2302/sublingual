using Avalonia.Controls;
using System.ComponentModel;
using Sublingual.App.ViewModels;

namespace Sublingual.App.Views;

public partial class OverlayWindow : Window
{
    private ScrollViewer? _darkTranscriptScrollViewer;
    private ScrollViewer? _lightTranscriptScrollViewer;
    private bool _suppressScrollSync;

    public event EventHandler? OverlayHidden;

    public OverlayWindow()
    {
        InitializeComponent();
        _darkTranscriptScrollViewer = this.FindControl<ScrollViewer>("DarkTranscriptScrollViewer");
        _lightTranscriptScrollViewer = this.FindControl<ScrollViewer>("LightTranscriptScrollViewer");
        AttachScrollViewerHandlers(_darkTranscriptScrollViewer);
        AttachScrollViewerHandlers(_lightTranscriptScrollViewer);

        SizeChanged += (_, _) =>
        {
            if (DataContext is OverlayWindowViewModel viewModel)
            {
                viewModel.OverlayWidth = Width;
                viewModel.OverlayHeight = Height;
            }
        };

        Opened += (_, _) => AttachViewModelHandlers();
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


    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        AttachViewModelHandlers();
    }

    private void AttachViewModelHandlers()
    {
        if (DataContext is not OverlayWindowViewModel viewModel)
        {
            return;
        }

        viewModel.PropertyChanged -= OnOverlayViewModelPropertyChanged;
        viewModel.PropertyChanged += OnOverlayViewModelPropertyChanged;
    }

    private void OnOverlayViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not OverlayWindowViewModel viewModel)
        {
            return;
        }

        if (e.PropertyName == nameof(OverlayWindowViewModel.ScrollRequestVersion) && viewModel.IsFixedToBottom)
        {
            ScrollToBottom();
        }

        if (e.PropertyName == nameof(OverlayWindowViewModel.IsFixedToBottom) && viewModel.IsFixedToBottom)
        {
            ScrollToBottom();
        }
    }

    private void ScrollToBottom()
    {
        try
        {
            _suppressScrollSync = true;
            _darkTranscriptScrollViewer?.ScrollToEnd();
            _lightTranscriptScrollViewer?.ScrollToEnd();
        }
        finally
        {
            _suppressScrollSync = false;
        }
    }

    private void AttachScrollViewerHandlers(ScrollViewer? scrollViewer)
    {
        if (scrollViewer is null)
        {
            return;
        }

        scrollViewer.ScrollChanged += OnTranscriptScrollChanged;
    }

    private void OnTranscriptScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_suppressScrollSync || DataContext is not OverlayWindowViewModel viewModel || sender is not ScrollViewer scrollViewer)
        {
            return;
        }

        var distanceFromBottom = scrollViewer.Extent.Height - (scrollViewer.Offset.Y + scrollViewer.Viewport.Height);
        var isNearBottom = distanceFromBottom <= 12;

        if (!isNearBottom && viewModel.IsFixedToBottom)
        {
            viewModel.IsFixedToBottom = false;
        }
    }

}
