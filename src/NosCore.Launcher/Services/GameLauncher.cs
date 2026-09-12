using System.Diagnostics;
using System.IO;
using NosCore.ClientTools;
using NosCore.Launcher.Models;
using NosCore.Shared.Enumerations;

namespace NosCore.Launcher.Services;

/// <summary>
/// Turns a signed-in account plus a pristine client binary into a running game:
/// authenticate, patch a fresh copy of the exe, drop the gf_wrapper stub beside
/// it, and start it with the auth code in its environment.
/// </summary>
public sealed class GameLauncher
{
    private readonly Action<string> _log;

    public GameLauncher(Action<string> log) => _log = log;

    public async Task<Process> LaunchAsync(
        LauncherConfig config, UserSettings settings, string username, string password, string? mfa, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(settings.ClientExePath) || !File.Exists(settings.ClientExePath))
        {
            throw new FileNotFoundException(
                "Set the path to your NostaleClientX.exe in the launcher settings first.", settings.ClientExePath);
        }

        var region = Enum.TryParse<RegionType>(settings.Region, ignoreCase: true, out var parsed)
            ? parsed
            : RegionType.EN;

        using var auth = new NosCoreAuthClient(config.Auth.BaseAddress, _log);
        var result = await auth.AuthenticateAsync(
            username, password, region.ToString(), settings.Locale, mfa, ct);
        _log($"Signed in as {username}.");

        var patchedExe = PreparePatchedClient(settings, config.LoginServerIp);

        var startInfo = new ProcessStartInfo
        {
            FileName = patchedExe,
            // The client parses the second token as the numeric RegionType
            // ordinal, not the language code.
            Arguments = $"gf {(int)region}",
            WorkingDirectory = Path.GetDirectoryName(patchedExe)!,
            UseShellExecute = false,
        };
        startInfo.EnvironmentVariables["_NC_AUTH_CODE"] = result.AuthCode;

        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("The client failed to start.");
        _log($"Client started (pid {process.Id}).");
        return process;
    }

    /// <summary>
    /// Patch a fresh copy from the pristine exe on every launch. Patching in
    /// place would compound edits across runs — the address slot is located by
    /// shape, so a second pass over an already-patched binary is not a no-op.
    /// </summary>
    private string PreparePatchedClient(UserSettings settings, string loginServerIp)
    {
        var bytes = File.ReadAllBytes(settings.ClientExePath);
        var directory = Path.GetDirectoryName(settings.ClientExePath)!;

        Apply(ClientPatcher.PatchServerAddress(bytes, loginServerIp), required: true);
        Apply(ClientPatcher.PatchAllowNoArg(bytes), required: false);
        Apply(ClientPatcher.PatchDefaultToEntwell(bytes), required: false);

        var stubPatch = ClientPatcher.PatchImportName(bytes);
        Apply(stubPatch, required: true);

        var outputPath = Path.Combine(directory, settings.PatchedExeName);
        if (string.Equals(Path.GetFullPath(outputPath), Path.GetFullPath(settings.ClientExePath),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The patched client name matches the source client — pick a different name so the original stays pristine.");
        }

        File.WriteAllBytes(outputPath, bytes);
        _log($"Wrote {outputPath}");
        _log($"Wrote {GfStub.DeployTo(directory)}");
        return outputPath;
    }

    private void Apply(ClientPatcher.PatchResult result, bool required)
    {
        _log(result.Log);
        if (!result.Success && required)
        {
            throw new InvalidOperationException(result.Log);
        }
    }
}
