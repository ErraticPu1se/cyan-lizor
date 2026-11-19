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

### Extension review: species and multiple pets

The shared-resource and per-pet rendering boundaries below are implemented.
Desktop behavior, roster UI and persistence remain planned:

| Responsibility | Ownership |
| --- | --- |
| Game compatibility, assembly loading, atlas textures | One shared reference context for the application |
| Species generation and movement capabilities | Explicit species definition/adapter; cyan first |
| Creature graphics, pose, behavior, size, den | One independent pet instance |
| Mesh objects and draw order | One view per pet, with ordering that keeps each pet's sprites together |
| Visible windows and monitor surfaces | Shared desktop service |
| Selection and control panel | Edits a selected pet by its application identity |
| Saved roster | Versioned collection of pet records |

`ReferenceContext` now owns assembly resolution, compatibility validation and
atlas textures. Its `CreatePet(speciesId)` factory returns an independent
`ReferenceBridge`; it rejects unknown species and a second simultaneous context.
Live pets prevent disposal of shared resources. Dispose views, then pets, then
the context. All these calls belong on the Unity main thread.

`ReferenceBridge` owns one creature's graphics, pose and comparison camera, with
a separate `PetId` GUID. `LizardSpecies` resolves supported species and owns
species-specific creation; `CyanSpecies` contains the existing cyan initialization
unchanged. Only cyan is registered. `SpriteMeshView` uses a Unity SortingGroup
per pet, with an explicit group order, while preserving the game's internal
sprite order. The control panel still presents one pet.

The next product step is a roster and selected-pet controls. Add movement
capabilities when navigation is introduced; do not scatter species-name checks
through that code. Species additions need their own generation and parity checks.

A pet's application identity must differ from its appearance ID: two pets with
the same Rain World ID still need independent size, pose, den and behavior.
Keep generation on the Unity main thread with random-state restoration. A
versioned roster save should migrate the existing `LastId` and `ReferenceCache`
preferences, and preserve data if a future schema cannot be read.

Acceptance checks for this extension: interleave two pets' generation without
changing either appearance; change or remove one pet without changing the other;
render overlapping pets without mixing their sprite layers; round-trip duplicate
appearance IDs through persistence; reject unsupported species explicitly.

### Planned desktop features

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

## Appearance comparison implementation

The controlled pose drives the original body-connection and tail-segment
constraints. Cosmetic springs reset before comparison settling; they do not
accumulate changes when users revisit an angle. This preserves ID generation
while making comparison geometry reproducible. It is not a locomotion controller.

The preview head light is a separate per-view additive pass. Species data supplies
its radius; pet state supplies its position and generated color. It currently
approximates light spill against the preview background. Full terrain lighting,
room darkness and shadows are a separate rendering integration task.

## Windows multiple-pet prototype (2026-11-11)

`feature/windows-multiple-pets` starts from the approved cyan feeding-hop prototype. One Windows overlay and one `ReferenceContext` serve up to three independent `DesktopPet` instances. Each instance has a runtime key separate from its appearance ID, its own motion/surface/interaction/audio state, and a scaled presentation root. Native coordinates and shared food use screen-pixel world units; each pet converts these to its own motion coordinates. Removing or rebuilding one instance does not release shared resources.

The selected saved preset remains the primary pet. The Desktop pets card adds independent copies of saved presets; their right-click changes are session-only. Existing versioned presets and companion preferences retain their format and values. The active roster and fighting toggle are transient controller-to-overlay settings, not persisted replacements for the user's presets.

`SharedFood` owns the single morsel. Interest is per pet, but custody is exclusive: loose, cursor, pet, or committed den entry. A loose morsel can be held after the same 0.1-second delay as a pet. Mouth-held food cannot be dragged directly. Grabbing its carrier or a scuffle can drop it. The first successful pickup wins; others follow at a small distance until den entry commits, then abandon pursuit. The existing bottom-edge hop/eat/return task remains in use. Placement projects onto a reachable surface, including edge-only climbing.

`PetScuffle` tracks pair-local tension, posture, bite, recoil, and cooldown. Aggression changes tension growth; dominance difference can end the encounter early. This adapts the inspected Rain World rivalry/posture/submission ideas, not its damage pipeline. The host uses the original FightingStance/head graphics with bounded movement impulses. There are no health changes, damage attacks, killing, or permanent grudges. Only one encounter per pet can run at once; grabbing, disabling fighting, or den entry interrupts it. Fighting starts disabled.
