
# Cyan Lizor

A Windows desktop-lizard project. The first milestone is a Unity appearance lab
for comparing cyan lizard IDs with Rain World.

## Preview scope

- Seed number or full `ID.spawner.number` input.
- Original local game graphics constructor and draw routines, accessed through a
  small reflection adapter; no game DLLs or sprites are committed or distributed.
- Size slider, view angle, controlled pose motion, and comparison screenshots.
- Neutral preview palette. This is not a complete Rain World room simulation.
- Desktop roaming, window climbing, den placement, and cursor petting come later.

The adapter currently targets **Rain World v1.11.8**, with the assembly fingerprint
checked before loading. Mod hooks, alternate seeds, winterized creatures, rot,
and room-specific lighting are outside the first preview's comparison profile.
Visual parity must be checked in-game; executing the original generator does not
by itself prove the preview pose, palette, or presentation matches a screenshot.

## Run a packaged preview

Extract the entire Windows package, then run `CyanLizor.exe`. Keep the supporting
folders alongside it. On first launch, enter your own Rain World installation
folder and choose **Prepare local game assets**. Preparation reads the game and
writes a cache under your Windows user profile; it does not change the game.

The packaged importer is self-contained. Players do not need Unity Hub, Python,
or Docker. They do need the supported Rain World installation. A fully independent
edition with distributable assets would be a separate release decision.

## Develop

Reference editor: **Unity 2020.3.45f1 (660cd1701bd5)**, matching the inspected game.
This is the compatibility prototype baseline, not a decision about the eventual
shipping editor version.

1. Clone this repository and add its root folder in Unity Hub.
2. Create a Python environment and install `tools/requirements.txt`.
3. Prepare local assets:

   ```powershell
   python tools/prepare_reference.py --game "C:\Program Files (x86)\Steam\steamapps\common\Rain World"
   ```

4. Open the project, select **Cyan Lizor → Create preview scene**, then press Play.
5. Enter an ID and select **Generate cyan lizard**.

`local-data/` is ignored by Git and stays outside Unity's Assets folder, so it is
not silently embedded into a build. The game assembly is read from the installed
game, not from the repository. All app scripts use named assembly definitions to
avoid colliding with Rain World's `Assembly-CSharp` assembly name.

For a batch verification run:

```powershell
& $UnityEditor -batchmode -projectPath $PWD -executeMethod CyanPreview.Editor.PreviewBuild.Verify -quit -logFile verify.log
```

For a Windows x64 build, use **Cyan Lizor → Build Windows preview**, or:

```powershell
& $UnityEditor -batchmode -projectPath $PWD -executeMethod CyanPreview.Editor.PreviewBuild.Windows -quit -logFile build.log
```

Build the optional bundled importer with Python and PyInstaller:

```powershell
pip install -r tools/requirements.txt pyinstaller==6.22.3
python -m PyInstaller --noconfirm --onefile --name CyanReferenceImporter --collect-all UnityPy --collect-all archspec --collect-all astc_encoder --exclude-module fmod_toolkit --exclude-module pyfmodex --distpath Builds/Importer --workpath Builds/ImporterWork --specpath Builds tools/prepare_reference.py
```

Run `python tools/collect_notices.py`, then `tools/package.ps1`. It creates a ZIP containing the Unity player,
importer, and notices, and rejects accidental game binaries or reference caches.

## Structure

- `Assets/CyanPreview/ReferenceContext.cs`: shared local-game compatibility and assets.
- `Assets/CyanPreview/ReferenceBridge.cs`: independent pet identity and graphics state.
- `Assets/CyanPreview/LizardSpecies.cs`: supported species creation (cyan only).
- `Assets/CyanPreview/SpriteMeshView.cs`: presents reference sprite geometry.
- `Assets/CyanPreview/PreviewApp.cs`: ID input, preview controls, local import.
- `Assets/CyanPreview/Editor/PreviewBuild.cs`: scene, build, reference checks.
- `tools/prepare_reference.py`: local atlas decoding.
- `docs/architecture.md`: framework and release decisions.

## Credits and distribution boundary

Rain World belongs to its respective creators and rights holders. This is an
unofficial companion project. The repository contains the app and adapter, not
the game implementation, textures, audio, or decompiled reference sources.

Third-party importer dependencies and their licenses accompany packaged builds.
No public release or installer publication happens automatically from a commit.

## Pet behavior and interaction preview

Movement mode starts idle, watches the cursor while idle, and selects a new
roaming destination when exploration utility exceeds resting utility. Behavior (idle/roam/pursuit) is
separate from physical state (crawl/charge/airborne/grabbed). Both belong to one
pet; neither shares its timers, destinations or random generator with another.

- **Click to explore** controls short clicks away from the pet.
- Hold on the pet for **0.1 seconds** to grab, move the cursor to drag, release
  to drop. Losing focus cancels a grab safely. Dragging never becomes a click
  destination.
- **Autonomous cyan jumps** samples ballistic candidates and predicted surface
  landings. This follows JumpFinder's candidate-selection approach but is a
  desktop adapter, not the original game's complete tile/path search.
- The movement lab offers **Frame**, **Background**, and **Pole** surfaces. Pole
  mode tests crawling on a single vertical rail; autonomous jumps are disabled
  there until rail-to-rail landing support is implemented.

These are preview surfaces, not real desktop windows. Moving-window tracking,
transparent overlay, transfers between poles/windows, and exact game AI parity
remain unimplemented.

Rest and roaming duration now emerge from exertion, restlessness and utility
competition, rather than fixed bout timers. The default test arena is 720 × 360 world units (previously 180 × 84),
with a 1× default pet scale and a 1440 × 900 default player window.
Cyan jumps include a smoke burst/trail, bubbles, flash and expanding shockwave,
adapted from the locally inspected game behavior to preview shaders.

Click exploration interrupts idle on mouse press. Nearby destinations use paced
crawling; distant destinations (120–190 world units, depending on bravery) attempt a trajectory-tested
cyan jump, then continue exploring after landing. If no valid landing is found,
the pet crawls instead. Fast dragging carries the complete rendered pose and
constrains dangling limbs, so feet cannot be left behind at an old cursor position.

### Cyan charge, smoke and voice

The preview now sets the original `PrepareToJump` animation, enabling the game's
JumpRings charge drawing, and adapts its alternating tail vibration. Launch smoke
grows and fades toward dark room colors; trailing smoke uses the smaller original
radius and remaining-flight lifetime. The shader is still a host approximation.

With movement enabled, **Pet sounds** enables original cyan charge, layered launch,
and voice clips loaded directly from the installed game's sound asset bundle.
**Vocalize** previews a call; exploration and prolonged idle also trigger calls.
Voice amplitude drives the original mouth visualization and free-floating head
bubbles adapted from LizardBubble. Voice choice is deterministic by ID, but not yet
the game's complete personality/voice generator. Audio playback is per pet; clips
are shared by the reference context. No game audio is copied into the repository.

### Friendly inspection and resting

Clicks now invite investigation rather than pursuit: approach, inspect the area,
settle, then rest with small body shifts and intermittent cursor attention. A new
click replaces the current interest; grabbing cancels it. This works when roaming
is disabled. The panel displays all six original ID-generated personality values.
Trait values come from the installed game. Energy, sympathy and bravery now have
explicit desktop decision mappings described below; resting pose is also adapted.

### Utility decisions and future den/feeding

Rest, exploration, click interest and friendly owner-following compete through
weighted, smoothed utility scores with continuation hysteresis. Energy changes how
quickly restlessness grows and how strongly exploration competes; sympathy weights
click interest; bravery lowers the distance at which a safe jump is preferred and
reduces reluctance between autonomous jump attempts. These are deliberately approved
desktop personality adaptations, not claims about the original cyan AI's mappings.
Dominance, nervousness and aggression are preserved without invented effects.

Click inspection reduces interest; exertion promotes rest. Stalled cues expire.
A previously invited owner can attract following when the cursor is distant and
stays still; rapid cursor movement suppresses following. Disabling roaming also
disables autonomous owner-following. The cursor is never a prey target. Enable
**Show decision scores** to see smoothed utilities, exertion and restlessness.
Continuation bonuses affect selection beyond the displayed baseline scores.

Planned, not implemented: a movable den with a stable per-pet home reference, return-
to-den utility and destination updates when moved; feeding with separately identified
prey, hunger/hunt utility, capture/carry physics and delivery into the den. Owner cues
must remain distinct from prey targets. A carried meal should make den return the
preferred task; missing/moved dens or removed prey must invalidate only that task.
These additions belong in decisions and target ownership, with the current movement
controller executing routes. No placeholder prey or den objects are created now.


### Desktop integration prototype

The den experiment is preserved separately on `prototype/movable-den` (commit
`8ab4843`). This branch, `feature/desktop-integration`, starts from the den-free
cyan preview. Den and feeding behavior are not enabled here.

In the Windows player, generate a cyan and choose **Start desktop pet**. Keep the
control panel open; **Stop desktop pet** or closing the panel stops its companion
process. The Unity Editor keeps the preview but does not launch the overlay.

The overlay covers the primary monitor's work area, renders the pet against a
transparent background, and passes mouse input through outside the pet. Existing
movement, utility decisions, cursor attention and 0.1-second grabbing are reused.
The desktop size slider is separate from the large preview's size.

- Default movement follows the monitor work-area frame.
- **Free climbing + visible window edges** allows background movement and uses
  nearby visible application edges as grips. Windows are sampled four times a
  second; translating a gripped window carries the body and feet together.
- **React to desktop clicks** is off by default. When enabled, clicks outside the
  pet still reach the underlying application while inviting exploration. Control
  panel clicks are excluded.
- The host reads window rectangles and visibility, not titles or application
  contents. It only changes the styles of its own overlay window.

This is a first Windows integration milestone: primary monitor only, one cyan,
no tray controls or installer yet. Desktop audio, jump smoke and soft head glow
are not connected; those remain available in the preview. Color-key transparency
is used, so soft translucent effects need a later rendering pass. Window-edge
navigation is local, not a complete pathfinder around overlapping applications.
Fast resizing, mixed-DPI monitors and native dragging need more hands-on testing.

Validation: full `PreviewBuild.Verify` suite and Windows player build passed.
New checks cover coordinate scaling and negative screen origins, covered/removed
window edges, frame-only behavior, and moving the body and feet together.
Live Windows checks confirmed a visible transparent overlay, usable underlying
controls and Start/Stop shutdown. These checks do not establish full native drag
or moving-window behavior across different desktop configurations.
