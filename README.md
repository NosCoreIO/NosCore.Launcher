# NosCore.Launcher #

<p align="center">
  <img width="250px" src="https://github.com/NosCoreIO/NosCore.Packets/blob/15.0.1/icon.png?raw=true"/>
</p>

Early-stage (`0.0.1`) launcher for [NosCore](https://github.com/NosCoreIO/NosCore) servers. WPF on .NET 10. Signs in against a NosCore server, prepares a NosTale client to reach it, and starts the game.

Replaces [NosCoreLegend/Launcher](https://github.com/NosCoreLegend/Launcher) (Electron/React, archived).

## Warning! ##
We are not responsible of any damages caused by bad usage of our source. Please before asking questions or installing this source read this readme and also do a research, google is your friend. If you mess up when installing our source because you didnt follow it, we will laugh at you. A lot.

## Legal ##
This is an independent and unofficial launcher for educational use ONLY. It is in no way affiliated with, authorized, maintained, sponsored or endorsed by Gameforge or any of its affiliates or subsidiaries. Using it might be against the TOS of any NosTale server you connect to. Use at your own risk.

## What it does ##

1. **Signs in.** Trades your credentials for a JWT and then a short-lived auth-code GUID, against `/api/v1/auth/thin/sessions` and `/api/v1/auth/thin/codes` on the configured server.
2. **Prepares the client.** Patches a *copy* of your `NostaleClientX.exe`: rewrites the embedded login-server address, neutralises the argc gate so the exe survives a no-arg launch, defaults unknown args into the Entwell standalone body, and repoints the `gf_wrapper.dll` import at `noscore_gf.dll`.
3. **Drops the stub.** Writes `noscore_gf.dll` — a NativeAOT x86 replacement for Gameforge's wrapper — next to the patched exe.
4. **Starts the game** with `gf <region>` and the auth code in `_NC_AUTH_CODE`, which the stub hands back when the client asks for its session ticket.

Steps 1–3 all come from [NosCore.ClientTools](https://github.com/NosCoreIO/NosCore.DeveloperTools), shared with NosCore.DeveloperTools.

### No Gameforge launcher, no pipe ###

The Electron launcher this replaces impersonated the real Gameforge launcher: it hosted a JSON-RPC server on `\\.\pipe\GameforgeClientJSONRPC` and answered `ClientLibrary.queryAuthorizationCode` and friends, so the client's genuine `gameforge_client_api.dll` chain would be satisfied. That meant you had to supply Gameforge's own DLL and keep a registry `InstallationId` around.

We replace the wrapper DLL instead. There is no pipe, no RPC, and no Gameforge binary to find — the auth code arrives through an environment variable and the stub returns it directly.

## Requirements ##

- Windows, .NET 10 desktop runtime (or use the self-contained Release build)
- A NosCore server with the WebApi reachable
- Your own `NostaleClientX.exe` — this repo ships no game files

## Settings ##

`Settings` in the title bar. The client path, patched filename, region, locale and config URL are stored in `%LocalAppData%\NosCore.Launcher\settings.json`.

Your password goes to **Windows Credential Manager** under `NosCore.Launcher:<username>`, not to that file — visible and revocable in Control Panel → Credential Manager. (The Electron launcher kept passwords as cleartext JSON.)

Your source client is never modified. Each launch writes a freshly patched copy under the filename you choose, because the address slot is located by binary shape — a second pass over an already-patched binary is not a no-op.

## launcher.json ##

Branding, endpoints and news come from a hosted JSON file whose URL you set in settings. Blank means the launcher opens on built-in defaults. The last successful fetch is cached, so the launcher still opens when the host is down. The shape is compatible with the old Electron launcher's config:

```json
{
  "Title": "NosCore",
  "Description": "A NosTale server running on NosCore.",
  "LoginServerIp": "127.0.0.1",
  "BackgroundUrl": "",
  "Auth": { "Url": "https://127.0.0.1", "Port": 7001 },
  "Links": { "Website": "https://github.com/NosCoreIO/NosCore", "Discord": "" },
  "News": { "Server is up": "https://example.invalid/news/1" },
  "Ads": {
    "Launch week": { "Img": "https://example.invalid/ad.png", "Url": "", "Description": "" }
  }
}
```

`Links` and `News` are label-to-URL maps; a news entry with an empty URL renders as plain text. `Ads` rotate every 7 seconds and are hidden entirely when the map is empty. `LoginServerIp` is the address patched into the client — the **port is not patched**, so the client's built-in default is what NosCore has to listen on.

## Build ##

Requires the .NET 10 SDK.

```
dotnet build
```

`Release` publishes a single self-contained `win-x64` exe. Unlike NosCore.DeveloperTools this is AnyCPU: the launcher only spawns the client, it never shares its address space, so it carries none of the injector's 32-bit constraint. The stub DLL is x86, but it travels as prebuilt bytes inside `NosCore.ClientTools`.
