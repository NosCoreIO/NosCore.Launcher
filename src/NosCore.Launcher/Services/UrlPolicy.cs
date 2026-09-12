namespace NosCore.Launcher.Services;

/// <summary>
/// What a URL out of the hosted <c>launcher.json</c> is allowed to be.
///
/// Everything in that file is attacker-controlled from the launcher's point of
/// view — whoever hosts it, or anyone who can MITM a plain-http fetch of it,
/// picks these strings. Three sinks consume them, and each is dangerous with a
/// scheme we did not intend: <c>Process.Start(UseShellExecute = true)</c> will
/// hand any registered protocol handler its argument, and
/// <see cref="System.Windows.Controls.Image"/> will happily resolve a
/// <c>file:</c> or UNC source — which on a remote share means an outbound SMB
/// handshake carrying the current user's credentials.
///
/// So the rule is one rule, applied at the boundary: absolute, and http or
/// https. Plain http stays allowed because self-hosted servers and
/// <c>localhost</c> testing routinely have no certificate.
/// </summary>
public static class UrlPolicy
{
    public static bool IsWebUrl(string? value) => TryParse(value, out _);

    public static bool TryParse(string? value, out Uri? uri)
    {
        uri = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }
        if (!Uri.TryCreate(value, UriKind.Absolute, out var parsed))
        {
            return false;
        }
        if (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }
        uri = parsed;
        return true;
    }

    /// <summary>The value when it passes, otherwise null — for binding straight to a view.</summary>
    public static string? Sanitise(string? value) => TryParse(value, out var uri) ? uri!.ToString() : null;
}
