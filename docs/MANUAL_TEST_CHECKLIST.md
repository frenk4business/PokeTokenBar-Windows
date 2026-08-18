# Manual Windows Desktop Test Checklist

Use this checklist on an interactive Windows 10 or Windows 11 desktop before packaging.

## Install / Run

- App launches without console errors.
- Tray icon appears.
- Launching a second instance does not create a second mutating app.
- Main popup opens when requested.

## Tray

- Left click toggles the popup.
- Right click opens the tray menu.
- Open shows the popup.
- Refresh starts a real refresh and disables while running.
- Settings opens the Settings view.
- Launch with Windows toggles the setting and registry registration.
- Exit removes the tray icon and terminates cleanly.

## Window

- Closing with X hides the window.
- App remains alive in the tray after X.
- Navigation works: Home, Collection, Bag, Shop, Settings.
- Window does not reopen off-screen after display changes.

## Codex

- Real Today, Last 5h, Week, Month values appear.
- Manual Refresh updates values.
- Unchanged refresh does not add XP twice.
- Missing `.codex` directory shows a friendly empty state.

## Gameplay

- Egg progress displays correctly.
- Egg hatches at 5M new post-baseline progress.
- Hatch uses a real Pokémon sprite when cached/network is available.
- Evolution updates species, sprite, and progress text.
- Nature and shiny marker display correctly when applicable.

## Collection

- Pokédex shows owned/missing species.
- Search by name works.
- Search by dex number works.
- Generation, rarity, owned, missing, and shiny filters work.
- Pagination stays in range after filters change.
- Catch Log shows graduated and discarded status.

## Shop / Bag

- Wallet balance matches used-since-install minus spent tokens.
- Rare Candy purchase increments Bag count.
- Rare Candy use applies +100M progression.
- Mint purchase increments Bag count.
- Mint use is disabled on eggs and rerolls active Pokémon nature.
- Shiny Charm can be bought once and shows active.
- Egg purchases show destructive confirmation.
- Shiny companion discard shows a stronger warning.
- Purchased egg tier persists after restart and hatches with the promised guarantee.

## Settings

- Launch with Windows enables/disables startup registration.
- Refresh interval changes polling behavior without restart.
- Manual-only refresh stops automatic polling.
- Notification toggles affect hatch/evolution/graduation/shiny notifications.
- Export creates a backup file.
- Import validates and restores a backup after confirmation.
- Invalid import leaves current save intact.
- Clear cache removes Pokémon/API cache but keeps save and settings.
- Open Data/Cache/Logs Folder opens Explorer.
- About text shows version, attribution, and unaffiliated disclaimer.

## Restart

- Active companion persists.
- Egg progress persists.
- Purchased egg tier persists.
- Collection persists.
- Catch Log persists.
- Inventory persists.
- Wallet persists.
- Claimed Codex baseline prevents XP replay.

## Error / Offline

- No internet during normal startup does not prevent opening existing state.
- No internet during required hatch shows waiting/error state and preserves progress.
- Missing sprite shows placeholder.
- Corrupt settings fall back to defaults.
- Invalid import is rejected with a readable message.

## DPI / Scaling

- 100% scaling: text and buttons do not clip.
- 125% scaling: text and buttons do not clip.
- 150% scaling: text and buttons do not clip.
- 200% scaling: layout remains usable.

## Notifications

- Egg hatch notification appears if enabled.
- Shiny hatch notification appears if enabled.
- Evolution notification appears if enabled.
- Graduation notification appears if enabled.
- No notification appears when the global notifications toggle is disabled.

