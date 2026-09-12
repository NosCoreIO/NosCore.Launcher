using System.Net.Http;
using System.Windows;
using NosCore.Launcher.Services;
using NosCore.Launcher.ViewModels;

namespace NosCore.Launcher;

public partial class App : Application
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var viewModel = new MainViewModel(new UserSettingsService(), new LauncherConfigService(_http));
        MainWindow = new MainWindow(viewModel);
        MainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _http.Dispose();
        base.OnExit(e);
    }
}
