using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediaForge.Application.Downloads;
using MediaForge.Core.Enums;
using MediaForge.Core.Models;
using MediaForge.App.Services;

namespace MediaForge.App.ViewModels;

public partial class DownloadsViewModel : ObservableObject
{
    private readonly MediaImportService _importService;
    private readonly IFolderPicker _folderPicker;

    [ObservableProperty] private string _sourceUrl = string.Empty;
    [ObservableProperty] private string _destinationDirectory = string.Empty;
    [ObservableProperty] private MediaFormat _selectedFormat = MediaFormat.Mp3;
    [ObservableProperty] private string _collectionTitle = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusText = "הדבק קישור כדי להתחיל";

    public ObservableCollection<ResolvedMediaItemViewModel> Items { get; } = [];
    public IReadOnlyList<MediaFormat> Formats { get; } = [MediaFormat.Mp3, MediaFormat.Mp4, MediaFormat.Wav, MediaFormat.M4a];
    public bool HasItems => Items.Count > 0;
    public int SelectedCount => Items.Count(x => x.IsSelected);
    public bool HasSelection => SelectedCount > 0;
    public event Func<Task>? MediaStaged;

    public DownloadsViewModel(MediaImportService importService, IFolderPicker folderPicker)
    {
        _importService = importService;
        _folderPicker = folderPicker;
    }

    public void SetDefaultDestination(string path)
    {
        if (string.IsNullOrWhiteSpace(DestinationDirectory)) DestinationDirectory = path;
    }

    public void SetDestination(string path)
    {
        if (!string.IsNullOrWhiteSpace(path)) DestinationDirectory = path;
    }

    public void PrepareForFolder(string path)
    {
        DestinationDirectory = path;
        SourceUrl = string.Empty;
        CollectionTitle = string.Empty;
        StatusText = "הדבק קישור כדי להתחיל";
        UnsubscribeItems();
        Items.Clear();
        NotifySelectionState();
    }

    private void NotifySelectionState()
    {
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(HasSelection));
    }

    private void SubscribeItem(ResolvedMediaItemViewModel item)
        => item.PropertyChanged += OnMediaItemPropertyChanged;

    private void UnsubscribeItems()
    {
        foreach (var item in Items)
            item.PropertyChanged -= OnMediaItemPropertyChanged;
    }

    private void OnMediaItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ResolvedMediaItemViewModel.IsSelected))
        {
            OnPropertyChanged(nameof(SelectedCount));
            OnPropertyChanged(nameof(HasSelection));
        }
    }

    [RelayCommand]
    private async Task ResolveAsync(CancellationToken cancellationToken)
    {
        if (IsBusy || string.IsNullOrWhiteSpace(SourceUrl)) return;
        IsBusy = true;
        UnsubscribeItems();
        Items.Clear();
        CollectionTitle = string.Empty;
        StatusText = "בודק קישור ומביא פרטי מדיה…";
        NotifySelectionState();
        try
        {
            var result = await _importService.ResolveAsync(SourceUrl.Trim(), cancellationToken).ConfigureAwait(true);
            CollectionTitle = result.CollectionTitle ?? string.Empty;
            foreach (var item in result.Items)
            {
                var itemViewModel = new ResolvedMediaItemViewModel(item, Formats);
                SubscribeItem(itemViewModel);
                Items.Add(itemViewModel);
            }

            StatusText = result.IsPlaylist
                ? $"נמצאו {Items.Count} שירים · נבחרו {SelectedCount}"
                : "נמצא שיר אחד";
            NotifySelectionState();
        }
        catch (OperationCanceledException) { StatusText = "הפעולה בוטלה"; }
        catch (Exception ex) { StatusText = $"לא ניתן לקרוא את הקישור: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var item in Items)
            item.IsSelected = true;

        StatusText = $"נבחרו {SelectedCount} מתוך {Items.Count} שירים";
        NotifySelectionState();
    }

    [RelayCommand]
    private void Mp3ToAll()
    {
        foreach (var item in Items)
        {
            item.DesiredFormat = MediaFormat.Mp3;
            item.IsSelected = true;
        }

        SelectedFormat = MediaFormat.Mp3;
        StatusText = $"כל {SelectedCount} השירים הוגדרו כ־MP3";
        NotifySelectionState();
    }

    [RelayCommand]
    private async Task StageSelectedAsync(CancellationToken cancellationToken)
    {
        if (IsBusy) return;
        if (string.IsNullOrWhiteSpace(DestinationDirectory)) { StatusText = "לא נבחרה תיקייה"; return; }

        var selected = Items.Where(x => x.IsSelected).Select(x => x.ToResolvedItem()).ToArray();
        if (selected.Length == 0) { StatusText = "בחר לפחות שיר אחד"; return; }

        IsBusy = true;
        StatusText = "מוסיף את השירים לשינויים הממתינים…";
        try
        {
            var staged = await _importService.StageDownloadsAsync(selected, DestinationDirectory, SelectedFormat, cancellationToken).ConfigureAwait(true);
            StatusText = staged.Count == selected.Length
                ? $"{staged.Count} שירים נוספו לשינויים הממתינים"
                : $"{staged.Count} שירים נוספו; כפילויות דולגו";
            if (MediaStaged is { } handler)
                await handler().ConfigureAwait(true);
        }
        catch (OperationCanceledException) { StatusText = "הפעולה בוטלה"; }
        catch (Exception ex) { StatusText = $"לא ניתן להוסיף את השירים: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task PickDestinationAsync(CancellationToken cancellationToken)
    {
        if (IsBusy) return;
        var path = await _folderPicker.PickFolderAsync(cancellationToken).ConfigureAwait(true);
        if (!string.IsNullOrWhiteSpace(path)) DestinationDirectory = path;
    }
}

public partial class ResolvedMediaItemViewModel : ObservableObject
{
    private readonly ResolvedMediaItem _item;
    [ObservableProperty] private string _title;
    [ObservableProperty] private bool _isSelected = true;
    [ObservableProperty] private MediaFormat _desiredFormat = MediaFormat.Mp3;

    public IReadOnlyList<MediaFormat> Formats { get; }
    public string VideoId => _item.VideoId;
    public string SourceUrl => _item.SourceUrl;
    public string Artist => _item.Metadata.Artist ?? "YouTube";
    public string DurationText => _item.Metadata.Duration is { } duration ? duration.ToString(@"hh\:mm\:ss") : "—";
    public string? ThumbnailUrl => _item.Metadata.ThumbnailUrl;

    public ResolvedMediaItemViewModel(ResolvedMediaItem item, IReadOnlyList<MediaFormat> formats)
    {
        _item = item;
        _title = item.Metadata.Title;
        _desiredFormat = item.DesiredFormat;
        Formats = formats;
    }

    public ResolvedMediaItem ToResolvedItem()
        => _item with { Metadata = _item.Metadata with { Title = Title.Trim() }, DesiredFormat = DesiredFormat };
}