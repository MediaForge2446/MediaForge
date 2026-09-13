using System.Windows;
using System.Windows.Threading;
using MediaForge.Application.Abstractions;
using MediaForge.Application.Commit;
using MediaForge.Application.Downloads;
using MediaForge.Application.Library;
using MediaForge.Application.Staging;
using MediaForge.App.Services;
using MediaForge.App.ViewModels;
using MediaForge.Core.Interfaces;
using MediaForge.Core.State;
using MediaForge.Infrastructure.FileSystem;
using MediaForge.Infrastructure.Media;
using MediaForge.Infrastructure.Persistence;
using MediaForge.Infrastructure.Tools;

namespace MediaForge.App;

public partial class App : System.Windows.Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        try
        {
            var paths = new LocalAppPaths();
            var store = new AtomicJsonStore();
            var libraryRepository = new JsonLibraryRepository(store, paths);
            var stagingRepository = new JsonStagingRepository(store, paths);
            var libraryService = new LibraryService(libraryRepository);
            var stagingService = new StagingService(stagingRepository, new StagingHistory());
            var folderPicker = new WindowsFolderPicker();

            var toolManager = new ManagedToolManager(paths);
            IYtDlpRunner ytDlpRunner = new YtDlpProcessRunner(toolManager, paths);
            IMediaDownloader mediaDownloader = new YtDlpMediaDownloader(ytDlpRunner);
            IMediaMetadataResolver metadataResolver = new YoutubeMediaMetadataResolver();
            IExplorerService explorerService = new WindowsExplorerService();
            IFileSystem fileSystem = new WindowsFileSystem();
            var downloadQueue = new DownloadQueue(maxConcurrency: 2);
            ICommitEngine commitEngine = new CommitEngine(
                fileSystem,
                mediaDownloader,
                stagingService,
                downloadQueue);

            var mediaImportService = new MediaImportService(metadataResolver, stagingService);
            var explorerViewModel = new ExplorerViewModel(explorerService, stagingService);
            var downloadsViewModel = new DownloadsViewModel(mediaImportService, folderPicker);
            var settingsViewModel = new SettingsViewModel(toolManager);
            var mainViewModel = new MainViewModel(
                libraryService,
                folderPicker,
                stagingService,
                commitEngine,
                explorerViewModel,
                downloadsViewModel,
                settingsViewModel);

            await mainViewModel.InitializeAsync();

            var window = new MainWindow(mainViewModel);
            MainWindow = window;
            window.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"MediaForge could not start.\n\n{ex.Message}",
                "MediaForge",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"MediaForge encountered an unexpected error.\n\n{e.Exception.Message}",
            "MediaForge",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
    }
}
