# Repilot — Claude Code Instructions

## Project overview

Repilot remaps the Windows 11 Copilot key via the **official Microsoft Copilot
hardware-key provider** model. It must ship as a signed **MSIX** to be assignable in
Settings. Reuses LittleLauncher patterns (WinUI 3 settings window, `SettingsManager`/
`UserSettings`, MSIX build script, and a tiny standalone handler exe like
LittleLauncher's Native-AOT companion).

## Architecture — TWO processes (keep this split)

The keypress path must stay thin and non-resident.

- **`RepilotKey.exe`** (project `RepilotKey/`) — the key handler. Self-contained,
  ReadyToRun, trimmed, single-file (flip `<PublishAot>` if VC++ build tools exist).
  Windows launches it on the key; `Program.cs` detects the gesture, reads
  `settings.json`, calls `ActionExecutor.RunSync`, and exits. No window, nothing resident.
  Shares model/catalog/executor with the settings app via **linked source files**
  (`<Compile Include="..\Repilot\...">`), so those files must stay WinUI-free and
  AOT/trim-safe (no NLog, no reflection-heavy code).
- **`Repilot.exe`** (project `Repilot/`) — WinUI 3 settings UI only. Launched
  on demand; `SettingsWindow` is the main window; not resident; not in the keypress path.

Both ship in one MSIX (`RepilotMSIX/`). Manifest has two `<Application>`s:
`Id="App"` = the handler (provider + protocol extension; Start tile "Repilot Key") —
**named "App" so the user's key assignment AUMID (`PFN!App`) survives updates**;
`Id="Settings"` = the WinUI app (Start tile "Repilot").

**CRITICAL — the provider (`Id="App"`) MUST be listed in the app list; do NOT set
`AppListEntry="none"` on it.** The "Customize Copilot key" picker only offers visible,
launchable apps. A hidden provider still registers in the AppExtension catalog (so a
catalog probe finds it) but Settings rejects it with "your PC doesn't have an app that
meets the hardware key criteria." Consequence: BOTH apps show a Start tile ("Repilot"
= settings, "Repilot Key" = runs the action). That second tile is required for
eligibility — it is not a bug to be cleaned up. (This shipped broken in 1.0.13; fixed
in 1.0.14.)

## How the key maps

- Windows launches the handler (`Id="App"`) on a key press; it runs the single
  configured `Action`. Holding the key auto-repeats the launch, so the handler
  debounces (one press = one run) via a `repeat-state` timestamp + a named mutex.
- A press-and-hold protocol activation (`repilot-key://?state=Up`) is a
  no-op; the OS doesn't reliably deliver a distinct hold gesture, so hold was dropped.
- `ActionExecutor` runs the `Action` from settings.json: `SendInput` for key combos
  (it reuses a physically-held Win when triggered via Win+C), `Process.Start(UseShellExecute)`
  for app/URI/`ms-settings:`, and `explorer.exe` for `shell:AppsFolder\{AUMID}` apps.

## Shared types (WinUI-free, in `Repilot/` but linked into the handler)

| File | Role |
|---|---|
| `Models/KeyCombo.cs` | Modifiers + VK; display string. |
| `Models/CopilotActionData.cs` | Plain POCO action (Type, Combo, LaunchPath, WindowsFunctionId). |
| `Models/WindowsFunction.cs` | Catalog entry. |
| `Services/WindowsFunctionCatalog.cs` | Curated grouped catalog. |
| `Services/ActionExecutor.cs` | `RunSync` (handler) / `Run` (UI test button); self-contained SendInput. |

WinUI-only: `Services/ActionSummary.cs`, `Services/CopilotKeyProvider.cs`
(assignment-status reader; `HandlerAppId = "App"`), `ViewModels/UserSettings.cs`,
`Classes/NativeMethods.cs` (settings-window P/Invoke only), pages, `SettingsWindow`.

## Conventions / gotchas

- Settings at `%AppData%\Repilot\settings.json` (auto-redirected to package data
  when packaged — both exes resolve the same path).
- Linked shared files must be AOT/trim-safe — **no NLog** there (handler errors go to
  `ActionExecutor.ErrorSink` in the UI, or the handler's own `key-handler.log`).
- `global::` does not parse inside interpolated strings — assign to a local first.
- Fully-qualify `Microsoft.UI.Xaml.Visibility`/`FocusState` in page code-behind.
- **PowerShell build scripts must be ASCII** (Windows PowerShell 5.1 reads BOM-less
  `.ps1` as ANSI; a UTF-8 em-dash inside a string becomes a curly quote and breaks parsing).
- MSIX blocks reinstalling the same version with different content — bump
  `<Version>` in `Directory.Build.props` per build.

## Update checking (`Services/UpdateService.cs`)

Packaged copies check the **Store**; unpackaged ones check GitHub Releases. Same shape as the
sibling apps (Little Launcher, Drive for Immich) — keep the three in step.

- **Presence of the app's own package family in `GetAppAndOptionalStorePackageUpdatesAsync` is
  the update signal.** `StorePackageUpdate.Package` describes the package *as installed*, so
  `Id.Version` reports the version already on the machine, never the one being offered — measured
  on Little Launcher with a live update pending (installed 1.27.1.0, published 1.28.0.0: one
  entry, reporting 1.27.1.0). **Never require the listed version to be strictly newer**; it can
  never match, and the result is a permanent, silent "up to date" while the Store shows the
  update ready. That exact bug shipped in both sibling apps.
- The version *number* comes from the Store's public display-catalog endpoint
  (`TryGetPublishedVersionAsync`, product `9PB5FJ08PNVJ`). Best-effort: null means "cannot say"
  and the Store's list is trusted alone; a published version that is not newer suppresses a stale
  offer. `LatestVersion` is empty when an update exists but its number is unknown, and About
  words that case without a version.
- **Download and install are separate Store calls.** Only `RequestDownloadStorePackageUpdatesAsync`
  reports real progress; installing needs every process in the package to exit, and the settings
  window is open by definition when someone clicks "Check for updates".
- Every check is logged, because this failure mode is otherwise invisible — the check *succeeds*.

Verify either half without a Store submission:

```bash
curl -s "https://displaycatalog.mp.microsoft.com/v7.0/products/9PB5FJ08PNVJ?market=US&languages=en-us&fieldsTemplate=Details"
```

```powershell
Invoke-CommandInDesktopPackage -PackageFamilyName '27766TechnicallyReal.CopilotKeyRemapper_gfb69tsnc4jnp' -AppId 'App' -Command 'C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe' -Args '-NoProfile -ExecutionPolicy Bypass -File <script>'
```

The readable version is inside each `PackageFullName` (`…_1.0.17.0_arm64__hash`), not the numeric
`Version` beside it (a packed 64-bit value). `StoreContext`/`Package.Current` need package
identity, which is what `Invoke-CommandInDesktopPackage` supplies; use Windows PowerShell 5.1,
not `pwsh`, and write output outside the package's redirected AppData.

## Building

- Dev UI: `dotnet build Repilot/Repilot.csproj -c Debug`.
- Handler: `dotnet publish RepilotKey/RepilotKey.csproj -c Release -r win-arm64`.
- MSIX: `RepilotMSIX/generate-msix-images.ps1` then `build-msix.ps1` (publishes both
  exes; finds SDK tools in an installed SDK or the `Microsoft.Windows.SDK.BuildTools`
  NuGet package). `-NoSign` for Store. Set `<Identity>` from Partner Center first.

## Releases and distribution (no public binaries, ever)

- **The Microsoft Store is the install route** (product `9PB5FJ08PNVJ`). Repilot is a paid
  app in a public repo, so there is nothing to give away here, and an unpackaged build is
  not a usable product anyway: the key is claimed through the
  `com.microsoft.windows.copilotkeyprovider` AppExtension, so without package identity it
  cannot be assigned (`Pages/HomePage.xaml.cs` hides the Assign button and says exactly that).
  Anyone who wants to run it from source builds it themselves.
- **A GitHub release carries notes and the tag, nothing downloadable.**
  `build-msix.yml` attaches no release assets and uploads no Actions artifact; artifacts on
  a public repo need only read access, which for a public repo means anybody. Do not add
  either back, and do not invent a portable build to fill the gap: there isn't one.
- **It still builds the MSIX on both platforms every run, and that step stays.** It is CI's
  only check that manifest stamping, `makepri`, `makeappx` and signing work. Delete it and
  the first sign of a break is a failed Store submission.
- The Store package comes from `store-publish.yml` (manual dispatch; it cannot succeed while
  the msstore CLI lacks paid-app support) or locally from `build-msix.ps1 -NoSign`, which
  keeps the real Partner Center identity and leaves the package unsigned because the Store
  re-signs at ingestion. Keep that local path working.
- Unpackaged copies check GitHub Releases and offer "View Release", a link to the release
  page, never an asset download, so notes-only releases are fine for that path.

## Adding a Windows function

Add an entry to `Services/WindowsFunctionCatalog.All` (unique `Id`, `Name`, `Group`,
`Description`, and a `Combo` or `ShellTarget`). It appears in the picker and is usable
by the handler automatically (the file is linked into both projects).
