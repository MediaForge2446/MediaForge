using MediaForge.Core.Enums;
using MediaForge.Core.Interfaces;
using MediaForge.Core.Models;
using MediaForge.Services;
using MediaForge.State;

namespace MediaForge.ViewModels;

public sealed class MainViewModel : ViewModelBase, IDisposable
{
    private readonly IFileSystemService _fileSystemService;
    private readonly IDownloadService _downloadService;
    private readonly ILoggingService _loggingService;
    private readonly ICommitService _commitService;
    private readonly ISettingsService _settingsService;
    private readonly IToolManager _toolManager;
    private readonly IAppPersistenceService _persistenceService;
    private readonly StagingHistory _stagingHistory;
    private readonly CancellationTokenSource _lifetimeCts = new();
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    private string? _lastError;
    private int _changeVersion;
    private bool _isRestoring;
    private bool _disposed;

    public AppState State { get; } = new();
    public LibraryViewModel Library { get; }
    public ExplorerViewModel Explorer { get; }
    public PendingChangesViewModel PendingChanges { get; }
    public SettingsViewModel Settings { get; }
    public int PendingCount => State.PendingChanges.PendingCount;
    public string PendingCountText => $"{PendingCount} pending";

    public string? LastError
    {
        get => _lastError;
        private set
        {
            if (SetProperty(ref _lastError, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(LastError);

    public MainViewModel()
    {
        _fileSystemService = new FileSystemService();
        _loggingService = new LoggingService();
        _settingsService = new SettingsService();
        _toolManager = new ToolManager();
        _downloadService = new DownloadService(new YtDlpPathProvider(_toolManager.ToolsDirectory));
        _commitService = new CommitService(_fileSystemService, _downloadService, _loggingService);
        _persistenceService = new AppPersistenceService();
        _stagingHistory = State.StagingHistory;

        Library = new LibraryViewModel(State.Library, this);
        Explorer = new ExplorerViewModel(_fileSystemService, State.PendingChanges, State.Library, _stagingHistory, this);
        PendingChanges = new PendingChangesViewModel(State.PendingChanges, _commitService, _stagingHistory, this);
        Settings = new SettingsViewModel(_settingsService, _toolManager, this);

        State.Changed += OnStateChanged;
        _ = RestoreStateAsync(_lifetimeCts.Token);
    }

    public void ReportError(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        LastError = exception.Message;
        _loggingService.Error("A UI operation failed.", exception);
    }

    public void ClearError() => LastError = null;

    private async Task RestoreStateAsync(CancellationToken cancellationToken)
    {
        _isRestoring = true;
        try
        {
            var snapshot = await _persistenceService.LoadAsync(cancellationToken).ConfigureAwait(true);
            if (snapshot is null)
            {
                return;
            }

            State.Library.Restore(snapshot.RootFolders);
            State.PendingChanges.Replace(snapshot.PendingChanges);
            _stagingHistory.Clear();
            foreach (var change in snapshot.PendingChanges.Where(change => change.Status == ChangeStatus.Pending))
            {
                _stagingHistory.Record(change);
            }

            Interlocked.Exchange(ref _changeVersion, 0);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            ReportError(exception);
        }
        finally
        {
            _isRestoring = false;
            OnPropertyChanged(nameof(PendingCount));
            OnPropertyChanged(nameof(PendingCountText));
        }
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (_isRestoring || _disposed)
        {
            return;
        }

        OnPropertyChanged(nameof(PendingCount));
        OnPropertyChanged(nameof(PendingCountText));
        Interlocked.Increment(ref _changeVersion);
        _ = PersistSoonAsync(_lifetimeCts.Token);
    }

    private async Task PersistSoonAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
            var version = Volatile.Read(ref _changeVersion);
            await PersistSnapshotAsync(cancellationToken).ConfigureAwait(false);

            if (version != Volatile.Read(ref _changeVersion))
            {
                _ = PersistSoonAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            ReportError(exception);
        }
    }

    private async Task PersistSnapshotAsync(CancellationToken cancellationToken)
    {
        await _saveGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var snapshot = new AppStateSnapshot
            {
                SchemaVersion = 1,
                RootFolders = State.Library.RootFolders,
                PendingChanges = State.PendingChanges.Changes,
                SavedAtUtc = DateTimeOffset.UtcNow
            };

            await _persistenceService.SaveAsync(snapshot, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _saveGate.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        State.Changed -= OnStateChanged;
        _lifetimeCts.Cancel();

        try
        {
            PersistSnapshotAsync(CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            _loggingService.Error("Failed to persist application state during shutdown.", exception);
        }

        Settings.Dispose();
        if (_toolManager is IDisposable disposableTools)
        {
            disposableTools.Dispose();
        }

        if (_persistenceService is IDisposable disposablePersistence)
        {
            disposablePersistence.Dispose();
        }

        _saveGate.Dispose();
        _lifetimeCts.Dispose();
    }
}
