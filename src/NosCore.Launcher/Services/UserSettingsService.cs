using System.IO;
using System.Text.Json;
using NosCore.Launcher.Models;

namespace NosCore.Launcher.Services;

public sealed class UserSettingsService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _path;

    public UserSettingsService()
    {
        Directory.CreateDirectory(AppPaths.DataDirectory);
        _path = Path.Combine(AppPaths.DataDirectory, "settings.json");
    }

    public UserSettings Load()
    {
        if (!File.Exists(_path)) return new UserSettings();
        try
        {
            using var stream = File.OpenRead(_path);
            return JsonSerializer.Deserialize<UserSettings>(stream, Options) ?? new UserSettings();
        }
        catch
        {
            return new UserSettings();
        }
    }

    public void Save(UserSettings settings)
    {
        var tmp = _path + ".tmp";
        using (var stream = File.Create(tmp))
        {
            JsonSerializer.Serialize(stream, settings, Options);
        }
        File.Move(tmp, _path, overwrite: true);
    }
}

public static class AppPaths
{
    public static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NosCore.Launcher");
}
