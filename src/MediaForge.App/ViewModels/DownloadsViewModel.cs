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

    [ObservableProperty]
    private string _sourceUrl = string.Empty;

    [ObservableProperty]
    private string _destinationDirectory = string.Empty;

    [ObservableProperty]
    private MediaFormat _selectedFormat = MediaFormat.Mp3;

    [ObservableProperty]
    private string _collectionTitle = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = "הדבק קישור ל־YouTube כדי להתחיל";

    public ObservableCollection<ResolvedMediaItemViewModel> Items { get; } = [];

    public IReadOnlyList<MediaFormat> Formats { get; } = [MediaFormat.Mp3, MediaFormat.Mp4];

    public bool HasItems => Items.Count > 0;

    public DownloadsViewModel(MediaImportService importService, IFolderPicker folderPicker)
    {
        _importService = importService;
        _folderPicker = folderPicker;
    }

    public void SetDefaultDestination(string path)
    {
        if (string.IsNullOrWhiteSpace(DestinationDirectory))
            DestinationDirectory = path;
    }

    [RelayCommand]
    private async Task ResolveAsync(CancellationToken cancellationToken)
    {
        if (IsBusy || string.IsNullOrWhiteSpace(SourceUrl))
            return;

        IsBusy = true;
        Items.Clear();
        CollectionTitle = string.Empty;
        StatusText = "בודק את הקישור ומביא פרטי מדיה…";
        try
        {
            var result = await _importService.ResolveAsync(SourceUrl, cancellationToken).ConfigureAwait(true);
            CollectionTitle = result.CollectionTitle ?? string.Empty;
            foreach (var item in result.Items)
                Items.Add(new ResolvedMediaItemViewModel(item));

            StatusText = result.IsPlaylist
                ? $"נמצאו {Items.Count} פריטים בפלייליסט"
                : "נמצא שיר אחד — אפשר לערוך לפני הוספה";
            OnPropertyChanged(nameof(HasItems));
        }
        catch (OperationCanceledException)
        {
            StatusText = "הפעולה בוטלה";
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task PickDestinationAsync(CancellationToken cancellationToken)
    {
        if (IsBusy)
            return;

        var path = await _folderPicker.PickFolderAsync(cancellationToken).ConfigureAwait(true);
        if (!string.IsNullOrWhiteSpace(path))
            DestinationDirectory = path;
    }

    [RelayCommand]
    private void SelectAll(bool? selected)
    {
        var value = selected ?? true;
        foreach (var item in Items)
            item.IsSelected = value;
    }

    [RelayCommand]
    private void Mp3ToAll()
    {
        SelectedFormat = MediaFormat.Mp3;
        foreach (var item in Items)
            item.IsSelected = true;
        StatusText = "כל הפריטים נבחרו כ־MP3";
    }

    [RelayCommand]
    private async Task StageSelectedAsync(CancellationToken cancellationToken)
    {
        if (IsBusy)
            return;
        if (string.IsNullOrWhiteSpace(DestinationDirectory))
        {
            StatusText = "בחר תיקיית יעד";
            return;
        }

        var selected = Items.Where(x => x.IsSelected).Select(x => x.ToResolvedItem()).ToArray();
        if (selected.Length == 0)
        {
            StatusText = "בחר לפחות פריט אחד";
            return;
        }

        IsBusy = true;
        try
        {
            await _importService.StageDownloadsAsync(
                selected,
                DestinationDirectory,
                SelectedFormat,
                cancellationToken).ConfigureAwait(true);

            StatusText = $"{selected.Length} הורדות נוספו לשינויים ממתינים";
        }
        catch (OperationCanceledException)
        {
            StatusText = "הפעולה בוטלה";
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}

public partial class ResolvedMediaItemViewModel : ObservableObject
{
    private readonly ResolvedMediaItem _item;

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private bool _isSelected = true;

    public string VideoId => _item.VideoId;
    public string SourceUrl => _item.SourceUrl;
    public string Artist => _item.Metadata.Artist ?? "YouTube";
    public string DurationText => _item.Metadata.Duration is { } duration
        ? duration.ToString(@"hh\:mm\:ss")
        : "—";
    public string? ThumbnailUrl => _item.Metadata.ThumbnailUrl;

    public ResolvedMediaItemViewModel(ResolvedMediaItem item)
    {
        _item = item;
        _title = item.Metadata.Title;
    }

    public ResolvedMediaItem ToResolvedItem()
        => _item with { Metadata = _item.Metadata with { Title = Title.Trim() } };
}
