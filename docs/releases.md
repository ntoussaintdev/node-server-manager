# Packaging and releases

Dev Server Manager ships as a Windows x64 installer and portable ZIP. Both bundle the .NET 10 runtime and WPF/SQLite dependencies. Users do not need .NET installed; Node.js, npm, and other tools used by managed projects remain separate prerequisites.

## Install or run portable

- **Installer:** run `DevServerManager-VERSION-Setup-x64.exe`. It installs for the current user under `%LOCALAPPDATA%\Programs\DevServerManager`, creates a Start Menu shortcut, and offers a desktop shortcut. No administrator rights are required. Configuration lives in `%LOCALAPPDATA%\DevServerManager\dev-server-manager.db`.
- **Portable:** extract `DevServerManager-VERSION-Portable-x64.zip` to a writable folder and run `DevServerManager\DevServerManager.exe`. Keep every bundled file, including `portable.flag`. Configuration is created beside the EXE. Move the entire folder to carry settings with it. Project source folders and development tools are not included.

Both downloads contain an EXE and runtime libraries in an application folder, rather than a single executable that extracts native libraries into a temporary cache. Portable app data stays in its folder; launched development tools may use their own caches elsewhere.

Exit from the tray menu before upgrading, moving, backing up, or uninstalling. Closing the window leaves the app running. Installed and portable copies share existing single-instance protection; exit one before launching the other.

## Upgrade and configuration

Run a newer Setup over the existing installation. A stable installer AppId keeps one uninstall entry; upgrades preserve the AppData database. Uninstall removes installed files and shortcuts but retains configuration. To deliberately erase configuration, exit the app and delete `%LOCALAPPDATA%\DevServerManager` yourself.

For portable upgrades, extract into a new folder and copy `dev-server-manager.db` from the old folder after exiting. Alternatively, replace application files in the existing portable folder while retaining its database and `portable.flag`.

Without `portable.flag`, the app uses AppData, including source builds. If AppData has no database and a legacy database exists beside the EXE, it imports that database once using SQLite backup, including committed WAL data. The original remains available; existing AppData settings are never overwritten. To retain an earlier build's portable location, create `portable.flag` beside its EXE before launching the new build. Setup refuses to install into a folder containing that marker.

Databases may contain private paths and environment-variable values. Packages exclude databases and sidecars.

## Build locally

Prerequisites: Windows, .NET 10 SDK, and Inno Setup 6.3 or later. CI uses Inno Setup 6.4.3. Obtain Inno Setup from https://jrsoftware.org/isdl.php and review its current licensing terms for your use.

From the repository root:

```powershell
./scripts/package.ps1 -Version 1.0.0

# Specify a compiler if automatic discovery fails:
./scripts/package.ps1 -Version 1.0.0 -IsccPath 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'

# Portable ZIP only, without Inno Setup:
./scripts/package.ps1 -Version 1.0.0 -SkipInstaller
```

Output appears in `artifacts\1.0.0\`:

- `DevServerManager-1.0.0-Setup-x64.exe` (unless skipped)
- `DevServerManager-1.0.0-Portable-x64.zip`
- `SHA256SUMS.txt`

The script runs isolated database checks, publishes a self-contained Release build without trimming, checks runtime/native dependencies and actual ZIP contents, compiles Setup, and hashes the downloads. Staging files are cleaned on success or failure. Existing version output is not overwritten: move it away or use another version before rebuilding.

Versions have three numeric components, optionally with a prerelease suffix such as `1.1.0-rc.1`. The version is embedded in the app and download names; Windows file/assembly versions use the numeric components plus `.0`.

Run storage checks separately:

```powershell
dotnet run --project tests/PackagingChecks --configuration Release
```

## GitHub releases

Push a tag pointing at the tested commit:

```powershell
git tag v1.0.0
git push origin v1.0.0
```

`.github/workflows/release.yml` builds on Windows, uploads Actions artifacts, and creates a **draft** GitHub release containing the installer, ZIP, checksums, and generated notes. Prerelease suffixes mark the draft as a prerelease. Review the downloads and notes, complete the checks below, then publish the draft in GitHub Releases.

Running the workflow manually with a version produces Actions artifacts without creating a tag or release. A failed tag workflow can be rerun before a draft exists. If a draft already exists, review or remove it before rerunning; existing release assets are not overwritten.

## Packaged-release acceptance checks

Use a clean Windows x64 VM without .NET installed and a normal non-administrator account:

1. Extract the ZIP and launch the EXE. Check UI, tray, and SQLite persistence: register a harmless test project, change theme, exit, and reopen.
2. Move the portable folder and reopen; settings should travel with it.
3. Install Setup; check no administrator prompt, working shortcuts, and AppData configuration.
4. Install a newer version; check one installation entry and retained projects/theme.
5. Attempt install/uninstall while the app is in the tray; it must request an exit. Confirm normal exit stops managed test servers.
6. Uninstall; application files/shortcuts should disappear while settings remain. Reinstall and confirm settings return.
7. Compare both downloads using `Get-FileHash -Algorithm SHA256` against `SHA256SUMS.txt`.

Initial packages are unsigned; Windows may show an unknown-publisher or SmartScreen prompt. Signing can be added when a signing identity is available. Keep keys and credentials outside the repository. There is no automatic updater.
