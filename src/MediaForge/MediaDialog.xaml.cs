using System.Collections.ObjectModel;
using MediaForge.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MediaForge;

public sealed partial class MediaDialog : ContentDialog
{
    public ObservableCollection<MediaItem> Media { get; } = new();
    public MainViewModel Model => ((MainWindow)App.MainWindow!).ViewModel;
    private CancellationTokenSource? _resolveCts;

    public MediaDialog()
    {
        InitializeComponent();
        DataContext = this;
        Closed += OnDialogClosed;
    }

    private void UrlChanged(object sender, TextChangedEventArgs e) =>
        Summary.Text = string.IsNullOrWhiteSpace(UrlBox.Text) ? "Paste a URL to begin" : "Ready to resolve";

    private async void Resolve(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(UrlBox.Text))
        {
            return;
        }

        try
        {
            _resolveCts?.Cancel();
            _resolveCts?.Dispose();
            _resolveCts = new CancellationTokenSource();
            Busy.IsActive = true;
            IsPrimaryButtonEnabled = false;
            Media.Clear();

            var items = await Model.Resolver.ResolveAsync(UrlBox.Text.Trim(), _resolveCts.Token);
            foreach (var item in items)
            {
                Media.Add(item);
            }

            Summary.Text = items.Count == 1 ? "1 video resolved" : $"{items.Count} playlist items resolved";
            Destination.Text = Model.CurrentPath;
        }
        catch (OperationCanceledException) when (_resolveCts?.IsCancellationRequested == true)
        {
        }
        catch (Exception ex)
        {
            Model.ReportError(ex);
            Summary.Text = "Resolve failed";
        }
        finally
        {
            Busy.IsActive = false;
            IsPrimaryButtonEnabled = true;
        }
    }

    private void SelectAll(object sender, RoutedEventArgs e)
    {
        foreach (var item in Media)
        {
            item.IsSelected = true;
        }
    }

    private void ClearAll(object sender, RoutedEventArgs e)
    {
        foreach (var item in Media)
        {
            item.IsSelected = false;
        }
    }

    private void AllMp3(object sender, RoutedEventArgs e)
    {
        foreach (var item in Media)
        {
            item.Format = MediaFormat.Mp3;
        }
    }

    private void AllMp4(object sender, RoutedEventArgs e)
    {
        foreach (var item in Media)
        {
            item.Format = MediaFormat.Mp4;
        }
    }

    private void CommitDialog(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        try
        {
            var folder = Model.CurrentPath;
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                args.Cancel = true;
                Model.ReportError(new InvalidOperationException("Open a destination folder before adding media."));
                return;
            }

            foreach (var item in Media.Where(x => x.IsSelected))
            {
                var extension = item.Format == MediaFormat.Mp4 ? ".mp4" : ".mp3";
                var target = Path.Combine(folder, Sanitize(item.Title) + extension);
                Model.Stage(new PendingChange
                {
                    Kind = ChangeKind.Download,
                    SourcePath = item.Url,
                    SourceUrl = item.Url,
                    TargetPath = target,
                    DisplayName = item.Title,
                    Format = item.Format
                });
            }
        }
        catch (Exception ex)
        {
            args.Cancel = true;
            Model.ReportError(ex);
        }
    }

    private void OnDialogClosed(ContentDialog sender, ContentDialogClosedEventArgs args)
    {
        _resolveCts?.Cancel();
        _resolveCts?.Dispose();
        _resolveCts = null;
    }

    private static string Sanitize(string name)
    {
        foreach (var character in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(character, '_');
        }

        name = name.Trim().TrimEnd('.');
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Media";
        }

        return name.Length <= 120 ? name : name[..120];
    }
}
