# Next release panel (local review)

This release brings the Windows development species and multi-pet support into the release worktree. It has Cyan and White, up to three independently configured pets, social attention, and optional non-lethal fighting. Feeding is not exposed or startable. The development branch retains the feeding experiment.

## Panel layout

The left column edits the selected live pet: animated preview, appearance ID, size, climbing, roaming, click exploration, sound, preset saving, and details. The separate All pets card controls fighting globally. The footer pauses, starts/stops all pets, and quits.

The panel starts compact. The Lizards button opens or collapses the right column, resizing the window while retaining the active selection and unfinished draft. The species selector opens a vertical list capped at 180 UI units; additional species scroll within that area. Choosing a species closes the list; choosing the same species keeps the draft ID. The right column lists the current session's pets. Selecting a row immediately opens its controls. The expandable Add a lizard card chooses Cyan or White, filters saved presets, and accepts a new ID. Browsing is a draft; only Add creates a live instance. Duplicate appearance IDs remain separate pets. Adding while stopped prepares the roster for Start all pets.

Saved preset values change only through Save as new or Update preset. Browsing, selecting, adding, and removing live pets do not overwrite presets. Extra live pets remain session-only, as in development; this change does not add roster persistence or migrate the user's preset file.

## Code map

- `UI/PreviewApp.ReleasePanel.cs`: layout, selected-pet controls, draft browser, explicit preset saving.
- `UI/PreviewApp.Multiple.cs`: live roster and release desktop configuration.
- `UI/PreviewApp.Presets.cs`: existing preset loading and compatibility.
- `UI/PreviewApp.cs`: app lifecycle, setup, tray, preferences, preview resources.
- `App/DesktopApp.Feeding.cs`: feeding entry is disabled for this release; shared tongue/motion code remains because White's cursor tongue and existing regression checks use it.
- `Platform/WindowsDesktop.cs`: no Feed action in the pet menu.
- `Editor/Tests/ReleasePanelChecks.cs`: selection/draft isolation, duplicate IDs, cap, removal, untouched saved presets, and disabled feeding entry.

The local build uses `next-release-local` as a temporary version, not a new public tag. No changes have been pushed.
