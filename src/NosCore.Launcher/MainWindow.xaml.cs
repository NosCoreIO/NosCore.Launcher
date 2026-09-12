using System.Windows;
using System.Windows.Input;
using NosCore.Launcher.Models;
using NosCore.Launcher.ViewModels;
using NosCore.Launcher.Views;

namespace NosCore.Launcher;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;

        // The view model stays free of Window references; dialogs are the view's job.
        viewModel.RequestCredentials = ShowLogin;
        viewModel.RequestSettings = ShowSettings;

        FitToWorkArea();
        Loaded += async (_, _) => await viewModel.InitialiseAsync(CancellationToken.None);
    }

    /// <summary>
    /// The design size is 960x680, and the window is deliberately not resizable.
    /// On a smaller work area — a 1366x768 panel at 125% scaling leaves roughly
    /// 1093x574 DIPs — that would push the status strip and PLAY button off
    /// screen with no way to reach them, so shrink to fit instead.
    /// </summary>
    private void FitToWorkArea()
    {
        var work = SystemParameters.WorkArea;
        Width = Math.Min(Width, work.Width);
        Height = Math.Min(Height, work.Height);
    }

    private Credentials? ShowLogin()
    {
        var dialog = new LoginDialog(_viewModel.Username, _viewModel.Settings.RememberMe) { Owner = this };
        return dialog.ShowDialog() is true ? dialog.Result : null;
    }

    private bool ShowSettings(UserSettings settings)
    {
        var dialog = new SettingsDialog(settings) { Owner = this };
        return dialog.ShowDialog() is true;
    }

    private void OnChromeDrag(object sender, MouseButtonEventArgs e) => DragMove();

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
