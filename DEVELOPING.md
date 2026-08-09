# Developing Repilot

Repilot remaps the Windows 11 Copilot key via the official Microsoft "Copilot
hardware key provider" model. It ships as a signed **MSIX** (required for it to be
assignable in Settings).

## Architecture (two processes, by design)

The keypress path is deliberately **thin and non-resident** — nothing runs in the
background just to wait for the key:

- **`RepilotKey.exe`** (`RepilotKey/`) — a tiny self-contained (ReadyToRun, trimmed,
  single-file) exe. It is the registered Copilot-key provider (Start tile
  **"Repilot Key"**). On a key press Windows launches it; it reads `settings.json`,
  performs the action (synthesized keystroke via `SendInput`, or `Process.Start` for
  an app/URI), and **exits**. No window, no resident process, ~ms startup. (One csproj
  flag from full Native AOT if the VC++ build tools are present.)
- **`Repilot.exe`** (`Repilot/`) — the WinUI 3 settings UI (Start tile **"Repilot"**).
  Launched on demand to configure the action and check for updates. Never resident,
  never in the keypress path.

Both ship in one MSIX (`RepilotMSIX/`) with two `<Application>` entries:

- `Id="App"` is the handler — named "App" so the user's key-assignment AUMID (`…!App`)
  survives updates.
- `Id="Settings"` is the WinUI app.

> **The provider (`Id="App"`) must be listed in the app list — do not set
> `AppListEntry="none"` on it.** The "Customize Copilot key" picker only offers
> visible, launchable apps; a hidden provider still registers in the AppExtension
> catalog but Settings rejects it as "no app meets the hardware key criteria." That is
> why both apps have a Start tile.

The handler and the settings app share their model/catalog/executor code via **linked
source files**, so those files stay WinUI-free and AOT/trim-safe (no NLog, no
reflection-heavy code).

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
powershell -File RepilotMSIX/generate-msix-images.ps1   # regenerate the icon/tile art + the .ico
powershell -File RepilotMSIX/build-msix.ps1             # sideload (dev-signed)
powershell -File RepilotMSIX/build-msix.ps1 -NoSign     # Store upload
```

The script publishes both exes (WinUI self-contained + the handler) into the package.
Before Store submission, set `<Identity>` `Name`/`Publisher` in
`RepilotMSIX/Package.appxmanifest` to your Partner Center values. Bump `<Version>` in
`Directory.Build.props` for each update (MSIX blocks same-version re-installs).

## Requirements

- Windows 11 (build 22621+)
- .NET 10 SDK
- Windows SDK packaging tools to build the MSIX
- Full Native AOT for the handler additionally needs the VC++ build tools (optional)

## Publishing to the Microsoft Store

Repilot is a **paid** app, and the Microsoft Store Developer CLI cannot submit paid
apps — so publishing is a manual upload today.

**Releasing:**

1. Bump `<Version>` in `Directory.Build.props` (the Store rejects a submission whose
   package version is not higher than the live one) and commit.
2. Build both unsigned Store packages:

   ```powershell
   .\RepilotMSIX\generate-msix-images.ps1
   .\RepilotMSIX\build-msix.ps1 -Platform x64 -NoSign
   .\RepilotMSIX\build-msix.ps1 -Platform ARM64 -NoSign
   ```

3. Zip `Repilot-x64.msix` + `Repilot-ARM64.msix` from `RepilotMSIX\bin\msix-output`
   into a single archive and rename it `Repilot.msixupload`.
4. Upload it to Partner Center (product `9PB5FJ08PNVJ`) and submit for certification.
5. Tag the release: `git tag v1.0.17 && git push origin v1.0.17`.

### Automated submission (disabled)

The [`Publish to Microsoft Store`](.github/workflows/store-publish.yml) workflow does
steps 2–4 via the [Microsoft Store Developer CLI](https://learn.microsoft.com/windows/apps/publish/msstore-dev-cli/overview).
It is **manual-dispatch only** and cannot succeed while the CLI lacks paid-app support.
Once Microsoft adds it, restore the `push: tags: 'v*'` trigger and add these repository
secrets under
*Settings → Secrets and variables → Actions*:

| Secret | Where to get it |
|---|---|
| `STORE_TENANT_ID` | Entra (Azure AD) tenant ID of the app linked to Partner Center |
| `STORE_CLIENT_ID` | Client (application) ID of that Entra app |
| `STORE_CLIENT_SECRET` | A client secret you create for that Entra app |
| `STORE_SELLER_ID` | Partner Center → *Account settings* → Seller ID |

To create the linked app: Partner Center → **Account settings → User management →
Azure AD applications → Add Azure AD application**. Create (or link) an app and give it
the **Manager** role, then add a **client secret** to it in Entra. The Store product ID
(`9PB5FJ08PNVJ`) is public and is hardcoded in the workflow.
