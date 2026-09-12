using System.Text.Json.Serialization;

namespace NosCore.Launcher.Models;

/// <summary>
/// Server-supplied branding and endpoints, fetched from a remote
/// <c>launcher.json</c>. The shape is deliberately compatible with the
/// original Electron launcher's config so an existing hosted file keeps
/// working: PascalCase keys, links/news as ordered maps of label to URL.
/// </summary>
public sealed class LauncherConfig
{
    public string Title { get; init; } = "NosCore";

    public string Description { get; init; } = string.Empty;

    /// <summary>Label to URL. Rendered as the left-panel nav.</summary>
    public Dictionary<string, string> Links { get; init; } = new();

    /// <summary>Headline to article URL. Empty URLs render as plain text.</summary>
    public Dictionary<string, string> News { get; init; } = new();

    public Dictionary<string, AdConfig> Ads { get; init; } = new();

    /// <summary>Polled for a status line; skipped when empty.</summary>
    public string StatusUrl { get; init; } = string.Empty;

    public AuthConfig Auth { get; init; } = new();

    /// <summary>
    /// Address patched into the client binary as its login server. The port is
    /// not patched — the client's built-in default is what NosCore listens on.
    /// </summary>
    public string LoginServerIp { get; init; } = "127.0.0.1";

    /// <summary>Optional background image URL; falls back to the built-in theme.</summary>
    public string BackgroundUrl { get; init; } = string.Empty;
}

public sealed class AuthConfig
{
    public string Url { get; init; } = "https://localhost";

    public int Port { get; init; } = 7001;

    /// <summary>Base address <see cref="ClientTools.NosCoreAuthClient"/> resolves its routes against.</summary>
    [JsonIgnore]
    public string BaseAddress => Port is 0 ? Url : $"{Url}:{Port}";
}

public sealed class AdConfig
{
    public string Img { get; init; } = string.Empty;

    public string Url { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;
}
