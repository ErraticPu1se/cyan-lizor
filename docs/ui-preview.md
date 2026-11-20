# UI refinement preview

The previous panel is preserved at `archive/ui-before-refinement` (commit `e757742`). This preview lives on `preview/ui-refinement`; existing published ZIPs remain available.

The panel keeps the current editing target visible. Opening Lizards reveals the roster, the next-launch preset chooser, and the add flow. Below 840 pixels wide, these sections move below the editor without scaling the text down. Start/Stop and Pause remain in a fixed footer. Setup keeps prepared-reference paths in Advanced. Appearance ID feedback and preset-name labels explain incomplete input.

Settings apply to the live pet. Save as new and Update preset are explicit writes; pending appearance IDs must be applied before saving. The next-launch chooser commits only the startup preset key. It neither switches a live pet nor changes the preset associated with that pet. Extra pets remain session-only; selecting a startup preset does not automatically start the desktop overlay.

Mute all sounds is a global override, available in the panel and tray. It affects every pet, including after removing the primary. Unmuting restores individual sound preferences and respects pause. Global mute is persisted separately from per-pet sound inside the existing versioned companion preferences. Older preferences and IPC settings default to unmuted.

## Verification contract

- Viewports: 540x840 compact, 920x840 expanded, and 540x840 with the roster open.
- Exercise empty roster, invalid ID, stopped/running/paused, missing setup, saved/unsaved, and muted states.
- Check text fit, scrolling, focus visibility, keyboard operation, and selection at each viewport. No claim of screen-reader support is made for Unity IMGUI.
- ReleasePanelChecks covers layout thresholds, next-launch round-trip, live-preset mapping, rejected startup keys, and global mute with mixed sound preferences and no primary pet.
- Run PreviewBuild.Verify and build the Windows player. Record actual outcomes here before publication.

## Results and experimental package

The runtime and editor/test assemblies compile against the installed Unity 2020.3.45f1 libraries using its Roslyn compiler. Only existing unused-field warnings remain. ReleasePanelChecks ran successfully in the existing 2020.3.45f1 Mono player with a temporary, isolated test harness. This includes master mute, preference compatibility, canonical appearance IDs, startup persistence, and preserving the live pet's preset mapping. The harness never starts the ordinary controller or desktop overlay.

The actual panel was rendered in that player at 540x840 and 920x840. Compact, expanded three-pet, narrow scrolled roster, invalid ID, muted, empty, and setup states were inspected. The rendered pass exposed and corrected the removal-label wrap and canonical-ID dirty-state mismatch. Keyboard navigation, native tray interaction, and real desktop overlay interaction remain unverified.

The Editor verification/build attempt stops before project loading: LicensingClient IPC connection times out, returning exit code 199. Therefore the downloadable experimental package combines the existing 2020.3.45f1 Mono player shell with the newly compiled production runtime assembly; it is not a fresh Editor player export. Test harnesses, local reference assets, and game assemblies are excluded. A normal Editor export and native interaction checks are still required before publishing a supported release or merging this draft.
