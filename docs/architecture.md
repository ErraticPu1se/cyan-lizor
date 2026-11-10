# Framework and release plan

## Current decision

Unity/C# on Windows. Build a controlled cyan preview first, then prove desktop
overlay behavior. Do not attach window navigation to a renderer that has not yet
been checked against Rain World IDs.

The current prototype calls the original graphics constructor and DrawSprites
from the user's installed game. The adapter constructs only the minimal object
graph needed by those methods, supplies an empty terrain context and a neutral
palette, and converts the resulting ordered sprites/meshes to Unity mesh objects.
Game AI and room simulation are not executed. Cosmetic springs can settle in the
empty terrain context. A fixed pose is the default for comparisons.

This avoids maintaining a second approximation of the random-call sequence.
Body-part initialization (including retracted tongue segments) consumes random
values, and cosmetic constructors consume additional values conditionally.
The surrounding Unity random state is always restored. ID generation must remain
independent of future desktop behavior randomness.

The loaded game DLL hash is pinned. Supporting a game update requires a compatibility
review and new parity tests. Reflection and dynamic loading require the Mono
scripting backend for this prototype; do not switch to IL2CPP without redesigning
the reference boundary. No editor assemblies or game DLLs should enter the player.

## Comparison procedure

1. Use a normal cyan lizard with a known ID in the supported game version.
2. Disable appearance-changing mods for the reference capture.
3. Enter the same ID in the preview. Record cosmetic types, color and proportions.
4. Compare several IDs covering wing/no-wing and tuft/gecko outcomes.
5. Compare poses/angles and account for room palette, lighting, blink and expression.
6. Save captures with the ID and compatibility version. Do not call parity complete
   until both generated appearance and visual presentation have been checked.

## Next components

- Desktop overlay: transparency, hit testing, click-through, tray, focus behavior.
- Surface service: visible window rectangles and edges, monitor boundaries,
  DPI/multiple-monitor coordinate transforms, movement updates.
- Navigation: free roam or avoid window interiors; independent permissions for
  monitor-edge and window-edge climbing; account for scaled body clearance.
- Pet simulation: idle/wander/investigate/petted/return-to-den.
- Interaction/audio: stroke recognition and fade-in/out purring.
- Persistence: ID, reference profile, size, movement permissions, den and volume.

## Delivery

Start with a **portable Windows x64 ZIP**. The folder contains the executable,
Unity runtime/data files, and a self-contained reference importer. Keep caches,
user paths and game content outside release packages. Store settings and imported
reference assets under the user's application-data folder.

After the preview stabilizes, add a per-user installer and uninstall support,
then optional startup-at-login. Evaluate signing and update delivery at that point.
The installation process should not require developer tooling or Docker.

GitHub stores source, project settings, tools and documentation. Large generated
builds go to explicitly published GitHub Releases, not Git history. Unity caches,
local reference files, extracted assets, decompiled code, and game binaries stay
ignored. CI can check source-only changes without a game installation; reference
parity checks require a machine with the supported local game/editor.

## Open limits

- First preview uses an artificial pose, neutral palette and no mod hooks.
- Alternate ID seed overrides and special creature states are not exposed yet.
- Cold first-run import and rendering performance need measurements.
- Future editor upgrade needs random-sequence and rendered-image comparisons.
- Full independent distribution without a Rain World installation is not the
  current package design.
