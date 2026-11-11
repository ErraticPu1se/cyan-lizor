# Standalone macOS dependency audit

Audited 2026-11-11 against Windows baseline `6f5f5a7`. Paths below are repository-relative.
This is a source inspection, not proof that a standalone build works.

## Finding

Preset selection removes the need to generate arbitrary IDs on Mac, but it does not remove
our current runtime dependency on Rain World. Copying three entries from saved-pets.v1.json,
copying the reference cache, or choosing macOS in Build Settings is insufficient.

`ReferenceContext` reads `reference.json`, follows its gamePath, verifies a specific game
assembly hash (v1.11.8), and loads Assembly-CSharp.dll. It initializes game graphics/atlas
objects. `ReferenceBridge.Generate` constructs game lizards and LizardGraphics, and `Draw`
calls the game's DrawSprites on every draw. Movement also uses live game BodyPart objects.

## Replacement map

| Current source | Dependency / problem | Standalone direction |
| --- | --- | --- |
| `Assets/CyanPreview/RainWorld/ReferenceContext.cs` | Assembly.LoadFrom/AssemblyResolve, pinned game DLL, Futile atlas objects, PNG cache, game sound bundle | A typed package loader with versioned data and portable assets; no game path or assembly loading |
| `Assets/CyanPreview/RainWorld/ReferenceBridge.cs` | ID parsing mixed with game object generation, LizardGraphics, pose settling, DrawSprites, FSprite list | Separate identity parsing from a standalone per-pet pose/appearance model and mesh generation |
| `Assets/CyanPreview/Species/LizardSpecies.cs` | Movement capabilities are useful, but factory creates game templates, body chunks, personality, tongue and cosmetics | Split immutable species tuning from the Windows reference factory; explicit cyan support only |
| `Assets/CyanPreview/Pets/PetPersonality.cs` | Values are copied through reflection from the game's constructor | Construct from six validated scalar values in each preset, without game initialization |
| `Assets/CyanPreview/Pets/PetMotion.cs` | Our movement decisions operate on reflected game chunks/limbs; calls connection.Update, Limb.Update/ConnectToPoint, tail and cosmetic updates | Replace these mechanics with owned typed body/limb/tail state while retaining host behaviour/events; preserve normal crawl appearance |
| `Assets/CyanPreview/Pets/PetMotion.Bounds.cs` | Reads head/tail/limb fields through the game adapter | Calculate length and occupied bounds from the new model |
| `Assets/CyanPreview/Presentation/SpriteMeshView.cs` | Converts FSprite/TriangleMesh data, atlas textures and jaw sprite indices; sorting groups and Unity meshes themselves are reusable | Render typed ordered meshes/sprites; provide an explicit mouth anchor and jaw layer rather than Rain World sprite indices |
| `Assets/CyanPreview/Presentation/CyanPetAudio.cs` | Uses ReferenceContext.LoadSound and current PetMotion | Keep playback/cooldowns/event intent; inject portable clips and motion events; missing clips must not stop pet initialization |
| `Assets/CyanPreview/Presentation/CyanJumpEffects.cs` | Particle renderer is host code but subscribes to PetMotion; static ReferenceContext import appears unused | Reuse mesh/particle logic after replacing motion coupling; verify shaders/alpha under Metal |
| `Assets/CyanPreview/Presentation/DesktopAlphaRenderer.cs` | gdi32/user32, DIBs and UpdateLayeredWindow with CPU readback | macOS composition/transparent window implementation; retain crop/alpha concepts only where applicable |
| `Assets/CyanPreview/Platform/WindowsDesktop.cs` | Win32 window enumeration, mouse callbacks, menus, work area and hit testing | Native macOS host; transform screen points/pixels/world units explicitly |
| `Assets/CyanPreview/Platform/WindowsTray.cs` and `SingleInstance.cs` | Shell notification icon, HWND messages, kernel32 mutex | Mac menu-bar item, settings activation and lifecycle/single-instance implementation |
| `Assets/CyanPreview/Platform/LegacyPreferences.cs`, `WindowsSteamPaths.cs`, `WindowsFolderPicker.cs` | Registry, Steam detection and Windows folder picker | Not part of standalone setup; retain solely for the Windows backend |
| `Assets/CyanPreview/App/DesktopApp.cs` | Settings validation requires cache/reference.json, launches same executable as child, constructs WindowsDesktop + ReferenceContext | Independent standalone configuration/bootstrap and Mac host; re-evaluate two-process design on Mac |
| `Assets/CyanPreview/UI/PreviewApp.cs` | Unguarded player startup calls SingleInstance and legacy migration; Ready depends on reference.Graphics; imports via .exe | Platform-selected bootstrap and preset-only readiness. No Rain World folder/import prompt on Mac |
| `Assets/CyanPreview/App/PetPresets.cs` | Saved choice/settings only, validates via ReferenceBridge and LizardSpecies; permits 100 entries | Reuse storage validation/atomic-save concepts; separate catalog selection from runtime appearance and enforce one active instance |
| `Assets/CyanPreview/Editor/PreviewBuild.cs`, `tools/prepare_reference.py`, `tools/package.ps1` | Windows build/import/package workflow and reference-based verification | Separate Mac build/standalone tests; preserve Windows tooling and pinned baseline |

## Logic that can largely be reused

`PetBehavior` is our desktop utility-based decision model, not the game's full tamed-lizard
AI. Keep its owner-friendly intent and personality inputs. Once PetPersonality has a plain
constructor, it need not depend on the game. `PetJumpPlanner`, `LandingRecovery`,
`FallCatchProgress`, `PettingContact`, `PetFeeding`, and the host surface geometry are already
largely data/Unity-math logic. `PetInteraction` still takes PetMotion directly, so reuse its
rules behind the new motion adapter rather than assuming it is dependency-free.

`FoodMorselView` is a procedural Unity sprite; it needs no extracted food texture. Preserve
recent feeding fixes: idle placement holds position without freezing simulation, food is
layered between jaws, and cyan uses a bottom passage with safe full-body return. The older
crawl route is preserved on `prototype/feeding-crawl-den` for future non-hopping species.

## Required preset payload (not exported yet)

A completed package needs a schema/version, species and ID provenance, content hashes and:

- Six personality values, effect colour and all appearance-specific proportions.
- Body rest lengths/radii, head and limb parameters, tail segment lengths/radii/stiffness.
- Cosmetic types, placement/distribution, mesh topology/UVs/colours and deformation bindings
  to body/tail segments. Static geometry or a screenshot alone cannot preserve flexible motion.
- Head/jaw/eye/limb frame mappings, pivots, trim metadata and sorting rules. The game switches
  atlas elements as the head rotates and limbs extend: one neutral pose misses those variants.
- Required portable textures, plus audio mappings for calls, affection, charge, launch and
  impacts. Current LoadSound opens RainWorld_Data/StreamingAssets/AssetBundles/loadedsoundeffects.
- Comparison captures over bend/grab/jump/idle poses for each ID, and deterministic replay
  inputs to compare the standalone renderer against the Windows reference.

Do not serialize reflected game object graphs, CLR type names, assembly-qualified types or
absolute Windows paths. The bundle must be data that the new runtime understands. Exporting
numbers does not replace the physics, cosmetic deformation or DrawSprites algorithms.

The next Windows export task should first inventory cosmetic types for IDs 9320, 7205 and
3616, then capture all needed variants and report omissions. No exact exported colour,
geometry or personality values have been fabricated in this preparation.

## Mac host and engine checkpoints

Keep Unity/C# as the working direction. The existing project pins 2020.3.45f1 with Mono for
runtime game loading. Unity's [2020.3 requirements](https://docs.unity.cn/2020.3/Documentation/Manual/system-requirements.html)
list an x64 Mac editor and additional Xcode requirements for Apple Silicon IL2CPP targeting.
Do not assume that pin is the right native M4 development toolchain. The Mac agent should
inspect installed tools and document an upgrade decision only on the Mac branch. Removing
runtime game loading is also what makes a static/AOT standalone build practical.
Unity's [current macOS guidance](https://docs.unity.com/en-us/engine/6000.3/manual/platform-specific/apple-mac/macos-requirements-and-compatibility)
recommends Apple silicon or Universal builds; this is not a decision to upgrade the project
to that version without compatibility testing.

Native candidates to evaluate on hardware:

- [NSPanel](https://developer.apple.com/documentation/appkit/nspanel) / NSWindow for the pet overlay;
  [ignoresMouseEvents](https://developer.apple.com/documentation/appkit/nswindow/ignoresmouseevents)
  for click-through behaviour, with accurate pet hit testing and explicit coordinate conversion.
- [NSStatusItem](https://developer.apple.com/documentation/appkit/nsstatusitem) for Liz menu-bar controls.
- [CGWindowListCopyWindowInfo](https://developer.apple.com/documentation/coregraphics/cgwindowlistcopywindowinfo(_:_:))
  as a candidate for window geometry. Availability, permissions, occlusion and update behaviour
  must be tested. Do not promise Windows-equivalent attachment or request broad permissions
  before establishing the actual feature/API requirement.

Cover Retina scale, multiple displays/negative origins, Dock position and auto-hide, Spaces,
full-screen applications, sleep/wake, display unplugging, focus and click-through. Window
tracking/global clicks can be a later milestone; the pet must still roam and be draggable
when optional capabilities are unavailable. Signing/notarization and a Mac package are later
release tasks, not outputs of this audit.

## Acceptance gates

1. A normal-window standalone preview loads all three real packages offline, without the
   game installed, reference.json, importer, registry access or dynamic game assembly loading.
2. Switching presets repeatedly leaves exactly one pet, preserves settings, and releases
   old views/audio without destroying shared immutable resources.
3. Malformed/version-mismatched packages, missing textures/audio, duplicate keys and unknown
   preset IDs fail clearly without silently selecting a different appearance or losing settings.
4. Replay comparisons cover pose, head tracking, tail motion, grabs, jumps and mouth attachment;
   an animated preview is not automatically appearance parity.
5. Native arm64 app works on the owner's Mac without Rosetta-dependent helpers or Windows DLLs;
   overlay/input/permissions are tested separately from standalone rendering.
6. Re-run Windows verification before any eventual merge of shared-code changes.
