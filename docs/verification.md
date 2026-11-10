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

## 2026-11-10: architecture refactor

- Unity verification passes after extracting ReferenceContext and LizardSpecies.
- Two pets with the same appearance ID have distinct application IDs and graphics,
  share the atlas textures, and remain independent under interleaved generation.
- Removing one pet leaves the other able to draw and regenerate; premature shared
  context disposal and unknown species are rejected.
- Each rendered pet has its own SortingGroup and explicit group order.
- The rendered ID 12345 PNG is byte-for-byte identical before and after this
  refactor (SHA-256 34D08EF7B24DD0E34084F4DFD850957DF417103C1FC01B3A5192FDAC92EBDE24).
- Windows build succeeds, and the rebuilt player's cyan preview was inspected.
- No roster UI, second species, or persistence migration is included in this refactor.

## 2026-11-10: first in-game comparison

- In the user's Warehouse arena, Dev Console accepted `spawn CyanLizard ID.-1.12345` and the cyan lizard was visibly present afterward.
- Observed broad features also present in the preview: dark body, cyan rings and cyan tail-tip markings. This is a qualitative observation, not a numerical appearance match.
- Used `game_speed 0` for a stationary observation, then restored `game_speed 1` and closed the console.
- Arena palette, lighting, pose and scale differ from the controlled preview. Existing active mods were retained; their influence was not ruled out.
- No appearance changes were made from this observation alone. Next comparison should align pose/scale and inspect color, proportions and cosmetic details for several IDs before declaring parity.
- An F12 capture was attempted, but a new Steam screenshot file was not found; this check was observed through the live window only.

Console syntax reference: https://github.com/SlimeCubed/DevConsole/wiki/Built-In-Commands
