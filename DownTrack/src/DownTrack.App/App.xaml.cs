using DownTrack.Application.Abstractions;
using DownTrack.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using DownTrack.App.Services;
using DownTrack.App.ViewModels;

namespace DownTrack.App;

public partial class App : Application
{
    private readonly IHost _host;
    private Window? _window;

    public App()
    {
        InitializeComponent();

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<ILibraryCatalog, InMemoryLibraryCatalog>();
                services.AddSingleton<IStagingService, InMemoryStagingService>();
                services.AddSingleton<IFolderPicker, WinUiFolderPicker>();
                services.AddTransient<HomeViewModel>();
                services.AddTransient<MainWindow>();
            })
            .Build();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window ??= _host.Services.GetRequiredService<MainWindow>();
        _window.Activate();
    }
}
