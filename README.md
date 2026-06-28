<p align="center">
  <img src="RepilotMSIX/Images/Square150x150Logo.png" alt="Repilot" width="120">
</p>

<h1 align="center">Repilot</h1>

<p align="center">
  Remap the Windows 11 Copilot key to do what <em>you</em> want — a keyboard shortcut, an app or link, or a built-in Windows function.
</p>

<p align="center">
  <a href="https://apps.microsoft.com/detail/9pb5fj08pnvj">
    <img src="https://get.microsoft.com/images/en-us%20dark.svg" alt="Download Repilot from the Microsoft Store" height="56">
  </a>
</p>

Repilot lets you choose what your keyboard's dedicated **Copilot key** (or **Windows logo key + C**) does, using the official Microsoft "Copilot hardware key provider" path — so it appears in **Settings → Bluetooth & devices → Keyboard → Customize Copilot key**.

**No background service. No wasted resources.** Nothing runs while you wait for the
key — there's no always-on service, no tray icon, and no startup task sitting in
memory or burning CPU/battery. When you press the key, a tiny handler runs your
action in a fraction of a second and exits. The settings UI only runs while you have
it open. Idle footprint: zero.

## What the key can do

1. **A custom shortcut** — record any chord (including system chords like Alt+Tab), replayed via `SendInput`.
2. **An app, file, or link** — pick from a searchable list of installed apps, or enter any path/URI (`https://…`, `ms-settings:display`).
3. **A Windows function** — choose from a curated, searchable catalog by name/group (Task View, snap, lock, Snip & Sketch, Magnifier, and more).
4. **Nothing** — quietly disable the key.

## Installing

- **Microsoft Store (recommended):** [Get Repilot](https://apps.microsoft.com/detail/9pb5fj08pnvj) — installs and stays up to date automatically.
- Then go to **Settings → Bluetooth & devices → Keyboard → Customize Copilot key → Custom → Repilot**.
- Open **Repilot** from the Start menu to set the action. You can also check for updates from the About page.

> **Two Start entries are expected:** **Repilot** (the settings app) and **Repilot Key** (the provider that runs your assigned action). Windows only offers *visible, launchable* apps in the Customize Copilot key picker, so the key handler must have its own Start entry — it's a requirement of the platform, not a stray tile.

## Architecture (two processes, by design)

The keypress path is deliberately **thin and non-resident** — nothing runs in the
background just to wait for the key:

- **`RepilotKey.exe`** — a tiny self-contained (ReadyToRun, trimmed, single-file)
  exe. It is the registered Copilot-key provider (Start tile **"Repilot Key"**). On a
  key press Windows launches it; it reads `settings.json`, performs the action
  (synthesized keystroke via `SendInput`, or `Process.Start` for an app/URI), and
  **exits**. No window, no resident process, ~ms startup. (One csproj flag from full
  Native AOT if the VC++ build tools are present.)
- **`Repilot.exe`** — the WinUI 3 settings UI (Start tile **"Repilot"**). Launched on
  demand to configure the action and check for updates. Never resident, never in the
  keypress path.

Both ship in one MSIX with two `<Application>` entries: `Id="App"` is the handler
(so the key assignment AUMID `…!App` survives updates) and `Id="Settings"` is the
WinUI app. The handler **must** be listed in the app list (no `AppListEntry="none"`)
— a hidden provider registers in the catalog but Settings rejects it as "no app meets
the hardware key criteria."

## Building

**Settings app (dev):**
```powershell
dotnet build Repilot/Repilot.csproj -c Debug
```

**MSIX package (assignable / Store):**
```powershell
# Needs the Windows SDK packaging tools (makeappx/makepri/signtool). The build
# script finds them in an installed SDK or the Microsoft.Windows.SDK.BuildTools
# NuGet package (acquiring it if needed).
powershell -File RepilotMSIX/generate-msix-images.ps1   # regenerate the icon/tile art
powershell -File RepilotMSIX/build-msix.ps1             # sideload (dev-signed)
powershell -File RepilotMSIX/build-msix.ps1 -NoSign     # Store upload
```

The script publishes both exes (WinUI self-contained + the handler) into the package.
Before Store submission, set `<Identity>` `Name`/`Publisher` in
`RepilotMSIX/Package.appxmanifest` to your Partner Center values. Bump `<Version>` in
`Directory.Build.props` for each update (MSIX blocks same-version re-installs).

## Requirements

- Windows 11 (build 22621+)
- .NET 10 SDK; Windows SDK packaging tools to build the MSIX
- Full Native AOT for the handler additionally needs the VC++ build tools (optional)

## License

Licensed under the [PolyForm Noncommercial License 1.0.0](LICENSE.md): free for any
**personal and other noncommercial use**, including modifying and redistributing it.
**Commercial use is not permitted.** Copyright © 2026 Ryan Ewen.
