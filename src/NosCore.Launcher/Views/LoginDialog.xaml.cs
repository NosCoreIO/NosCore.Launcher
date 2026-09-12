using System.Windows;
using NosCore.Launcher.ViewModels;

namespace NosCore.Launcher.Views;

public partial class LoginDialog : Window
{
    public LoginDialog(string username, bool rememberMe)
    {
        InitializeComponent();
        UsernameBox.Text = username;
        RememberBox.IsChecked = rememberMe;

        SourceInitialized += (_, _) => DarkTitleBar.Apply(this);
        Loaded += (_, _) =>
        {
            if (string.IsNullOrEmpty(UsernameBox.Text))
            {
                UsernameBox.Focus();
            }
            else
            {
                PasswordBox.Focus();
            }
        };
    }

    public Credentials? Result { get; private set; }

    private void OnSubmit(object sender, RoutedEventArgs e)
    {
        var username = UsernameBox.Text.Trim();
        if (string.IsNullOrEmpty(username))
        {
            Fail("Enter your login or email.");
            return;
        }
        if (PasswordBox.Password.Length == 0)
        {
            Fail("Enter your password.");
            return;
        }

        var mfa = MfaBox.Text.Trim();
        Result = new Credentials(
            username,
            PasswordBox.Password,
            string.IsNullOrEmpty(mfa) ? null : mfa,
            RememberBox.IsChecked is true);
        DialogResult = true;
    }

    private void Fail(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
