# Phase 8 Polish and Settings

## Settings Schema

Settings are stored separately from gameplay state at:

`%APPDATA%\PokeTokenBar\settings.json`

The current settings schema version is `1`. Settings are UI/platform preferences, not gameplay data, so they are not stored in `state.json`.

Persisted settings include:

- launch with Windows
- auto refresh enabled
- refresh interval
- notification master toggle
- hatch, evolution, graduation, and shiny notification toggles
- show token usage in tray tooltip
- start minimized to tray

Invalid refresh intervals are normalized to the default two-minute interval. Supported intervals are manual only, 1, 2, 5, 10, and 15 minutes.

## Startup

Launch-at-login uses the current-user registry key:

`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`

The app registers the current executable path with normal user privileges. No administrator rights are required. The Settings page reflects the requested state and the app also checks the actual registry state during startup.

## Polling

Auto-refresh runs through the application host with cancellation-aware delay logic. The default is two minutes. If auto-refresh is disabled or the refresh interval is set to manual, the background loop sleeps conservatively and does not poll Codex usage.

Refresh operations remain serialized by the ViewModel so repeated tray/menu clicks do not start overlapping gameplay refreshes.

## Notifications

Phase 8 uses a lightweight `System.Windows.Forms.NotifyIcon` balloon notification fallback instead of a heavy toast dependency.

Notifications are driven by gameplay domain events:

- `Hatched`
- `Evolved`
- `Graduated`

Shiny hatches use the shiny notification path when enabled. If shiny notifications are disabled but hatch notifications are enabled, the hatch can still produce the ordinary hatch notification. Notification failures are logged but do not block save/progression.

## Import and Export

Save export writes a portable JSON backup package containing:

- gameplay state
- Pokédex and Catch Log
- inventory
- wallet ledger
- active companion
- Codex baseline/claim state
- optional settings

Exports do not include raw Codex session files, prompt text, sprites, PokéAPI cache, or logs.

Imports validate the package before mutation. Validation rejects unsupported package versions, future game schemas, negative token ledgers, negative inventory counts, invalid companion structures, and species IDs outside Gen 1-5 scope.

Before a valid import replaces the current save, a timestamped backup is created under:

`%APPDATA%\PokeTokenBar\Backups\`

Import uses the same `GameStateStore` migration path as ordinary save loading.

## Cache Reset

The cache reset action removes only disposable Pokémon data:

- `%LOCALAPPDATA%\PokeTokenBar\Cache\pokeapi`
- `%LOCALAPPDATA%\PokeTokenBar\Cache\sprites`

It does not remove gameplay state, settings, wallet data, Pokédex, Catch Log, or logs.

## Folder Actions

Settings exposes buttons to open:

- data folder
- cache folder
- logs folder

Folders are resolved through `IAppPathProvider`; no username or machine path is hard-coded.

## Tray Behavior

The tray menu contains:

- Open
- Refresh
- Settings
- Launch with Windows
- Exit

The tooltip includes the current companion/egg and today's compact token usage when enabled. Refresh is disabled while an async refresh is running. Exit cancels polling, disposes the tray icon, and shuts down the application.

The tray icon remains the static application icon in Phase 8. Dynamic Pokémon tray icons were deferred to avoid fragile PNG-to-icon conversion and GDI lifetime risk.

## Error and Empty States

The ViewModel keeps the last successful usage/gameplay snapshot visible when refresh fails. User-facing messages are concise, for example:

- Codex usage not detected
- No Codex usage found
- Waiting for Pokémon data
- Purchase could not be saved

Collection, Bag, and Shop views retain their Phase 6/7 empty states and now sit inside a more complete navigation shell.

## Log Rotation

The local file logger rotates `poke-token-bar.log` when it reaches 5 MB and retains four rotated files. Logs remain local and continue to avoid raw Codex JSONL, prompt text, project paths, and source content.

## Versioning

The app assembly/package version is `0.1.0`. UI surfaces read this from assembly metadata instead of duplicating a hard-coded display version.

## Floating Desktop Companion

The Windows floating companion intentionally uses a normal non-topmost transparent WPF window instead of macOS-style always-visible behavior. It is designed to behave like a desktop companion:

- visible when the desktop is visible
- covered by normal apps such as Chrome, VS Code, or File Explorer
- visible again after minimizing apps or pressing Win+D
- hidden from the taskbar

The implementation avoids `Topmost=true` for normal behavior. A more invasive `WorkerW`/`Progman` desktop-parenting approach was considered, but deferred because it is more fragile across DPI, Explorer restarts, dragging, and multi-monitor changes. The current fallback is simpler and reliable: a borderless transparent WPF window with saved position/size and visible-screen correction.

The previous always-on-top setting is no longer exposed and is normalized to `false` when settings load.

## Known UI Limitations

- Windows toast notifications are not implemented; tray balloon notifications are used for stability.
- The tray icon is still the static app icon rather than the current Pokémon sprite.
- Desktop interaction requires manual verification on an interactive Windows session.
- Save import/export dialogs are UI-driven and unit tests cover the underlying transfer service rather than clicking native dialogs.
