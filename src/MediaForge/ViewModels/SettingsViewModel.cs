using System.Collections.ObjectModel;
using MediaForge.Core.Interfaces;
using MediaForge.Core.Models;

namespace MediaForge.ViewModels;

public sealed class SettingsViewModel : ViewModelBase, IDisposable
{
    private readonly ISettingsService _settingsService;
    private readonly IToolManager _toolManager;
    private readonly MainViewModel _main;
    private CancellationTokenSource? _backgroundCts;
    private bool _isBusy;
    private double _progress;
    private string _statusMessage = "Ready";

    public ObservableCollection<ToolStatus> Tools { get; } = new();

    public bool AutomaticToolUpdates
    {
        get => _settingsService.Current.AutomaticToolUpdates;
        set
        {
            if (AutomaticToolUpdates == value)
            {
                return;
            }

            _ = SaveAutomaticUpdatesAsync(value);
            OnPropertyChanged();
        }
    }

    public string DownloadDirectory =>
        string.IsNullOrWhiteSpace(_settingsService.Current.DownloadDirectory)
            ? Environment.GetFolderPath(Environment.SpecialFolder.MyMusic)
            : _settingsService.Current.DownloadDirectory;

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(CanUpdate));
            }
        }
    }

    public bool CanUpdate => !IsBusy;

    public double Progress
    {
        get => _progress;
        private set => SetProperty(ref _progress, Math.Clamp(value, 0d, 1d));
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public SettingsViewModel(ISettingsService settingsService, IToolManager toolManager, MainViewModel main)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _toolManager = toolManager ?? throw new ArgumentNullException(nameof(toolManager));
        _main = main ?? throw new ArgumentNullException(nameof(main));

        _settingsService.Changed += OnSettingsChanged;
        _toolManager.StatusChanged += OnToolStatusChanged;
        RefreshTools(_toolManager.Status);
    }

    public async Task CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Checking for tool updates…";
            var status = await _toolManager.CheckForUpdatesAsync(cancellationToken).ConfigureAwait(true);
            RefreshTools(status);
            StatusMessage = status.Any(tool => tool.UpdateAvailable)
                ? "Updates are available"
                : "All tools are up to date";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            StatusMessage = "Update check canceled";
        }
        catch (Exception exception)
        {
            StatusMessage = "Update check failed";
            _main.ReportError(exception);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task UpdateAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            IsBusy = true;
            Progress = 0d;
            StatusMessage = "Updating tools…";
            var progress = new Progress<double>(value => Progress = value);
            var status = await _toolManager.UpdateAllAsync(progress, cancellationToken).ConfigureAwait(true);
            RefreshTools(status);
            Progress = 1d;
            StatusMessage = "Tools are ready";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            StatusMessage = "Tool update canceled";
        }
        catch (Exception exception)
        {
            StatusMessage = "Tool update failed";
            _main.ReportError(exception);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task StartBackgroundMaintenanceAsync()
    {
        _backgroundCts?.Cancel();
        _backgroundCts?.Dispose();
        _backgroundCts = new CancellationTokenSource();
        var token = _backgroundCts.Token;

        try
        {
            var status = await _toolManager.CheckForUpdatesAsync(token).ConfigureAwait(false);
            RefreshTools(status);

            if (_settingsService.Current.AutomaticToolUpdates && status.Any(tool => tool.UpdateAvailable))
            {
                var progress = new Progress<double>(value => Progress = value);
                var updated = await _toolManager.UpdateAllAsync(progress, token).ConfigureAwait(false);
                RefreshTools(updated);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            StatusMessage = "Background tool maintenance failed";
            _main.ReportError(exception);
        }
    }

    private async Task SaveAutomaticUpdatesAsync(bool enabled)
    {
        try
        {
            var settings = _settingsService.Current with { AutomaticToolUpdates = enabled };
            await _settingsService.SaveAsync(settings).ConfigureAwait(true);
            StatusMessage = enabled ? "Automatic updates enabled" : "Automatic updates disabled";
        }
        catch (Exception exception)
        {
            _main.ReportError(exception);
            OnPropertyChanged(nameof(AutomaticToolUpdates));
        }
    }

    private void RefreshTools(IEnumerable<ToolStatus> status)
    {
        Tools.Clear();
        foreach (var tool in status)
        {
            Tools.Add(tool);
        }
        OnPropertyChanged(nameof(Tools));
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(AutomaticToolUpdates));
        OnPropertyChanged(nameof(DownloadDirectory));
    }

    private void OnToolStatusChanged(object? sender, EventArgs e)
    {
        RefreshTools(_toolManager.Status);
    }

    public void Dispose()
    {
        _backgroundCts?.Cancel();
        _backgroundCts?.Dispose();
        _settingsService.Changed -= OnSettingsChanged;
        _toolManager.StatusChanged -= OnToolStatusChanged;
    }
}
