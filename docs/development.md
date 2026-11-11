# Development

These instructions describe the current source. For the published Windows preview, check out tag `v0.2.0-preview.2` and use the instructions at that tag.

## Build from source

Use **Unity 2020.3.45f1** with Windows build support and the Mono backend.

1. Clone the repository and add its root folder to Unity Hub.
2. Install the Python dependencies and prepare your local reference:

   ```powershell
   pip install -r tools/requirements.txt
   python tools/prepare_reference.py --game "C:\Program Files (x86)\Steam\steamapps\common\Rain World"
   ```

3. Open the project, select **Cyan Lizor → Create preview scene**, then press Play.
4. Use **Cyan Lizor → Build Windows preview** to produce `Builds/LizorWindows/Lizor.exe`.

Run the automated verification suite with `$UnityEditor` set to your Unity editor executable:

```powershell
& $UnityEditor -batchmode -projectPath $PWD -executeMethod CyanPreview.Editor.PreviewBuild.Verify -quit -logFile verify.log
```

To include the self-contained asset importer in a shareable ZIP:

```powershell
pip install pyinstaller==6.22.3
python -m PyInstaller --noconfirm --onefile --name CyanReferenceImporter --collect-all UnityPy --collect-all archspec --collect-all astc_encoder --exclude-module fmod_toolkit --exclude-module pyfmodex --distpath Builds/Importer --workpath Builds/ImporterWork --specpath Builds tools/prepare_reference.py
python tools/collect_notices.py
.\tools\package.ps1
```

The packaging script includes the player, importer, and dependency notices and rejects game assemblies or reference caches. Send the resulting ZIP, not just the executable or your `local-data` folder.


## Interaction verification

`PreviewBuild.Verify` includes contact timing, cooldown/mute, the local cyan love clip, independent duplicate-ID pets, flexible dragging, extreme cursor movement, limb reach, bounded release, window movement/resizing, and support loss. Native window-control hit-testing still needs hands-on checks across applications. Custom title bars use a conservative 40-screen-pixel top strip to avoid interpreting close/drag clicks as exploration. Only one desktop pet and cyan are currently exposed in the app.

Petting readiness is checked before consuming contact/cooldown; affection and ordinary voice cooldowns are independent, with one non-overlapping voice source. Brief contact gaps up to 0.15 seconds pause accumulation. Window interior attachments preserve normalized coordinates on resize; frame attachments retain edge offsets. Terrain impact sounds use the inspected game threshold (speed > 1.5) and light/heavy blend over 6–14, with a host 0.15-second anti-repeat guard. Free background catches are not terrain collisions and do not thud.

Landing recovery is per pet, with immutable species tuning. Cyan uses a 0.15–0.35-second impact-scaled response: reduced drive, contact-normal body compression, continuing head/tail physics, and nearby reachable foot grips. Grabbing or losing support cancels it; moving support carries its contact plane. Rain World's impact thresholds, landing velocity retention, and limb reach constraints inform this desktop adaptation. Tests cover soft/hard impacts, walls/ceiling/corners, repeat contact audio, support translation/loss, grab interruption, disabled tuning, invalid inputs, and duplicate-ID isolation.

## Source layout

Open `Assets/CyanPreview` in VS Code. This is a folder-only organization; namespaces and the runtime/editor assembly definitions remain unchanged.

| Folder | What belongs here | Current files |
| --- | --- | --- |
| App | Desktop lifecycle and coordination | DesktopApp (also contains DesktopSettings, DesktopLauncher, and menu actions) |
| UI | Control panel and preview interface | PreviewApp (still includes preview coordination) |
| Pets | Per-pet decisions, motion, and interaction | PetBehavior, PetPersonality, PetMotion, PetJumpPlanner, LandingRecovery, PetInteraction, PettingContact |
| Surfaces | Geometry and permitted movement surfaces | PetSurfaces, ClimbSurfaces, DesktopSurfaces |
| Species | Species definitions and capabilities | LizardSpecies |
| Presentation | Sprite rendering, transparency, audio, effects | SpriteMeshView, DesktopAlphaRenderer, CyanPetAudio, CyanJumpEffects |
| Platform | Native Windows integration | WindowsDesktop |
| RainWorld | Shared game resources and per-pet adapters | ReferenceContext, ReferenceBridge |
| Editor/Tests | Automated verification checks | Existing *Checks classes |
| Editor | Unity build and verification entry points | PreviewBuild and editor assembly definition |
| Resources | Runtime-loaded Unity assets | Existing resources, unchanged |

Keep script `.meta` files with their scripts when moving them. The runtime assembly definition stays at the CyanPreview root; the editor assembly definition stays under Editor, so tests remain excluded from the player. Folders describe responsibilities but do not introduce new dependency boundaries. Settings extraction and separating UI drawing from coordination are future refactors, not part of this move.

Climbing modes use per-pet DesktopSettings schema 2. Schema 1 windowEdges=true migrates to EdgesAndBackgrounds; false migrates to EdgesOnly (now including window edges). New sessions default to EdgesAndBackgrounds. Both menus use the same setting. A mode change updates surfaces without recreating the pet and releases invalid support; planned propulsion can grip a reachable edge, while ordinary falls use the delayed edge-catching rules below. ClimbingModeChecks covers geometry, routing, occlusion, moving/resized/closed windows, legacy migration, invalid modes, duplicate-ID isolation, grabbing, support loss, retained edge grips, species capability limits, and planned edge landings.

FallCatchProgress is per pet and resets on each grab, new flight, and landing. A drop from the airborne apex must reach the initial body-chain length plus end radii (50 world units for current cyan, excluding tail) and at least 0.1 seconds, with downward velocity. Only species with wall-climbing capability can catch. IEdgeCatchSurfaces supplies current visible edges without background support; feet must reach an edge within their actual joint distance before attachment. LizardLimb's default hunt speed plus connection velocity informs the reaching motion. Catches preserve body position, damp velocity, and enter landing recovery without a floor thud. Ground collision still lands immediately, including short drops. Tests cover both modes, window and desktop sides, apex/threshold timing, regrabbing, missing/moved/covered edges, limb reach, and independent duplicate-ID pets.

The compact panel defaults to 540×900 and uses a separate 768×240 render texture for its contained animated preview (30 updates/sec). PanelTheme owns the reusable grey/cyan styles and their generated textures. Advanced retains pose animation, view angle, identity/personality details, and screenshot capture; the large movement lab is removed from the user panel. Automated verification and a Windows build pass. Player checks covered preview animation, medium/reset size, invalid-ID feedback and recovery, expanded sections/scrolling, and desktop Start/Stop. Native right-click synchronization code remains unchanged. The user's LIAZRD title, subtitle, and scene-object wording were preserved; the attribution was revised with permission.


## First-run setup and preferences

The source now detects the saved Rain World folder and common Steam installations, including additional libraries listed in `libraryfolders.vdf`. Detection only checks for installation files; the importer still verifies the supported game fingerprint. Non-Steam/custom locations can be selected with **Browse** or pasted into the folder field. Canceling the picker preserves the current path. Detection does not overwrite a path edited while the search was running.

The setup panel explains why Start is disabled: assets have not been prepared, preparation is running, setup failed, the appearance ID needs applying, or the app is running inside the Unity Editor. The desktop pet is never started automatically.

`CompanionPreferences` stores size, climbing mode, roaming, propulsion, click exploration, sound, and the game folder in a versioned PlayerPrefs entry. Changes save within half a second and on normal close. The existing `LastId` and `ReferenceCache` entries remain compatible. Temporary process IDs, running state, and sound requests are not saved. Missing fields use defaults; malformed or unsupported preference data displays a warning and falls back to defaults.

`SetupChecks` covers preference storage round trips, partial/corrupt/unsupported data, invalid scale and mode values, multiple/duplicate/missing Steam libraries, legacy library syntax, quoted/Unicode paths, wrong data subfolders, removed installations, and launch-blocker states. The Windows player was also checked for automatic detection, native folder-picker cancellation, successful preparation, and size/mode restoration after restarting.

See [setup preview performance measurements](performance-setup-preview.md) for the local CPU, GPU, and memory samples. Setup and preference improvements are included in v0.2.0-preview.2. See [desktop rendering optimization](performance-desktop-rendering.md) for the subsequent profiling and verification.

## Lizor system tray

The current source builds as **Lizor**. Closing settings hides the panel while the desktop pet keeps running. Double-click the cyan **Liz** tray icon to reopen settings; right-click for Settings, Pause/Resume, Sound, Stop desktop pet, or Quit Lizor. Launching Lizor again restores the existing controller. Pause is temporary and is not saved across launches; Stop removes the pet without closing settings.

Existing `LastId`, `ReferenceCache`, and `CompanionPreferences.v1` values are copied once from the old Cyan Lizor Preview preference store, only where Lizor has no value. The original preferences and local reference folder stay in place. A failed migration leaves the app blocked with an explanation rather than saving defaults over user settings.

`TrayState` owns transient application state, `WindowsTray` owns native window/menu/icon cleanup, `SingleInstance` owns the native instance handle, and `LegacyPreferences` reads the old store. `TrayGlyph` creates the Liz icon without a new image dependency. The native callback queues commands for the Unity update loop. A missing tray leaves normal window-close behavior available; an Explorer restart recreates the icon or restores settings if recreation fails. Hidden settings stop preview rendering and lower their update rate while the separate desktop pet continues normally.

`TrayChecks` covers stopped/paused/quit transitions, close fallback and shutdown policy, duplicate instance disposal/relaunch, exact and partial preference migration, migration failure, old IPC defaults, and icon sizes/transparency. Player checks are still required because Unity's standalone Mono differs from its editor runtime.
