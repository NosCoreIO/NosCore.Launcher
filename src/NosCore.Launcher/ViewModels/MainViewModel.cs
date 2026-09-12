using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NosCore.Launcher.Models;
using NosCore.Launcher.Services;

namespace NosCore.Launcher.ViewModels;

public sealed record LinkItem(string Label, string Url);

public sealed record NewsItem(string Headline, string Url)
{
    public bool HasUrl => !string.IsNullOrWhiteSpace(Url);
}

public sealed record AdItem(string ImageUrl, string Url, string Description);

public sealed record Credentials(string Username, string Password, string? Mfa, bool Remember);

public sealed partial class MainViewModel : ObservableObject
{
    private readonly UserSettingsService _settingsService;
    private readonly LauncherConfigService _configService;
    private readonly DispatcherTimer _adTimer;

    private LauncherConfig _config = new();
    private string? _pendingPassword;
    private string? _pendingMfa;

    /// <summary>Set by the view to show the login dialog. A null result means cancelled.</summary>
    public Func<Credentials?>? RequestCredentials { get; set; }

    /// <summary>Set by the view to show the settings dialog. False means cancelled.</summary>
    public Func<UserSettings, bool>? RequestSettings { get; set; }

    public MainViewModel(UserSettingsService settingsService, LauncherConfigService configService)
    {
        _settingsService = settingsService;
        _configService = configService;
        Settings = settingsService.Load();
        _username = Settings.LastUsername;

        _adTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(7) };
        _adTimer.Tick += (_, _) => NextAd();
    }

    public UserSettings Settings { get; }

    public ObservableCollection<LinkItem> Links { get; } = new();

    public ObservableCollection<NewsItem> News { get; } = new();

    public ObservableCollection<AdItem> Ads { get; } = new();

    [ObservableProperty]
    private string _title = "NosCore";

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string? _backgroundUrl;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AccountLabel))]
    private string _username;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AccountLabel))]
    private bool _isSignedIn;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PlayCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private AdItem? _currentAd;

    public string AccountLabel => IsSignedIn ? Username : "Sign in";

    public async Task InitialiseAsync(CancellationToken ct)
    {
        var result = await _configService.LoadAsync(Settings.ConfigUrl, ct);
        _config = result.Config;

        Title = _config.Title;
        Description = _config.Description;
        BackgroundUrl = string.IsNullOrWhiteSpace(_config.BackgroundUrl) ? null : _config.BackgroundUrl;

        foreach (var (label, url) in _config.Links.Where(l => !string.IsNullOrWhiteSpace(l.Value)))
        {
            Links.Add(new LinkItem(label, url));
        }

        foreach (var (headline, url) in _config.News)
        {
            News.Add(new NewsItem(headline, url));
        }

        foreach (var ad in _config.Ads.Values.Where(a => !string.IsNullOrWhiteSpace(a.Img)))
        {
            Ads.Add(new AdItem(ad.Img, ad.Url, ad.Description));
        }
        CurrentAd = Ads.FirstOrDefault();
        if (Ads.Count > 1)
        {
            _adTimer.Start();
        }

        Status = result.Source switch
        {
            ConfigSource.Remote => string.Empty,
            ConfigSource.Cache => "Offline — showing the last server info we saw.",
            _ when string.IsNullOrWhiteSpace(Settings.ConfigUrl) =>
                "No launcher config URL set — add one in settings for server info and news.",
            _ => $"Could not load the launcher config: {result.Error}",
        };

        // A remembered account is shown as signed in, but the credential is only
        // spent when Play is pressed: an auth code is short-lived, so fetching
        // one at startup would usually mean fetching it twice.
        if (Settings.RememberMe
            && !string.IsNullOrWhiteSpace(Settings.LastUsername)
            && CredentialStore.Load(Settings.LastUsername) is not null)
        {
            IsSignedIn = true;
        }
    }

    private void NextAd()
    {
        if (Ads.Count == 0)
        {
            return;
        }
        var index = CurrentAd is null ? -1 : Ads.IndexOf(CurrentAd);
        CurrentAd = Ads[(index + 1) % Ads.Count];
    }

    [RelayCommand]
    private void SelectAd(AdItem? ad)
    {
        if (ad is null)
        {
            return;
        }
        CurrentAd = ad;
        // Restart the dwell so a deliberate pick is not yanked away immediately.
        _adTimer.Stop();
        if (Ads.Count > 1)
        {
            _adTimer.Start();
        }
    }

    [RelayCommand]
    private void OpenUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Status = $"Could not open {url}: {ex.Message}";
        }
    }

    [RelayCommand]
    private void Account()
    {
        if (IsSignedIn)
        {
            CredentialStore.Delete(Username);
            _pendingPassword = null;
            _pendingMfa = null;
            IsSignedIn = false;
            Status = "Signed out.";
            return;
        }
        SignIn();
    }

    [RelayCommand]
    private void EditSettings()
    {
        if (RequestSettings?.Invoke(Settings) is not true)
        {
            return;
        }
        _settingsService.Save(Settings);
        Status = "Settings saved.";
    }

    [RelayCommand(CanExecute = nameof(CanPlay))]
    private async Task PlayAsync()
    {
        IsBusy = true;
        try
        {
            if (!IsSignedIn && !SignIn())
            {
                return;
            }

            var password = _pendingPassword ?? CredentialStore.Load(Username);
            if (string.IsNullOrEmpty(password))
            {
                Status = "The saved password is gone — sign in again.";
                IsSignedIn = false;
                return;
            }

            var launcher = new GameLauncher(message => Status = message);
            await launcher.LaunchAsync(_config, Settings, Username, password, _pendingMfa, CancellationToken.None);
            // An MFA code is single-use; a second launch has to ask again.
            _pendingMfa = null;
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanPlay() => !IsBusy;

    private bool SignIn()
    {
        if (RequestCredentials?.Invoke() is not { } credentials)
        {
            return false;
        }

        Username = credentials.Username;
        Settings.LastUsername = credentials.Username;
        Settings.RememberMe = credentials.Remember;
        if (credentials.Remember)
        {
            CredentialStore.Save(credentials.Username, credentials.Password);
        }
        else
        {
            CredentialStore.Delete(credentials.Username);
        }
        _settingsService.Save(Settings);

        _pendingPassword = credentials.Password;
        _pendingMfa = credentials.Mfa;
        IsSignedIn = true;
        Status = string.Empty;
        return true;
    }
}
