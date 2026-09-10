using System.Collections.ObjectModel;
using MediaForge.Core.Enums;
using MediaForge.Core.Interfaces;
using MediaForge.Core.Models;
using MediaForge.State;

namespace MediaForge.ViewModels;

public sealed class MediaDownloaderViewModel : ViewModelBase
{
    private readonly IMediaResolver _resolver;
    private readonly PendingChangesState _pendingChanges;
    private readonly MainViewModel _main;
    private readonly string _targetFolderPath;
    private string _sourceUrl = string.Empty;
    private bool _isResolving;

    public ObservableCollection<MediaItem> Items { get; } = new();

    public string SourceUrl
    {
        get => _sourceUrl;
        set
        {
            if (SetProperty(ref _sourceUrl, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(CanResolve));
            }
        }
    }

    public bool IsPlaylist => Items.Count > 1;

    public bool IsResolving
    {
        get => _isResolving;
        private set
        {
            if (SetProperty(ref _isResolving, value))
            {
                OnPropertyChanged(nameof(CanResolve));
            }
        }
    }

    public bool CanResolve => !IsResolving && !string.IsNullOrWhiteSpace(SourceUrl);

    public int SelectedCount => Items.Count(item => item.IsSelected);

    public MediaDownloaderViewModel(
        IMediaResolver resolver,
        PendingChangesState pendingChanges,
        MainViewModel main,
        string targetFolderPath)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _pendingChanges = pendingChanges ?? throw new ArgumentNullException(nameof(pendingChanges));
        _main = main ?? throw new ArgumentNullException(nameof(main));
        _targetFolderPath = Path.GetFullPath(
            string.IsNullOrWhiteSpace(targetFolderPath) ? Environment.GetFolderPath(Environment.SpecialFolder.MyMusic) : targetFolderPath);
    }

    public async Task ResolveAsync(CancellationToken cancellationToken = default)
    {
        if (!CanResolve)
        {
            return;
        }

        try
        {
            IsResolving = true;
            var items = await _resolver.ResolveAsync(SourceUrl, cancellationToken).ConfigureAwait(true);
            Items.Clear();
            foreach (var item in items)
            {
                Items.Add(item);
            }

            OnPropertyChanged(nameof(IsPlaylist));
            OnPropertyChanged(nameof(SelectedCount));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _main.ReportError(new InvalidOperationException("Media detection was canceled."));
        }
        catch (Exception exception)
        {
            _main.ReportError(exception);
        }
        finally
        {
            IsResolving = false;
        }
    }

    public void ToggleSelection(MediaItem item, bool isSelected)
    {
        ArgumentNullException.ThrowIfNull(item);
        ReplaceItem(item with { IsSelected = isSelected });
    }

    public void Rename(MediaItem item, string title)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ReplaceItem(item with { Title = title.Trim() });
    }

    public void SetFormat(MediaItem item, MediaFormat format)
    {
        ArgumentNullException.ThrowIfNull(item);
        ReplaceItem(item with { Format = format });
    }

    public void SelectAll(bool selected = true)
    {
        for (var index = 0; index < Items.Count; index++)
        {
            Items[index] = Items[index] with { IsSelected = selected };
        }

        OnPropertyChanged(nameof(SelectedCount));
    }

    public void SetAllFormat(MediaFormat format)
    {
        for (var index = 0; index < Items.Count; index++)
        {
            Items[index] = Items[index] with { Format = format };
        }
    }

    public int AddSelectedToPendingChanges()
    {
        var selectedItems = Items.Where(item => item.IsSelected).ToArray();
        foreach (var item in selectedItems)
        {
            var safeTitle = SanitizeFileName(string.IsNullOrWhiteSpace(item.Title) ? "Media item" : item.Title.Trim());
            var extension = item.Format == MediaFormat.Mp4 ? ".mp4" : ".mp3";
            var targetPath = CreateUniquePendingPath(Path.Combine(_targetFolderPath, safeTitle + extension));

            _pendingChanges.Add(new PendingChange
            {
                Id = Guid.NewGuid(),
                Type = ChangeType.Download,
                Status = ChangeStatus.Pending,
                SourcePath = item.SourceUrl,
                TargetPath = targetPath,
                CreatedAtUtc = DateTimeOffset.UtcNow
            });
        }

        return selectedItems.Length;
    }

    private void ReplaceItem(MediaItem replacement)
    {
        var index = Items.IndexOf(replacement);
        if (index < 0)
        {
            index = Items
                .Select((item, currentIndex) => (item, currentIndex))
                .FirstOrDefault(result => result.item.Id == replacement.Id).currentIndex;
        }

        if (index >= 0 && index < Items.Count)
        {
            Items[index] = replacement;
            OnPropertyChanged(nameof(SelectedCount));
        }
    }

    private string CreateUniquePendingPath(string desiredPath)
    {
        var candidate = desiredPath;
        var extension = Path.GetExtension(desiredPath);
        var basePath = desiredPath[..^extension.Length];
        var counter = 2;

        while (File.Exists(candidate) || Directory.Exists(candidate) ||
               _pendingChanges.Changes.Any(change => string.Equals(change.TargetPath, candidate, StringComparison.OrdinalIgnoreCase)))
        {
            candidate = $"{basePath} ({counter}){extension}";
            counter++;
        }

        return candidate;
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(value.Select(character => invalid.Contains(character) ? '_' : character).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "Media item" : sanitized;
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
    }
}
