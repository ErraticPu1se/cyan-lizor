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
- Desktop overlay, climbing, cursor petting, den behavior, and installer. This milestone is an appearance preview, not the finished desktop pet.

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

## 2026-11-10: user reference screenshots and presentation fixes

The user's ID 12345 images show a flexed body, grounded/gripping feet, cyan
rings and tail markings, and light spilling onto nearby walls from the head.
The old preview's straight spine and symmetric leg spread were an unsuitable
side-on comparison pose. Bright eye sprites alone also omitted the head light.

Changes:
- Use the original BodyChunkConnection constructors and their Update method to
  enforce body spacing, instead of blank connection objects containing only distances.
- Use the original LizardGraphics.UpdateTailSegment routine with cyan stiffness
  250 and decline 0.2. It applies the game's tail gravity, stiffness and constraints.
- Use a controlled side-on body/foot placement and the original signed-distance
  limb-flip calculation. Top view still spreads the feet when view depth is zero.
- Reset cosmetic spring state before settling each comparison pose, so changing
  the viewing angle and returning to it yields the same geometry.
- Add a head-centered light using the generated effect color and the game's
  100-unit radius and 0.6 alpha setting. The preview uses a custom additive falloff;
  it does not reproduce Rain World's darkness buffer, shadows or terrain lighting.

Validation: Unity verification passes ID/appearance determinism, independent pet
ownership, finite geometry and exact pose round-trip geometry. ID-generated
cosmetics, proportions and effect color remain unchanged by posing. Rendered
output was inspected with the added light.

Limits: body/foot targets are controlled preview positions, not game locomotion.
Tail constraints run over a bounded settling interval, not a complete room
simulation. Cyan propulsion, jumping, gripping and collision navigation remain
unimplemented. Screenshot lighting/poses are not standardized, so exact visual
parity is still unproven; no seed parameters were tuned to fit a screenshot.

## Autonomous roaming regression (2026-11-10)

`PreviewBuild.Verify` now simulates roaming without cursor clicks for behavior
seeds 0, 1, 42 and 73. Each must move at least 25 units, enter idle, then move at
least 25 units again after resuming. The initial test reproduced a stall.

Corrections: use perimeter routes instead of comparing straight-line distances
across the frame, let behavior own arrival detection using the projected surface
position, steer around corners using the upcoming surface normal, and bend the
leading body chunk when reversing direction. These are desktop adapter rules,
not execution of the complete Rain World AI.

The four roaming cases and existing head-target, idle/pursuit, jump/landing,
pet-independence and appearance checks pass in an isolated Unity project copy.
Logs are in the workspace research folder (`unity-roaming-before.log`,
`unity-roaming-after.log`, `unity-roaming-build.log`). No interactive visual
verification of this final fix has been performed.

## Behavior / interaction verification (2026-11-10)

`PetInteractionChecks.Run` is included in `PreviewBuild.Verify`. It checks
initial idle, optional click exploration, short press versus hold, drag offset,
autonomy suppression while held, release without accidental exploration,
drop/landing, focus-loss cancellation, autonomous charging/jumping/landing,
and climbing/gripping on background and pole test surfaces. These pass together
with the four autonomous roaming sequences and the existing graphics/motion
checks. Full output: workspace `research/unity-interaction-verify.log`.

Jump landing predictions use host boundary geometry and do not guarantee the
full body's exact landing point; body steering and tail constraints remain
active during the actual jump. Pole-to-pole jump planning is not implemented.


## Timing, arena and effects update (2026-11-10)

Grab threshold: 0.1s; idle: 8–15s; roaming bout: 15–25s. A timed roaming bout
returns to idle even if its current destination has not been reached. Autonomous
jumps wait for roaming rather than interrupting idle. The arena is 720 × 360
world units with a 1× default scale; default window is 1440 × 900.

`JumpEffectChecks.Run` verifies timing bounds, launch-event timing, a burst with
at least nine particles, eventual expiration, and preservation of global random
state in the larger arena. All prior motion/interaction checks also passed.
Logs: `research/unity-effects-verify.log` and `research/unity-effects-build.log`
in the parent workspace (not shipped).

Effects research: local `LizardJumpModule.Jump`, `JumpingUpdate`, `JumpLight`, and
`Smoke.CyanLizardSmoke`. The preview maps their burst/trail, power-dependent
lifetimes, flash, bubbles and shockwave into bounded per-pet particle meshes.
It does not load game room shaders or reproduce underwater/terrain lighting.


## Drag and click-target corrections (2026-11-10)

`TargetingChecks.Run` verifies a 200+ unit drag carries rendered feet even before
the next fixed update; alternating rapid drags keep feet within limb reach.
It also checks immediate idle interruption without position changes on input,
near-target crawling without a jump or position snap, and far-target propulsion
followed by arrival on both frame and background surfaces. Exploration remains
optional. Four autonomous roaming seeds and the prior checks continue to pass.

The adapter was compared with local `LizardAI` destination/run-speed decisions,
`Lizard` path-following front/middle driving, `LizardJumpModule.JumpFinder`
trajectory iteration, and `JumpingUpdate` landing behavior. Click exploration is
not treated as prey hunting: it uses gentler speed, middle-chunk alignment
weighting and destination-facing head motion. The target planner solves candidate
launch velocities and verifies them using discrete drag/gravity and host surface
collision checks. This is an adaptation, not execution of the full game AI.

Logs: parent workspace `research/unity-targeting-verify.log` and
`research/unity-targeting-build.log`. Original game files remain local only.

## Charge smoke and sound follow-up (2026-11-10)

Reviewed installed-game `Smoke.CyanLizardSmoke`, `LizardJumpModule`,
`LizardCosmetics.JumpRings`, `LizardBubble`, `LizardVoice`, and the sound mappings.
Also inspected the user's Cyan Lizard charge GIF on the official wiki.
The original charge animation is now passed to original ring drawing. Smoke
radius, damping, growth and darkening follow the reference formulas; room shader,
water interactions, sticky mouth bubbles and full voice personality remain adapted
or unimplemented. Free-floating head bubbles follow the voice amplitude.

Verification: `PreviewBuild.Verify` passed in the isolated project, including all
11 nonempty game sound clips, charge animation and draw, head bubble emission,
burst lifetime, movement regression checks and independent-pet checks.
`PreviewBuild.Windows` completed with exit code 0. Logs:
`research/unity-targeting-verify.log` and `research/unity-targeting-build.log`.
Player smoke test: movement and new sound controls shown; distant click visibly
entered Pursuing/Charging then landed at the selected upper frame edge. Player
log contains no runtime exceptions. Audio was loaded and playback controls
exercised; audible fidelity and exact frame-by-frame particle parity were not verified.
Local build: `Builds/TargetingPreview/CyanLizor.exe` (ignored).

## Held physics and turning follow-up (2026-11-10)

Grab selects the nearest of the three body chunks. That point stays pinned while
other chunks receive gravity and small deterministic struggle forces through the
existing body connections. Feet paddle within reach and tail segments receive
gravity. Cursor translation still carries interpolation history to avoid lagging
feet. Joint corrections and between-tick dragging both respect arena boundaries.
Turning bends front and rear around the middle and gates forward drive until
aligned, including near-180-degree reversals. Propulsion smoke/flash/shockwave
radius is 45% of the previous setting; trailing smoke is 60%. Voice bubbles are unchanged.

Expanded assertions passed in `research/unity-hold-edge-verify.log`:
- 45 hold scenarios: IDs 0/42/12345, front/middle/rear grips, all four extreme
  out-of-bounds corners plus an interior point, repeated outside release/cancel.
- Fixed anchor, finite positions, arena containment, bounded body separation,
  gravity sag, ongoing struggle, release fall and fast-drag limb reach.
- Grab during both charge and flight; no deferred launch while held; recovery.
- Identical fixed-step results at two render frame rates; another pet remains untouched.
- Reversal before substantial backward travel on floor, background, pole and wall.
- Existing target propulsion, near click, charge/effect lifetime, sound assets,
  autonomous roaming, rendering and independent-pet checks.
The boundary test initially failed and revealed a between-tick corner-drag escape;
the final implementation fixes it and the full suite passes. Build log:
`research/unity-hold-build.log`. Local player: `Builds/GripPreview/CyanLizor.exe`.
These checks establish behavior invariants, not exact Rain World motion parity.

## Tamed companion inspection and idle pose (2026-11-10)

Research: local `AbstractCreature.Personality(EntityID)`, `LizardAI` idle and
FollowFriend branches, `FriendTracker.RunSpeed` and friendship utility. Personality
now runs the original installed-game constructor and is exposed as an immutable
per-pet snapshot of all six traits. The original values are also assigned to the
abstract creature. LizardAI uses dominance for taming difficulty and energy for
some other species' lurking; inspected cyan movement does not directly map these
traits to speed. Since this companion is already friendly, no invented aggression,
nervousness or dominance movement multiplier is applied. Full social memory,
feeding/taming progression and game creature friendship trackers are not simulated.

Clicks are friendly investigation destinations, not prey. FriendTracker-inspired
distance easing is scaled for desktop geometry. On arrival after landing, the pet
inspects for 2-4 seconds, settles for 1.5-2.5 seconds, then rests. Repeated clicks
replace interest; grabbing clears it. Inspection works with roaming disabled.
Bounded resting weight shifts and intermittent head attention are explicit desktop
adaptations, not claims of original personality-dependent cyan behavior.

`PreviewBuild.Verify` passed (`research/unity-companion-verify.log`) including
arrival, no inspection before landing, repeated clicks during inspection/settling,
roaming toggles, grab cancellation, bounded nonzero idle movement, original
personality values, ID repeatability, ownership and preserved global randomness.
All existing movement, grab boundaries, propulsion, rendering and audio checks pass.
Windows build succeeded (`research/unity-companion-build.log`). Native player UI
showed personality values and Investigating, Inspecting and Settling states with
no runtime exceptions. Build: `Builds/CompanionPreview/CyanLizor.exe` (ignored).

## Cursor attention and nearby exploration (2026-11-10)

After a click, live cursor position drives head attention during investigation,
inspection and settling. Charge/flight retain landing-target attention for posture.
The exploration centre stays at the clicked location, so cursor movement alone
cannot drag the body around. Inspection selects small surface-valid destinations
24-40 units around the click, walks at a lower speed, and then settles. No extra
propulsion is requested for these local inspection steps. Voice particles unchanged.

Full verification passed (`research/unity-cursor-verify.log`): live cursor attention,
body movement during inspection, click-centre stability, no local jump, and bounded
surface-valid destinations at centre/corners on background, frame and pole. Existing
inspection interruption and settling, personality, grab, autonomy and rendering
checks pass. The pole climb check now verifies that it climbed with gripping feet
before inspection can legitimately walk back down. Windows build succeeded
(`research/unity-cursor-build.log`). Local build: `Builds/CursorPreview/CyanLizor.exe`.
Exact naturalness remains a visual comparison task; this is desktop exploration.

## Utility-based desktop personality (2026-11-10)

This supersedes the earlier fixed rest/roam and inspection timing notes. Reviewed
local UtilityComparer weighted utility/smoothing and FriendTracker utility/run-speed.
The host uses those concepts plus explicit continuation hysteresis (the inspected
UtilityComparer declares continuationBonus but does not apply it in the displayed
selection implementation). Host needs/scores and the approved energy/sympathy/bravery
mappings are adaptations. The full game AI/relationship world is not instantiated.

Rest/explore/click/friend scores now compete. Exertion increases with movement/work
and recovers at rest; restlessness grows at an energy-dependent rate. Inspection
satisfies click curiosity and settling ends as movement slows. Only attention,
local waypoint, obstruction and jump cooldowns retain short timing safeguards.
Stable distant owner attention is allowed only after an owner cue; moving cursors
and disabled autonomy suppress friendly following. Bravery modifies safe propulsion
preference, never trajectory validity. All scores/needs are owned per pet.

Verification passed: `research/unity-utility-verify.log`. Tests compare energy across
8 controlled seeds, sympathy weighting, brave/timid actual jump choice, 4000-step
rest/explore stability, unreachable cue expiry, stable owner attraction, disable-
autonomy interruption and rapid-cursor suppression. All existing personality,
inspection, cursor, dragging, boundaries, climbing, autonomy, sound, rendering and
ownership checks also passed. Fixed-duration assertions were removed because they
asserted the behavior this change intentionally replaces.

Windows build succeeded (`research/unity-utility-build.log`). Native player check
showed changing decision scores and autonomous transition to roaming. Runtime log:
`research/utility-player.log`. Build: `Builds/UtilityPreview/CyanLizor.exe` (ignored).
Movable den and feeding/capture/carry/delivery are documented future work only.
