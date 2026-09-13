using System.Windows;
using MediaForge.Application.Library;
using MediaForge.Application.Staging;
using MediaForge.Core.State;
using MediaForge.Infrastructure.Persistence;

namespace MediaForge.App;

public partial class App : System.Windows.Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var paths = new LocalAppPaths();
        var store = new AtomicJsonStore();
        var libraryRepository = new JsonLibraryRepository(store, paths);
        var stagingRepository = new JsonStagingRepository(store, paths);
        var libraryService = new LibraryService(libraryRepository);
        var stagingService = new StagingService(stagingRepository, new StagingHistory());
        var folderPicker = new Services.WindowsFolderPicker();

        var viewModel = new ViewModels.MainViewModel(libraryService, folderPicker, stagingService);
        await viewModel.InitializeAsync();

        var window = new MainWindow(viewModel);
        MainWindow = window;
        window.Show();
    }
}
