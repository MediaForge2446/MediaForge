using MediaForge.Core.Enums;
using MediaForge.Core.Models;
using MediaForge.Services;
using MediaForge.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace MediaForge.Views;

public sealed partial class MediaDownloaderDialog : ContentDialog
{
    private readonly MainViewModel _main;
    private CancellationTokenSource? _resolveCts;
    private bool _disposed;

    public MediaDownloaderViewModel ViewModel { get; }

    public MediaDownloaderDialog(MainViewModel main, Func<string?> targetFolderProvider)
    {
        _main = main ?? throw new ArgumentNullException(nameof(main));
        ArgumentNullException.ThrowIfNull(targetFolderProvider);

        ViewModel = new MediaDownloaderViewModel(
            new MediaResolverService(),
            main.State.PendingChanges,
            main.State.StagingHistory,
            main.ReportError,
            targetFolderProvider);

        InitializeComponent();
        DataContext = this;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        UpdateUiState();
    }

    private async void OnDetectClick(object sender, RoutedEventArgs e) => await ResolveNowAsync();

    private async void OnUrlKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Enter)
        {
            return;
        }

        e.Handled = true;
        await ResolveNowAsync();
    }

    private void OnUrlTextChanged(object sender, TextChangedEventArgs e)
    {
        ViewModel.SourceUrl = UrlTextBox.Text;
        ScheduleAutoResolve();
    }

    private void OnSelectAllClick(object sender, RoutedEventArgs e) => ViewModel.SelectAll();
    private void OnDeselectAllClick(object sender, RoutedEventArgs e) => ViewModel.SelectAll(false);

    private void OnMp3AllClick(object sender, RoutedEventArgs e)
    {
        ViewModel.SetAllFormat(MediaFormat.Mp3);
        UpdateUiState();
    }

    private void OnMp4AllClick(object sender, RoutedEventArgs e)
    {
        ViewModel.SetAllFormat(MediaFormat.Mp4);
        UpdateUiState();
    }

    private void OnItemSelectionChanged(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox && checkBox.DataContext is MediaItem item)
        {
            ViewModel.ToggleSelection(item, checkBox.IsChecked == true);
        }
    }

    private void OnTitleLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox textBox || textBox.DataContext is not MediaItem item)
        {
            return;
        }

        var title = textBox.Text.Trim();
        if (title.Length == 0)
        {
            textBox.Text = item.Title ?? "Media item";
            return;
        }

        ViewModel.Rename(item, title);
    }

    private void OnFormatChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 0 ||
            sender is not ComboBox comboBox ||
            comboBox.DataContext is not MediaItem item ||
            e.AddedItems[0] is not ComboBoxItem option ||
            option.Tag is not string tag ||
            !Enum.TryParse<MediaFormat>(tag, true, out var format))
        {
            return;
        }

        ViewModel.SetFormat(item, format);
    }

    private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        try
        {
            if (ViewModel.SelectedCount == 0)
            {
                args.Cancel = true;
                _main.ReportError(new InvalidOperationException("Select at least one media item."));
                return;
            }

            if (string.IsNullOrWhiteSpace(ViewModel.TargetFolderPath))
            {
                args.Cancel = true;
                _main.ReportError(new InvalidOperationException("Open a library folder before adding media."));
                return;
            }

            ViewModel.AddSelectedToPendingChanges();
        }
        catch (Exception exception)
        {
            args.Cancel = true;
            _main.ReportError(exception);
        }
    }

    private void OnSecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        DisposeResources();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => UpdateUiState();

    private async Task ResolveNowAsync()
    {
        CancelPendingResolve();
        _resolveCts = new CancellationTokenSource();
        var cancellationToken = _resolveCts.Token;
        try
        {
            await ViewModel.ResolveAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _main.ReportError(exception);
        }
    }

    private void ScheduleAutoResolve()
    {
        CancelPendingResolve();
        if (!ViewModel.CanResolve)
        {
            return;
        }

        _resolveCts = new CancellationTokenSource();
        var cancellationToken = _resolveCts.Token;
        _ = AutoResolveAsync(cancellationToken);
    }

    private async Task AutoResolveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(450, cancellationToken);
            if (ViewModel.CanResolve && !ViewModel.IsResolving)
            {
                await ViewModel.ResolveAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _main.ReportError(exception);
        }
    }

    private void CancelPendingResolve()
    {
        try
        {
            _resolveCts?.Cancel();
            _resolveCts?.Dispose();
            _resolveCts = null;
        }
        catch
        {
        }
    }

    private void DisposeResources()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CancelPendingResolve();
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
    }

    private void UpdateUiState()
    {
        MediaTypeText.Text = ViewModel.MediaTypeLabel;
        SelectedSummaryText.Text = ViewModel.SelectedSummary;
        ResolveProgressRing.IsActive = ViewModel.IsResolving;
        PlaylistActionsPanel.Visibility = ViewModel.IsPlaylist ? Visibility.Visible : Visibility.Collapsed;
        PrimaryButtonText = ViewModel.SelectedCount > 0 ? $"Add {ViewModel.SelectedCount} to changes" : "Add to changes";
        IsPrimaryButtonEnabled = ViewModel.SelectedCount > 0 && !ViewModel.IsResolving && ViewModel.CanResolve || ViewModel.SelectedCount > 0 && !ViewModel.IsResolving;
    }
}
