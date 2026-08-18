# Packaging

## Versioning

The release version is defined in `src/PokeTokenBar/PokeTokenBar.csproj`.

Current version: `0.1.0`

The About page reads the assembly version instead of a separate hard-coded value.

## Release Configuration

The WPF app is configured as a Windows GUI application with:

`<OutputType>WinExe</OutputType>`

## Publish

Phase 9 uses a self-contained Windows x64 publish:

```powershell
dotnet publish src\PokeTokenBar\PokeTokenBar.csproj -c Release -r win-x64 --self-contained true
```

.NET runtime installation required by the user: No.

## Single-File Decision

Single-file publishing is not used for v0.1.0. WPF, NotifyIcon, resource files, icon loading, sprite files, import/export, registry startup, and the floating desktop companion are simpler and more reliable as a multi-file self-contained release.

## Trimming Decision

Trimming is not enabled. WPF, serialization, and resource loading can be fragile with trimming unless separately validated.

## Portable Release

The portable ZIP contains the published self-contained app plus README and LICENSE. It does not contain source, tests, Debug output, Codex logs, game saves, settings, or caches.

## Installer

The installer script uses Inno Setup and targets per-user installation under:

`{localappdata}\Programs\PokeTokenBar`

The installer preserves `%APPDATA%\PokeTokenBar` and `%LOCALAPPDATA%\PokeTokenBar` on uninstall. The app remains responsible for Launch with Windows.

## Artifact Layout

```text
artifacts\v0.1.0\
  portable\
  installer\
  publish\
  PokeTokenBar-Windows-v0.1.0-win-x64.zip
  checksums.txt
```

## Release Script

Run:

```powershell
.\scripts\publish-release.ps1
```

The script restores, builds, tests, publishes, stages the portable release, creates a ZIP, writes SHA-256 checksums, attempts an installer build if Inno Setup is installed, and smoke-tests the published executable.

## Smoke Testing

The script starts the published executable briefly, confirms it remains alive, then terminates it. Full tray/window behavior must still be manually verified on a Windows desktop.

## Known Packaging Limitations

- Installer compilation requires Inno Setup to be installed.
- Windows Explorer icon cache may delay visual EXE icon updates.
- Portable startup registration points to the current executable path; moving the folder may require toggling Launch with Windows off/on.
