using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Sublingual.App.ViewModels.SpeakingPractice;

namespace Sublingual.App.Views.SpeakingPractice;

public partial class PracticeSessionView : UserControl
{
    private PracticeSessionViewModel? _viewModel;

    public PracticeSessionView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
    }

    private void OnDetachedFromVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.Messages.CollectionChanged -= OnMessagesCollectionChanged;
        }

        DataContextChanged -= OnDataContextChanged;
        DetachedFromVisualTree -= OnDetachedFromVisualTree;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.Messages.CollectionChanged -= OnMessagesCollectionChanged;
        }

        _viewModel = DataContext as PracticeSessionViewModel;
        if (_viewModel is not null)
        {
            _viewModel.Messages.CollectionChanged += OnMessagesCollectionChanged;
        }
    }

    private void OnMessagesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action is not (NotifyCollectionChangedAction.Add or NotifyCollectionChangedAction.Replace or NotifyCollectionChangedAction.Reset))
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            var scrollViewer = this.FindControl<ScrollViewer>("MessagesScrollViewer");
            scrollViewer?.ScrollToEnd();
        }, DispatcherPriority.Background);
    }

    private void OnTypedMessageKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not PracticeSessionViewModel vm)
        {
            return;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            return;
        }

        if (vm.SendTypedMessageCommand.CanExecute(null))
        {
            vm.SendTypedMessageCommand.Execute(null);
            e.Handled = true;
        }
    }
}
