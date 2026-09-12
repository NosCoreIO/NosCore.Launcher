using System.IO;
using System.Net.Http;
using System.Text.Json;
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
    };

    private readonly HttpClient _http;
    private readonly string _cachePath;

    public LauncherConfigService(HttpClient http)
    {
        _http = http;
        Directory.CreateDirectory(AppPaths.DataDirectory);
        _cachePath = Path.Combine(AppPaths.DataDirectory, "launcher.json");
    }

    public sealed record Result(LauncherConfig Config, ConfigSource Source, string? Error);

    public async Task<Result> LoadAsync(string configUrl, CancellationToken ct)
    {
        string? error = null;

        if (!string.IsNullOrWhiteSpace(configUrl))
        {
            try
            {
                var json = await _http.GetStringAsync(configUrl, ct);
                var config = Parse(json);
                await File.WriteAllTextAsync(_cachePath, json, ct);
                return new Result(config, ConfigSource.Remote, null);
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }
        }

        if (File.Exists(_cachePath))
        {
            try
            {
                return new Result(Parse(await File.ReadAllTextAsync(_cachePath, ct)), ConfigSource.Cache, error);
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }
        }

        return new Result(new LauncherConfig(), ConfigSource.Default, error);
    }

    private static LauncherConfig Parse(string json) =>
        JsonSerializer.Deserialize<LauncherConfig>(json, Options)
        ?? throw new InvalidDataException("launcher.json deserialised to null.");
}

public enum ConfigSource
{
    Remote,
    Cache,
    Default,
}
