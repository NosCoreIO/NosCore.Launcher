using System.IO;
using System.Windows;
using Microsoft.Win32;
using NosCore.Launcher.Models;
using NosCore.Shared.Enumerations;

namespace NosCore.Launcher.Views;

public partial class SettingsDialog : Window
{
    private readonly UserSettings _settings;

    public SettingsDialog(UserSettings settings)
    {
        _settings = settings;
        InitializeComponent();

        ClientPathBox.Text = settings.ClientExePath;
        PatchedNameBox.Text = settings.PatchedExeName;
        LocaleBox.Text = settings.Locale;
        ConfigUrlBox.Text = settings.ConfigUrl;

        SourceInitialized += (_, _) => DarkTitleBar.Apply(this);

        foreach (var region in Enum.GetValues<RegionType>())
        {
            RegionBox.Items.Add(region.ToString());
        }
        RegionBox.SelectedItem = Enum.TryParse<RegionType>(settings.Region, ignoreCase: true, out var parsed)
            ? parsed.ToString()
            : RegionType.EN.ToString();
    }

    private void OnBrowseClient(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select your NostaleClientX.exe",
            Filter = "NosTale client (*.exe)|*.exe",
            CheckFileExists = true,
        };
        if (!string.IsNullOrWhiteSpace(ClientPathBox.Text))
        {
            var directory = Path.GetDirectoryName(ClientPathBox.Text);
            if (Directory.Exists(directory))
            {
                dialog.InitialDirectory = directory;
            }
        }
        if (dialog.ShowDialog(this) is true)
        {
            ClientPathBox.Text = dialog.FileName;
        }
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        var clientPath = ClientPathBox.Text.Trim();
        if (clientPath.Length > 0 && !File.Exists(clientPath))
        {
            Fail("That client path does not exist.");
            return;
        }

        var patchedName = PatchedNameBox.Text.Trim();
        if (patchedName.Length == 0)
        {
            Fail("Give the patched client a filename.");
            return;
        }
        if (patchedName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            Fail("The patched client filename contains characters a filename cannot hold.");
            return;
        }
        // Patching over the source would compound edits run after run: the
        // address slot is found by shape, so a second pass is not a no-op.
        if (clientPath.Length > 0
            && string.Equals(patchedName, Path.GetFileName(clientPath), StringComparison.OrdinalIgnoreCase))
        {
            Fail("The patched filename must differ from the source client, so the original stays pristine.");
            return;
        }

        var configUrl = ConfigUrlBox.Text.Trim();
        if (configUrl.Length > 0 && !IsHttpUrl(configUrl))
        {
            Fail("The launcher config URL must be an http or https address.");
            return;
        }

        _settings.ClientExePath = clientPath;
        _settings.PatchedExeName = patchedName;
        _settings.Region = RegionBox.SelectedItem as string ?? RegionType.EN.ToString();
        _settings.Locale = LocaleBox.Text.Trim();
        _settings.ConfigUrl = configUrl;
        DialogResult = true;
    }

    private static bool IsHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private void Fail(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
