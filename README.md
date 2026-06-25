# Copilot Key Remapper

A Windows 11 app that lets you choose what your keyboard's dedicated **Copilot key**
does, using the official Microsoft "Copilot hardware key provider" path — so it
appears in **Settings → Bluetooth & devices → Keyboard → Customize Copilot key**.

**No background service. No wasted resources.** Nothing runs while you wait for the
key — there's no always-on service, no tray icon, and no startup task sitting in
memory or burning CPU/battery. When you press the key, a tiny handler runs your
action in a fraction of a second and exits. The settings UI only runs while you have
it open. Idle footprint: zero.

## Architecture (two processes, by design)

The keypress path is deliberately **thin and non-resident** — nothing runs in the
background just to wait for the key:

- **`CopilotKeyRemapperKey.exe`** — a tiny self-contained (ReadyToRun, trimmed, single-file)
  native-ish exe. It is the registered Copilot-key provider. On a key press Windows
  launches it; it reads `settings.json`, performs the action (synthesized keystroke
  via `SendInput`, or `Process.Start` for an app/URI), and **exits**. No window, no
  resident process, ~ms startup. (One csproj flag from full Native AOT if the VC++
  build tools are present.)
- **`CopilotKeyRemapper.exe`** — the WinUI 3 settings UI. Launched on demand from the Start
  tile to configure the actions. Never resident, never in the keypress path.

Both ship in one MSIX. The handler is `Application Id="App"` (so the user's key
assignment survives updates); the settings UI is `Application Id="Settings"` (the
visible Start tile).

## How the key maps

When the key is pressed, Windows launches the handler, which runs your configured
action and exits. (Holding the key makes Windows auto-repeat the launch, so the
handler debounces — one press runs the action exactly once.) The action can be:

1. **A custom shortcut** — recorded chord (incl. system chords like Alt+Tab), replayed via `SendInput`.
2. **An app, file, or link** — picked from a searchable list of installed apps, or any path/URI (`https://…`, `ms-settings:display`).
3. **A Windows function** — chosen from a curated, searchable catalog by name/group.
4. **Nothing**.

## Building

**Settings app (dev):**
```powershell
dotnet build CopilotKeyRemapper/CopilotKeyRemapper.csproj -c Debug
```

**MSIX package (assignable / Store):**
```powershell
# Needs the Windows SDK packaging tools (makeappx/makepri/signtool). The build
# script finds them in an installed SDK or the Microsoft.Windows.SDK.BuildTools
# NuGet package (acquiring it if needed).
powershell -File CopilotKeyRemapperMSIX/generate-msix-images.ps1   # one-time: visual assets
powershell -File CopilotKeyRemapperMSIX/build-msix.ps1             # sideload (dev-signed)
powershell -File CopilotKeyRemapperMSIX/build-msix.ps1 -NoSign     # Store upload
```

The script publishes both exes (WinUI self-contained + the handler) into the package.
Before Store submission, set `<Identity>` `Name`/`Publisher` in
`CopilotKeyRemapperMSIX/Package.appxmanifest` to your Partner Center values. Bump
`<Version>` in `Directory.Build.props` for each update (MSIX blocks same-version
re-installs).

## Using it

1. Install the signed MSIX (or from the Store).
2. **Settings → Bluetooth & devices → Keyboard → Customize Copilot key → Custom →
   Copilot Key Remapper.**
3. Open **Copilot Key Remapper** from the Start menu to set the action.

## Requirements

- Windows 11 (build 22621+)
- .NET 10 SDK; Windows SDK packaging tools to build the MSIX
- Full Native AOT for the handler additionally needs the VC++ build tools (optional)

## License

Licensed under the [PolyForm Noncommercial License 1.0.0](LICENSE.md): free for any
**personal and other noncommercial use**, including modifying and redistributing it.
**Commercial use is not permitted.** Copyright © 2026 Ryan Ewen.
