# Windows stability candidate

Branch: `release/windows-stability`, based on main `57f5917`.
Release version: `v0.2.1-preview.1` (Windows stability preview).

## Scope

Keep main's single active cyan, saved presets, compact animated preview, setup,
settings and tray interface. UI and preference/preset serialization are unchanged.
Feeding, multi-pet management, fighting, social attention and diagnostic labels
remain on the Windows development branch and are absent from this candidate.

## Fixes

- Adapted the resting gaze, reachable foothold and supported resting-pose fixes
  from `1cf82be`, `d456cad` and `7867b8a` without their prototype dependencies.
- Adapted the stationary-window delta and background/edge attraction fixes from
  `c2fdac2`. Unchanged rectangles produce exactly zero attachment displacement;
  real movement, resize, occlusion and window removal retain their handling.
- Ported render-buffer reuse from `e6f129a` to reduce resize/allocation churn.
- Moved the low-level mouse hook off the Unity main thread. Its dedicated message
  pump reads immutable screen-space hit geometry and queues bounded button events.
  It never calls game reflection, renders or waits for Unity. Captured releases
  remain paired across pause/cancel. Overflow cancels pending interactions; stale
  hit geometry expires after 250 ms. The overlay stays click-through outside its
  native context menu. Worker shutdown removes the hook before disposal.

## Verification

Unity 2020.3.45f1: full `CyanPreview.Editor.PreviewBuild.Verify` passed, followed
by a successful Windows player build. Existing appearance, movement, click-hop,
grab, landing, effects, audio, climbing, setup, tray and preset checks still run.

Additional regressions cover gaze range boundaries and rear crossings, idle body
and tail motion, floor/wall/corner/window support, foot reach and grip resets,
stationary-window refresh at multiple scales, real subpixel movement, resizing,
occlusion, removal and no background attraction near frames. Background motion
matches an equivalent scene without a nearby window.

Input checks cover snapshot immutability, invalid/outside coordinates, work-area
clipping, original hit-geometry equivalence at five scales, paired left/right
releases, pause/cancel, bounded overflow, stale snapshots and repeated thread
startup/shutdown. Three main-thread stalls of roughly 1.55 seconds each still
processed all 100 worker probes and over 100 native mouse-hook callbacks per run.
This demonstrates isolation from Unity's message pump; it is not a measurement of
end-to-end pointer latency or a guarantee against driver/OS/global-GC stalls.

The player was opened and the single-pet interface and successful overlay startup
were observed. User confirmed cursor responsiveness, grab/release, right-click
menu, background/edge posture and GUI all look right. Saved pet file remained
byte-identical; user preference values remained unchanged (Unity's own session
counter/ID advanced normally).

## Before publication

Have the two users who reported cursor lag test this candidate on their laptops.
The release remains a prerelease while that feedback is collected. Main now includes this stability release. Build and
package from this checkout; do not reuse a Windows development-branch binary.