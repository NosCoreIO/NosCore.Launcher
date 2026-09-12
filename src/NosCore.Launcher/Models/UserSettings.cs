namespace NosCore.Launcher.Models;

/// <summary>Machine-local choices. Credentials live in Windows Credential Manager, not here.</summary>
public sealed class UserSettings
{
    /// <summary>Pristine <c>NostaleClientX.exe</c> the launcher patches from.</summary>
    public string ClientExePath { get; set; } = string.Empty;

    /// <summary>Filename written next to the source client. Patched afresh on every launch.</summary>
    public string PatchedExeName { get; set; } = "NosCore.exe";

    public string Region { get; set; } = "EN";

    public string Locale { get; set; } = "en_US";

    public string LastUsername { get; set; } = string.Empty;

    public bool RememberMe { get; set; } = true;

    /// <summary>Overrides the hosted config URL. Empty means use the built-in default.</summary>
    public string ConfigUrl { get; set; } = string.Empty;
}
