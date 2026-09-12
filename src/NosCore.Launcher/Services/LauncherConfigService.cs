using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using NosCore.Launcher.Models;

namespace NosCore.Launcher.Services;

/// <summary>
/// Fetches the hosted <c>launcher.json</c>, caching the last good copy on disk.
/// The launcher has to open even when the config host is down, so the cache is
/// a real fallback rather than an optimisation: remote, then cache, then the
/// built-in default, in that order.
/// </summary>
public sealed class LauncherConfigService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _http;
    private readonly string _cachePath;

    public LauncherConfigService(HttpClient http)
    {
        _http = http;
        Directory.CreateDirectory(AppPaths.DataDirectory);
        _cachePath = Path.Combine(AppPaths.DataDirectory, "launcher-config.json");
    }

    public sealed record Result(LauncherConfig Config, ConfigSource Source, string? Error);

    /// <summary>
    /// The cache records which URL produced it. Point the launcher at a different
    /// server and the old entry must not answer for the new one — it carries
    /// <c>Auth</c> and <c>LoginServerIp</c>, so serving it under the wrong origin
    /// would silently send credentials to the previous server.
    /// </summary>
    private sealed record CacheEntry(string SourceUrl, LauncherConfig Config);

    public async Task<Result> LoadAsync(string configUrl, CancellationToken ct)
    {
        string? error = null;

        if (!string.IsNullOrWhiteSpace(configUrl) && !UrlPolicy.TryParse(configUrl, out _))
        {
            // Not a fetch failure — a setting that can never work. Say so rather
            // than falling through to a cache that answers for a different host.
            return new Result(new LauncherConfig(), ConfigSource.Default,
                "The launcher config URL must be an absolute http or https address.");
        }

        if (!string.IsNullOrWhiteSpace(configUrl))
        {
            try
            {
                var json = await _http.GetStringAsync(configUrl, ct);
                var config = Parse(json);
                await WriteCacheAsync(new CacheEntry(configUrl, config), ct);
                return new Result(config, ConfigSource.Remote, null);
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }
        }

        if (ReadCache() is { } cached && string.Equals(cached.SourceUrl, configUrl, StringComparison.OrdinalIgnoreCase))
        {
            return new Result(cached.Config, ConfigSource.Cache, error);
        }

        return new Result(new LauncherConfig(), ConfigSource.Default, error);
    }

    private CacheEntry? ReadCache()
    {
        if (!File.Exists(_cachePath))
        {
            return null;
        }
        try
        {
            using var stream = File.OpenRead(_cachePath);
            var entry = JsonSerializer.Deserialize<CacheEntry>(stream, Options);
            return entry is null ? null : entry with { Config = Normalise(entry.Config) };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Temp file then move. A direct write truncates the existing cache before
    /// the new bytes land, so cancellation or a crash mid-write would destroy
    /// the last-good copy — losing exactly the offline fallback it exists for.
    /// </summary>
    private async Task WriteCacheAsync(CacheEntry entry, CancellationToken ct)
    {
        var tmp = _cachePath + ".tmp";
        await using (var stream = File.Create(tmp))
        {
            await JsonSerializer.SerializeAsync(stream, entry, Options, ct);
        }
        File.Move(tmp, _cachePath, overwrite: true);
    }

    private static LauncherConfig Parse(string json) =>
        Normalise(JsonSerializer.Deserialize<LauncherConfig>(json, Options)
                  ?? throw new InvalidDataException("launcher.json deserialised to null."));

    /// <summary>
    /// An explicit <c>null</c> in the JSON overwrites a property initialiser, so
    /// <c>"Links": null</c> or <c>"Auth": null</c> would survive parsing and then
    /// throw on first use. Fill those back in before anything downstream sees them.
    /// </summary>
    private static LauncherConfig Normalise(LauncherConfig config) => new()
    {
        Title = string.IsNullOrWhiteSpace(config.Title) ? "NosCore" : config.Title,
        Description = config.Description ?? string.Empty,
        Links = config.Links ?? new Dictionary<string, string>(),
        News = config.News ?? new Dictionary<string, string>(),
        Ads = config.Ads ?? new Dictionary<string, AdConfig>(),
        StatusUrl = config.StatusUrl ?? string.Empty,
        Auth = config.Auth ?? new AuthConfig(),
        LoginServerIp = string.IsNullOrWhiteSpace(config.LoginServerIp) ? "127.0.0.1" : config.LoginServerIp,
        BackgroundUrl = config.BackgroundUrl ?? string.Empty,
    };
}

public enum ConfigSource
{
    Remote,
    Cache,
    Default,
}
