using MediaForge.Core.Interfaces;
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

    public AppState State { get; } = new();
    public LibraryViewModel Library { get; }
    public ExplorerViewModel Explorer { get; }
    public PendingChangesViewModel PendingChanges { get; }
    public SettingsViewModel Settings { get; }

    private string? _lastError;
    public string? LastError
    {
        get => _lastError;
        private set => SetProperty(ref _lastError, value);
    }

    public MainViewModel()
    {
        _fileSystemService = new FileSystemService();
        _downloadService = new DownloadService();
        _loggingService = new LoggingService();
        _commitService = new CommitService(_fileSystemService, _downloadService, _loggingService);
        _settingsService = new SettingsService();
        _toolManager = new ToolManager();

        Library = new LibraryViewModel(State.Library, this);
        Explorer = new ExplorerViewModel(_fileSystemService, State.PendingChanges, State.Library, this);
        PendingChanges = new PendingChangesViewModel(State.PendingChanges, _commitService, this);
        Settings = new SettingsViewModel(_settingsService, _toolManager, this);
    }

    public void ReportError(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        LastError = exception.Message;
        _loggingService.Error("A UI operation failed.", exception);
    }

    public void Dispose()
    {
        Settings.Dispose();
        if (_toolManager is IDisposable disposableTools)
        {
            disposableTools.Dispose();
        }
    }
}
