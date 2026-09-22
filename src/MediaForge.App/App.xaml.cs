using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using MediaForge.Application.Abstractions;
using MediaForge.Application.Commit;
using MediaForge.Application.Downloads;
using MediaForge.Application.Library;
using MediaForge.Application.Staging;
using MediaForge.App.Services;
using MediaForge.App.Localization;
using MediaForge.App.ViewModels;
using MediaForge.App.Views;
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
        LocalizationService.Instance.Initialize();
        var preferences = new UserPreferencesService();
        preferences.Initialize();
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        try
        {
            var paths = new LocalAppPaths();
            var store = new AtomicJsonStore();

            if (!IsCiSmokeMode())
            {
                var termsAcceptance = new TermsAcceptanceService(store, paths);
                if (!await termsAcceptance.HasAcceptedCurrentTermsAsync())
                {
                    var termsWindow = new TermsWindow();
                    var accepted = termsWindow.ShowDialog() == true;
                    if (!accepted)
                    {
                        Shutdown(0);
                        return;
                    }

                    await termsAcceptance.AcceptCurrentTermsAsync();
                }
            }
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
            var libraryScanService = new LibraryScanService(explorerService);
            IFileSystem fileSystem = new WindowsFileSystem();
            IMediaIndex mediaIndex = new JsonMediaIndex(store, paths);
            await mediaIndex.InitializeAsync();

            var downloadQueue = new DownloadQueue(maxConcurrency: 2);
            ICommitEngine commitEngine = new CommitEngine(
                fileSystem,
                mediaDownloader,
                stagingService,
                downloadQueue,
                mediaIndex);
            var mediaImportService = new MediaImportService(
                metadataResolver,
                stagingService,
                mediaIndex,
                fileSystem);
            var explorerViewModel = new ExplorerViewModel(explorerService, stagingService, mediaIndex);
            var updateService = new GitHubAppUpdateService();
            var downloadsViewModel = new DownloadsViewModel(
                mediaImportService,
                folderPicker,
                LocalizationService.Instance,
                preferences,
                downloadQueue);
            var settingsViewModel = new SettingsViewModel(
                toolManager,
                LocalizationService.Instance,
                preferences,
                updateService);
            var mainViewModel = new MainViewModel(
                libraryService,
                libraryScanService,
                folderPicker,
                stagingService,
                commitEngine,
                explorerViewModel,
                downloadsViewModel,
                settingsViewModel,
                LocalizationService.Instance);

            var window = new MainWindow(mainViewModel);
            MainWindow = window;
            window.Show();
            await mainViewModel.InitializeAsync();

            _ = settingsViewModel.CheckForUpdatesInBackgroundAsync();
        }
        catch (Exception ex)
        {
            if (IsCiSmokeMode())
            {
                WriteSmokeFailure(ex);
                Shutdown(-1);
                return;
            }

            System.Windows.MessageBox.Show(
                $"MediaForge could not start.\n\n{ex.Message}",
                "MediaForge",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        if (IsCiSmokeMode())
        {
            WriteSmokeFailure(e.Exception);
            e.Handled = true;
            Shutdown(-1);
            return;
        }

        System.Windows.MessageBox.Show(
            $"MediaForge encountered an unexpected error.\n\n{e.Exception.Message}",
            "MediaForge",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private static bool IsCiSmokeMode()
        => string.Equals(
            Environment.GetEnvironmentVariable("MEDIAFORGE_CI_SMOKE"),
            "1",
            StringComparison.Ordinal);

    private static void WriteSmokeFailure(Exception exception)
    {
        try
        {
            var path = Path.Combine(Path.GetTempPath(), "MediaForge.StartupSmoke.txt");
            File.WriteAllText(path, exception.ToString());
        }
        catch
        {
            // Never allow diagnostic logging to mask the original startup failure.
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e) => e.SetObserved();
}
