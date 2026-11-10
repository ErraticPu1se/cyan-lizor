# Preview verification log

## 2026-11-09

Completed:

- Unity 2020.3.45f1 imports and compiles the project and builds the Windows x64 player successfully.
- The real Unity verification entry point passes ID parsing, repeatability across generation order, spawner independence, random-state preservation, and finite mesh checks. Seeds include 0, 1, 2, 42, 999, -1, both signed integer limits, and 12345.
- The original local-game graphics constructor and drawing routines execute. A rendered comparison image was inspected.
- The Python importer prepares 250 sprite elements. Its standalone executable produces a matching sampled head sprite without requiring Python on the executable path.
- The Windows player's first-run import button successfully prepares assets and generates a cyan lizard from the installed game.
- The player reopens with its saved reference. Full ID input, generation by button and Enter, size slider, view angle, gentle pose motion, and saving a comparison image were exercised visually.
- Testing found and fixed stale UI drawing outside the preview viewport and Enter being consumed by the text field. The rebuilt player was checked again.
- Local game files remain outside Unity Assets and ignored by Git. The package script rejects game assemblies and reference caches.
- The repository's existing Apache-2.0 license was preserved.

Still pending:

- Side-by-side comparison with the same ID in Rain World. Original generation and drawing calls are used, but the preview uses a controlled pose and neutral palette; exact visual parity is not established.
- Testing on a clean second Windows machine and other game versions. The current adapter deliberately supports the inspected game assembly only.
- Desktop overlay, climbing, cursor petting, sound, den behavior, and installer. This milestone is an appearance preview, not the finished desktop pet.
