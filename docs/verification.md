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

## Lizor tray preview (2026-11-11)

Full `PreviewBuild.Verify` passed, including `TRAY_LIFECYCLE_MIGRATION_SINGLE_INSTANCE_CHECKS_PASSED`; Windows player build passed. The first player smoke test caught an unsupported named managed Mutex constructor in standalone Unity Mono. Replaced it with native CreateMutex and repeated the full verification and player build.

Live player checks: existing appearance ID, reference path, and complete CompanionPreferences JSON matched the old registry values byte-for-byte; the old values also matched the pre-test snapshot. Pause/Resume updated both panel and desktop IPC without changing saved sound/roaming settings. Closing the panel kept controller and pet alive. Launching again restored the original controller without an extra pet. Quit while paused removed both owned processes; relaunch loaded the same settings with no persistent pause. The user confirmed the actual Liz tray context menu and double-click restore work.

Not exercised live: restarting Windows Explorer, Windows sign-out, or a second computer. Close fallback/shutdown policy and multiple icon sizes have automated coverage. The test build is `Builds/TrayPreview/Lizor.exe`; production builds now use `Builds/LizorWindows/Lizor.exe`. No release was published for this change.

## Saved pet presets (2026-11-11)

Saved pets use versioned `saved-pets.v1.json` in Lizor's persistent data directory. The first entry imports the existing appearance and pet preferences once. Installation/reference paths remain app-wide; pause, running state, process IDs, and sound requests are excluded. Preset keys are separate from appearance IDs. Writes use a flushed temporary file and atomic replacement with a previous-save backup. Recovery validates the backup and preserves the damaged original. Unrecoverable data disables preset saving rather than overwriting user files.

Full Unity verification and Windows player build passed. `SAVED_PET_PRESET_EDGE_CASES_PASSED` covers exact migration, repeated migration, duplicate appearance identity, independent rename, invalid name/species/size/appearance/selection/keys, interrupted writes, backup recovery, corrupt stores, and last-preset deletion protection. Live player loaded ID 9320, 1.5x, and the current movement/sound options; all three existing preference values matched the pre-launch snapshot byte-for-byte. Saved pets controls were inspected in the player. One pet is active at a time. Selecting a preset constructs its appearance before committing the selection.

Local build: `Builds/PresetsPreview/Lizor.exe`. No README or release change.

## Feeding edge-den prototype (2026-11-11)

This work is isolated on `prototype/feeding-edge-den`, based on the saved-presets commit. The presets branch contains no food code. A per-pet `PetFeeding` task owns placement, pickup, carrying, den entry, eating, and automatic return. `PetBehavior.TaskTarget` supplies a temporary destination without replacing autonomous movement or saving feeding state in presets. Both climbing modes work, and feeding respects disabled cyan propulsion.

Inspected local Rain World references: `Lizard.CarryObject` for a held point ahead of the leading body chunk, `LizardAI` ReturnPrey for den-directed movement, and `Lizard.SpitOutOfShortCut` for head-first body ordering on emergence. The desktop version supplies its own reachable surface targets, invisible bottom-edge entrance, and den timing; it does not run the game's room shortcut system. Native pet menu and panel offer placement; Escape cancels. Grabbing drops carried food. Cancellation, pause, stop, display/scale changes, repeated requests, and unreachable food have bounded handling.

The first player cycle completed but the user reported rigid sliding at entry/exit. Replaced whole-sprite translation with an arc-length path through the entrance: head leads, body chunks and tail follow, feet move independently, and emergence is head-first before physics resumes. Entry waits for grounded movement. `DenTransition` updates the reference body/graphics pose without committing game assets or decompiled code.

Full verification passed, including preset tests, feeding interruption/timeout/isolation cases, and real-motion feeding completion in both climbing modes with propulsion enabled and disabled. Windows build passed. The initial live build completed repeated approach/carry/eat/return cycles. The corrected transition completed live and the user confirmed it looks better. Added a nearby click-style investigation phase and normal arrival call, followed by a love call on pickup; tests cover delayed pickup, a waypoint away from food, affection priority over the arrival voice, repeat cooldown, mute, and disposal. Full verification and Windows build passed again after these additions. Current test build: `Builds/FeedingCurvedPreview/Lizor.exe`.

Scope: one pet, one nonliving morsel, current primary desktop work area. Multi-monitor den routing, feeding rewards/hunger, and visual parity with game shortcuts are not implemented. README and release unchanged.

## Windows multiple-pet prototype (2026-11-11)

Full `CyanPreview.Editor.PreviewBuild.Verify` and Windows player build passed. Added checks for legacy single-pet settings, three-pet limit, invalid/duplicate runtime keys, duplicate appearance independence, unsupported species, invalid scale, exclusive food ownership, cursor/pet pickup races, carrier removal, committed-den ownership, personality-weighted rivalry, bounded exchanges, cooldown, pause/invalid time, and fight interruption. Real reference-backed tests cover independent grabs/scales, removing a pet while others still render, unchanged health/death state after scuffle impulses, and the actual three-pet feeding coordinator (simultaneous bites, grabbed carrier, cursor-held food, and stopping other pursuits on den entry).

Local verification/build logs: `research/unity-multi-tests.log` and `research/unity-multi-build.log` in the research workspace. Test player: `Builds/MultiplePetsPreview/Lizor.exe` (ignored). Live UI checks loaded the existing selected pet, added saved IDs 9320 and 7205, displayed 3 / 3, and disabled further Add buttons. The saved-preset file hash remained unchanged. Visual scuffle/food-drag feedback is still a user check; automated invariants do not establish animation quality. No main/Mac merge or release publication is part of this change.

## Manual scuffle and follower jump regression (2026-11-11)

Windows feature branch adds a two-pet selector and Start a scuffle request. The request bypasses aggression buildup, reserves the selected pair, approaches through normal crawling, then uses existing bounded non-lethal posture/bite/recoil/cooldown phases. Held, airborne, paused, removed, off-screen-feeding, already-fighting, and cooling-down pets are rejected or interrupt the encounter. Approach times out after 20 simulated seconds with visible feedback. Request IDs prevent replay during settings polling. These controls and statuses are session-only; preset persistence is unchanged.

Also corrected food followers retaining automatic jumps and stale charges on pickup/den commitment. Settings refresh respects the follower jump restriction. Cancellation stops a pending charge without snapping an already-airborne pet down. Actual per-pet rivalry phase/tension is shown in the pet list, with active encounters highlighted.

Full Verify and Windows build passed (`research/unity-manual-scuffle-tests.log`, `research/unity-manual-scuffle-build.log`). Added tests for manual progression, bounded bites, cooldown, invalid/duplicate/removed selection, duplicate requests, selected-pair isolation, pause/grab/airborne rejection and cancellation, timeout feedback, indicator priorities, and follower charge cancellation/settings refresh. Player UI check selected two pets, submitted a request, observed Approaching and a completed/cooldown response without logged errors. Saved-preset hash remained identical, including the user's new preset. Animation quality remains a user visual check. Local build: `Builds/ManualScufflePreview/Lizor.exe`; no release or main/Mac merge.

## Reference-informed bite motion and sound (2026-11-11)

Inspected local `Lizard.FightingStance`, `Bite`, `DamageAttack`, `LizardAI` rivalry behavior, and the installation's sound trigger table. Desktop stance now braces the rear against forward head force; attack impulses oppose the front (+8) and rear (-6), without calling Violence, DamageAttack, or changing health. Idle pose stabilization no longer cancels rivalry movement. Bites alternate between participants, close the jaw fully, and only produce contact response within mouth range; missed snaps still have sound. Out-of-range posturing retries approach with a bounded limit instead of silently completing.

CyanPetAudio loads the local `lizBite2A`–`lizBite2G` NPC-contact and `lizBite3A`–`lizBite3G` missed-snap families, with the installed trigger volumes (0.3/0.5). A separate audio source and 0.3-second cooldown preserve vocal cooldowns; per-pet mute, pause, and disposal stop playback. Missing optional bite clips produce a warning without disabling normal pet audio. No game audio was copied into the repository or build package.

Full verification and Windows build passed (`research/unity-bite-tests.log`, `research/unity-bite-build.log`). Tests cover actual local sound-family loading, repeated snaps, mute/disposal, opposing body impulses, invalid targets, held-pet attack rejection, and existing non-lethal/feeding regressions. `Builds/BitePreview/Lizor.exe` opened with the saved preset. Listening and game-like animation quality still require the user's check; reference mechanics do not establish exact Rain World visual parity.

## Body contacts and independent bite decisions (2026-11-11)

Windows feature branch replaces scripted alternating attack turns with separate per-pet jaw readiness, random bite decisions, snap cooldowns, and hit recovery. Reference review covered LizardAI.AggressiveBehavior, Lizard.AttemptBite/JawReady/JawsSnapShut, and the cyan breed values (forward 25, attempt radius 90, chance 0.46511626, delay 12 ticks, homing 1.4). Desktop encounter duration remains bounded; this is an adaptation, not the full game AI.

PetContacts resolves scaled body-chunk overlaps and inward velocities without damage. Mouth contact tests all target body chunks using body heading rather than cursor gaze. Both pets decide from one contact snapshot before responses apply. Approach continues until mouth contact instead of stopping at a fixed head distance. Confirmed hits affect the contacted chunk and release only the victim's carried morsel; misses, cursor-held food, another pet's food, and den-committed food cannot release it. Food carriers cannot bite while holding food. Held and off-screen-route pets are excluded. Collision resolution is discrete; extreme-speed tunneling is not covered by continuous collision detection.

Full Verify and Windows build passed (research/unity-contact-tests.log and research/unity-contact-build.log). ContactBiteChecks covers independent and simultaneous decisions, misses, recovery, invalid/paused time, scaled rear-body contact, backwards bites, coincident overlaps, held-pet exclusion, approach-to-contact gating, victim food release, ownership boundaries, finite positions, and unchanged health/death state. Existing generation, motion, audio, feeding, and multi-pet checks also passed.

Packaged player Build/ContactPreview (actual path: Builds/ContactPreview/Lizor.exe) opened, started the preserved pet, added a session-only second pet, and accepted Start a scuffle, reaching Posture and Cooldown without logged runtime exceptions. Preset file hash and LastId/ReferenceCache/CompanionPreferences matched the pre-change snapshots. Natural fighting appearance and audible hit timing still need a user check; synthetic contact tests do not establish visual parity. No main/Mac merge or release publication.

## Feeding approach without synchronized waiting (2026-11-11)

Replaced the fixed 75-unit inspection ring and mandatory 1.5-second pause with an observer-distance-based waypoint. The feeding task owns that waypoint so generic cursor exploration cannot settle there. Arrival immediately transitions to food approach; a pet already within pickup range can claim food in the same step. The five-second fallback only prevents an unreachable waypoint from blocking approach. Shared food ownership, fighting, den travel, and saved preferences are unchanged.

Full Verify and Windows build passed: research/unity-food-approach-tests.log and research/unity-food-approach-build.log. Added regressions for immediate nearby pickup, waypoint arrival without waiting, distinct approach offsets/directions, and paused/grabbed pickup prevention. Existing motion tests exercise the complete feeding journey across climbing modes and jump settings. Test package: Builds/FeedingApproachPreview/Lizor.exe. Multi-pet visual timing still requires user observation.

## Threat, lunge, and recovery (2026-11-11)

Inspected local Lizard.PrepareToLounge/Lounge, FightingStance, and cyan breed parameters. Each pet now prepares independently before a short committed-direction lunge. Cyan range is 130 local units; base preparation is 35 simulation ticks, with a small per-pet aggression-weighted hesitation. Propulsion applies 4.5/(chunk index+1) for the first 10 of 20 lunge ticks. The 40-tick recovery follows the inspected Lounge exit path; it is not the unused cyan postLoungeStun breed field. Threat posture is softened for desktop surfaces, and recovery applies a small backward force. These are desktop adaptations, not execution of the full game combat AI.

Bite contact remains required for a hit and food release. Misses close the jaw and recover. Facing/range loss cancels preparation; a committed lunge does not home toward a moving rival. Grabbing, unavailable pets, and ending the encounter clear propulsion. Health and lethal game APIs remain untouched. Threat/Lunge/Recoil statuses appear independently for each participant.

Full Verify and Windows build passed (research/unity-lunge-tests.log, research/unity-lunge-build.log). Tests include preparation before biting, misalignment cancellation, lunge interruption, recovery lockout, real body displacement, held-body safety, simultaneous decisions, misses, food ownership/drop rules, and unchanged health. Build: Builds/LungePreview/Lizor.exe. Preset file and LastId/ReferenceCache/CompanionPreferences match pre-change snapshots. Visual fidelity remains a user check.

## Lunge preview memory sample (2026-11-11)

Windows LungePreview, settings panel visible, fighting off, sequentially 1/2/3 active pets. Primary ID 2222, then 9320, then 7205. Each count used a 10-second warm-up and five samples at five-second intervals. Values below sum the settings and desktop-pet processes; units are MiB (bytes / 1048576).

| Active pets | Average working set | Observed working-set range | Average private committed |
| --- | ---: | ---: | ---: |
| 1 | 302 | 287-340 | 632 |
| 2 | 394 | 360-435 | 803 |
| 3 | 393 | 377-414 | 788 |

The panel stayed near 131 MiB; variability came mainly from the pet process. These short, sequential samples include allocation spikes and retained caches. They do not establish a leak, prove that three pets cost less than two, or isolate incremental per-pet memory. No optimization is claimed. Raw local measurements: research/lunge-memory-1.csv, lunge-memory-2.csv, lunge-memory-3.csv. A longer repeated add/remove run and allocation profiling would be needed to explain the spikes.

Live check reached Threat but the initially separated pair timed out before a completed exchange could be confirmed. Added preparation-range hysteresis (35% margin while crouching) to avoid cancelling solely because the backward crouch crosses the starting range. Added margin/reset regression checks; full Verify and Windows build passed again. Memory samples above were taken before this final range-only correction. The corrected player was repackaged; full visual lunge/recoil quality remains unverified.

## Multi-pet render-buffer churn (2026-11-11)

Investigation found that DesktopAlphaRenderer.EnsureSize replaced the render texture, CPU readback texture, managed BGRA array and native DIB whenever the rounded union bounds changed width or height. Multiple independently moving pets enlarge and reshape that union. This is a concrete source of allocation churn and a plausible contributor to the sampled spikes; no allocation-profiler capture yet attributes all live-process spikes to it.

ReuseCaptureRect now retains bounded capacity through small fluctuations, grows in 128-pixel buckets, and shrinks after 120 consecutive frames with less than half the area needed (about two seconds at 60 FPS). Display shrink clamps capacity immediately. Camera/readback/presentation use the same padded rectangle, and the render target is explicitly released before destruction. Tradeoff: additional transparent pixels are rendered while capacity is retained; this is not a CPU/GPU optimization claim.

Full Verify and Windows build passed (research/unity-buffer-tests.log and research/unity-buffer-build.log). A deterministic 600-frame crop-policy stress test reduced resize decisions from 600 to 2. Calculated cumulative managed BGRA allocation for that sequence fell from 1,574,092,800 to 5,767,168 bytes; these are allocation-volume calculations, not resident-memory measurements. Tests cover negative desktop origins, preserved bounds, delayed shrink and smaller displays. Padded-crop versus full-screen rendering had zero mismatched or clipped pixels in both glow and head-particle comparisons. Existing movement, effects, feeding and combat checks passed.

Builds/BufferReusePreview/Lizor.exe is packaged separately. The user's running LungePreview was left untouched for their fighting review. The optimized player has not yet been live-profiled or visually checked; no new before/after RAM claim is made. Saved-settings code and combat behavior are unchanged.

## Close-range bites and body-focused targets (2026-11-11)

Rechecked local Lizard.AttemptBite and LizardAI.AggressiveBehavior: normal contact bites are independent of Lounge. PetBite now opens its jaw and attempts a probabilistic contact snap without requiring lunge preparation or alignment toward the rival head. Actual forward-mouth/body contact remains mandatory. Ordinary snaps use bite cooldown without the full post-lunge recovery; lunge hits/misses retain that recovery. Holding food, unavailable/held pets and hit recovery still block biting.

PetContacts.TargetPoint selects an already-contacted body chunk first, otherwise the nearest chunk surface. Approach, engagement distance, facing, lunge direction and missed-snap target use that body point. These are the head, middle and rear physics chunks; tail/feet sprites are not additional hitboxes. Target selection is a desktop adaptation, not a claim that the original game uses this exact selection algorithm. Shared food is still released only by a confirmed hit on its carrier; health is untouched.

Full Verify and Windows build passed (research/unity-close-bite-tests.log and unity-close-bite-build.log). Added contact-without-lunge, contact-loss, failed-chance snap, rear-body targeting and movement-target regression assertions; existing simultaneous decisions, held-pet exclusion, food ownership, non-lethal state, lunge movement and rendering tests passed. Saved presets and LastId/ReferenceCache/CompanionPreferences matched pre-change snapshots. Packaged Builds/CloseBitePreview/Lizor.exe separately; the user's active player was not replaced. Visual close-range bite quality remains a user check.

## Pickup isolation from active rivalry (2026-11-11)

The reported simultaneous wiggle was not reproduced as shared affection playback: each pet has distinct AudioSource, voice intensity and graphics state. However, pickup cleanup called BeginIdleHold/Rest on every losing pet, and food-following could overwrite an active combat target before the social update. ResetInterest now cancels only food state when a rival target is active. Non-carriers in active combat defer food investigation/following until combat ends, while carriers still carry their morsel toward the den. Food revision is invalidated so loose-food investigation can restart afterward.

Regression tests exercise actual host pickup with a second pet already preparing a bite: its target, hold posture, jaw readiness and voice intensity survive pickup and subsequent feeding updates. An actual carrier affection-call request leaves the other pet's voice clip and cooldown unchanged. Following and loose-food investigation resume after combat ends. Existing food-drop, held-pet, ownership and non-lethal checks remain in place. This fixes the confirmed task-priority conflict; the exact reported visual wiggle still needs a player check.
Full Verify and Windows build passed (research/unity-pickup-isolation-tests.log and research/unity-pickup-isolation-build.log). Separate package: Builds/PickupIsolationPreview/Lizor.exe. Persistence code and the running user preview were untouched; visual validation is pending.

## Carrier pursuit freeze regression (2026-11-11)

Reproduced the pickup freeze before the fix: the new ConfigureRival regression failed with "Food challenger stopped to posture instead of pursuing carrier" (research/unity-carrier-freeze-before.log). Combat held the challenger during threat preparation/contact, while also replacing the mouth-occupied carrier's feeding destination with the rival. Local LizardAI distinguishes ReturnPrey from pursuit; AggressiveBehavior updates a target destination independently of close-contact AttemptBite.

ConfigureRival now preserves the carrier's feeding destination and removes its attack posture. The challenger can keep crawling and turning toward the carrier body while preparing, and ordinary mouth contact does not hold it still. Committed lunges and hit recovery still retain control; these exceptions are specific to a currently carried morsel, so ordinary non-food encounters keep their existing stance. Moving preparation is a desktop adaptation. The carrier remains eligible to receive confirmed non-lethal bites and drop its food until den entry.

The previously failing assertions pass, including carrier escape-route ownership, pursuit during contact, and a real body-motion loop advancing toward a moving carrier. Recovery is still enforced and normal control returns on food drop. Full Verify passed, including food ownership/drop, independent audio, held-pet, movement and effects checks. Preset-file hash and LastId/ReferenceCache/CompanionPreferences match pre-change snapshots. Visual reproduction on the user's desktop remains pending.
Windows build passed (research/unity-carrier-freeze-build.log); full tests: research/unity-carrier-freeze-tests.log. Separate package: Builds/CarrierPursuitPreview/Lizor.exe. The running player was left untouched.

## Carrier charging/airborne target retention (2026-11-11)

Reproduced target loss before the fix: the host regression failed with "Carrier Charging cancelled challenger target" (research/unity-air-carrier-before.log). AdvanceScuffles required both participants to be Crawling, so charging immediately interrupted the encounter and removed the challenger target.

CanRemainInScuffle now distinguishes a target from an attacker: a current food carrier can remain a rival while Charging or Airborne, provided it is not cursor-held or using the den route. The challenger still requires Crawling and footing to attempt a bite; the occupied-mouth carrier cannot attack. Manual scuffle startup remains restricted to grounded pets. Ownership loss removes the airborne-carrier exception; den entry, pause, fighting-off, held pets and encounter timeout retain their cancellation rules.

Full Verify passed. Regression checks drive the host across Charging, Airborne and Crawling, asserting retained body targets and no carrier attack; den entry ends pursuit and airborne ownership loss removes special eligibility. Existing food-drop/contact, non-lethal, held-pet, feeding, movement and effects checks also passed. Saved-preset hash and LastId/ReferenceCache/CompanionPreferences match pre-change snapshots. This reproduces and fixes the code transition; visual confirmation of the reported desktop sequence remains pending.
Windows build passed. Logs: research/unity-air-carrier-tests.log and research/unity-air-carrier-build.log. Separate package: Builds/AirborneCarrierPreview/Lizor.exe; existing running player left untouched.

## Opt-in pickup diagnostic preview (2026-11-11)

Added observation only; feeding, combat, movement and preference decisions are unchanged. A package-local pickup-diagnostics.enabled marker enables per-pet cyan TextMesh labels showing motion/feeding state, threat/lunge/recovery or intent, movement hold, jaw opening/cooldown, target position/body-chunk match, mouth contact, voice intensity, frame displacement and grips. Labels are bounded to the desktop and remain outside pet pointer hit testing. Read-only getters expose current drive destination and bite cooldown.

Automatic pickup tracing keeps roughly two seconds of sampled history (100 phase records at nominal 10 Hz), then writes about twelve seconds after pickup. It records before/after contacts, after feeding, after social decisions, and after motion, plus the pickup event itself. Each pet has a stable runtime key; snapshots contain all body positions/velocities, requested target, drive target, actual frame displacement and food ownership. Body-chunk labels match the stored rival target position to current body positions within two world pixels; -1 means no close match and does not itself prove that the pet has no target. Logs are capped at 5000 records or 8 MiB per session; reaching the cap shows TRACE FULL. Disk failures disable tracing rather than changing simulation behavior.

Runtime traces are local only: %USERPROFILE%/AppData/LocalLow/ErraticPu1se/Lizor/Diagnostics/pickup-*.jsonl. They contain pet configuration names/keys and simulation data, not screen captures or window titles. Read a live trace with file sharing enabled, or quit the player before opening it with a restrictive reader. Diagnostic text/serialization adds overhead and enlarges the render crop; do not use this build for performance comparisons.

Full Verify and Windows build passed (research/unity-diagnostics-tests.log and research/unity-diagnostics-build.log). New tests check target identification, independent labels, automatic pickup file creation, pre-event history, observation without state mutation, trace limits and idempotent disposal. The package Builds/PickupDiagnosticPreview/Lizor.exe includes the marker; ordinary builds do not. Live check started the saved pet, added a second session-only pet, and displayed both labels. The user confirmed labels are visible and will reproduce the freeze. Saved-preset hash and LastId/ReferenceCache/CompanionPreferences matched pre-change snapshots. No behavior fix or freeze diagnosis is claimed by this change.

## Automatic food-carrier competition (2026-11-11)

The pickup trace showed the challenger falling back to a 75-pixel food-following offset with no bite activity. Pickup also cancelled its pending charge, and charging/airborne challengers were ineligible for rivalry. Food-carrier competition now starts immediately when fighting is enabled and the pair is available, without waiting for ordinary tension buildup. Both participants retain body-targeted pursuit eligibility through charge/flight. Existing cooldowns, grabs, pause, den routing, occupied mouths, and non-lethal contact/food-drop rules remain enforced. With three pets, automatic food competition selects a carrier pair rather than a pair of non-carriers; the existing one-encounter-per-pet limit remains.

Pickup cleanup preserves a competitor's pending charge. Pursuit updates the carrier body target each social update and allows the existing species-aware jump planner when the challenger's jump preference permits it. Already launched jumps retain normal physics; this does not add aerial homing. Fighting off retains peaceful following and existing pickup charge cancellation.

Full PreviewBuild.Verify and Windows player build passed. Added tests exercise actual automatic pickup, all nine challenger/carrier crawling/charging/airborne combinations, moving body targets, actual progress through feeding/social/motion updates, three-pet target selection, jump preferences, fighting disabled, grab/pause interruption, den commitment, cursor-held food, removal, invalid time, and cooldown preservation. Existing contact-hit food drop and non-lethal checks passed. Logs: research/unity-food-chase-tests.log and research/unity-food-chase-build.log in the local research workspace. Initial no-graphics verification could not satisfy the existing glow rendering test; the complete graphics-enabled rerun passed.

Saved preset hash and LastId/ReferenceCache/CompanionPreferences matched the pre-change snapshot. Separate package: Builds/FoodCompetitionPreview/Lizor.exe, with opt-in diagnostic labels/tracing enabled. The running diagnostic player was not replaced. Visual chase quality still requires a player check; automated state and movement tests do not prove animation quality.

## Pursuit takeoff, contact bites, and cooldown (2026-11-11)

Reviewed local Rain World Lizard.AttemptBite (jaw readiness/contact independent of LegsGripping, with grip-gated movement response), Lizard's bite-delay update, and LizardJumpModule.RunningUpdate/JumpingUpdate (beneficial trajectory selection and separate airborne body-orientation forces). The changes below adapt those distinctions to desktop surfaces rather than importing room AI or damage calls.

Pursuit jumps require facing the carrier and separation beyond close engagement distance. Charges cancel if the carrier becomes close, moves behind, disappears as a rival, or jumping is disabled; cancellation has a short retry delay. Immediately before launch the existing species-aware trajectory planner recomputes the landing/velocity for the current body target. Once airborne, pursuit body-orientation forces follow momentum instead of turning back toward a passed landing point. Existing flight physics, landing checks, effects, and non-pursuit jumps remain in place; the head may still track the rival independently.

Jaw readiness now builds while approaching within engagement range, including charge/flight. Contact snaps do not require a grip, while lunge preparation/propulsion still require grounded gripping. Charging/airborne snaps apply no extra attacker propulsion impulse. Confirmed body contact, species bite probability, empty mouth, bite delay, hit recovery, held-pet exclusion, and non-lethal food-drop handling remain enforced. Encounter cooldown now progresses with valid unpaused elapsed time even while either pet is unavailable or fighting is disabled; pause does not consume it.

Full Verify passed, including new PURSUIT_TAKEOFF_AIR_CONTACT_COOLDOWN_CHECK_PASSED checks: moved-target takeoff planning, close/behind/lost/disabled charge cancellation, old-target overshoot without reversal or relaunch, airborne jaw readiness/no-contact rejection, ready contact snaps, bite delay/recovery, charging/airborne food drops without attacker propulsion or health changes, grabbed-pet exclusion, paused/invalid-time cooldown, cooldown while unavailable/disabled, and subsequent chase restart. Existing shared-food, independent-pet, movement, and rendering/effects checks also passed. Test log: research/unity-pursuit-bite-tests.log in the local research workspace. Preset hash and saved preference values matched snapshots. Desktop visual behavior still needs user confirmation; bite success remains probabilistic and requires actual contact.

Windows build passed (research/unity-pursuit-bite-build.log). Separate package: Builds/PursuitBitePreview/Lizor.exe with diagnostic labels/tracing. The currently running player was left untouched.

## Flat-background idle gaze stability (2026-11-11)

The focused reproduction found cursor-driven idle target discontinuities rather than a nonzero background support force: BackgroundSurfaces.Nearest returns the same interior position and a zero normal. Tiny cursor motion across the rear direction switched clamped body/neck turns between opposite limits. Attention range checks and timed distant glances introduced additional discontinuities. In a 600-tick measurement after warmup, rear-crossing body travel was 307.065 local units versus 11.992 with a distant cursor; the maximum head step was 2.944 local units.

Idle body/neck turns now change gradually, with a stable turn side in the rear cone and smoothly reduced cursor attention between 150 and 210 local units. Timed distant cursor pulses were removed. A small neutral area around the head prevents tiny offsets from selecting opposite look directions. Existing body sway, original head spring/neck constraint, and original tail updates remain active. These fields belong to each PetMotion; no saved preference format changed. Non-idle pursuit and attack aiming remain separate.

CompanionChecks.IdleStability covers distant/near/stationary rear cursors, tiny rear crossings, attention range crossings, the desktop ObserveCursor boundary, cursor disappearance, and cursor directly on the head. It measures body/head step sizes, changes in body displacement, drift, and continued body/tail animation. Baseline measurements: research/unity-idle-jitter-before.log. Final verification log: research/unity-idle-jitter-tests.log in the research workspace. Visual confirmation on the reporting user's machine is still required.

Release scope: the next tag should prioritize idle stability and the mouse-input thread conflict before further multi-pet development. This change does not resolve the mouse-hook issue, split release branches, or publish a tag.

Full Verify and Windows build passed (research/unity-idle-jitter-tests.log and research/unity-idle-jitter-build.log). Rear-crossing body travel fell to 11.722 local units and maximum head step to 0.042; range-crossing body travel fell from 156.980 to 12.135. Cursor-on-head testing exposed an additional neck instability, resolved by the neutral aim area (maximum head step 0.104). Body and tail animation remain nonzero in all eight scenarios. The same original gaze discontinuities were confirmed in tag v0.2.0-preview.2. Preset hash and saved preferences matched snapshots. Separate build: Builds/IdleStabilityPreview/Lizor.exe, without the diagnostic marker; running player untouched.
