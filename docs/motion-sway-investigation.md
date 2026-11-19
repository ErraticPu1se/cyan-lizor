# Edge sway investigation

Observation-only diagnostic build: `Builds/MotionTracePreview/Lizor.exe`.
Place `motion-diagnostics.enabled` beside the executable to enable capture. Remove it and restart the desktop pet to disable capture. No user preferences are changed. Trace files are `motion-*.jsonl` under the app persistent-data `Diagnostics` directory. Capture is capped at 12,000 simulation samples or 32 MiB per overlay session; it flushes every 40 samples. With multiple pets the sample limit is shared.

## Reproduction and finding

The user reproduced the visible sway on a window edge with the pointer out of attention range. The trace repeatedly alternates Crawling/Roaming (a movement goal and drive force) with Crawling/Idle (no goal). Tiny external shifts occur on the roaming-to-idle transitions, commonly 0.0000152587890625 local units. The visible motion includes the front body, not only the head sprite.

`DesktopSurfaces.UpdateWindows` reconstructs an edge point through normalized rectangle coordinates. Even identical old/new bounds can produce a nonzero delta from float rounding. `DesktopPet.RefreshWindows` treats any nonzero delta as movement and calls `Motion.Behavior.Rest()`, clearing the destination. Subsequent roaming attempts apply turning forces, then are cancelled again. `PetMotion` captures a fresh resting pose at each transition.

The isolated `MotionDiagnosticsChecks.InvestigateStationaryWindow` probe made 1,000 calls with identical bounds: 555 falsely reported movement; maximum delta was 0.0000305175781 local units. This corroborates the live trace. The numerical shift is tiny; the behavior cancellation is the consequential error.

Some samples also switch individual chunks between edge support and background support. That is recorded, but its independent contribution to the visible sway remains unproven.

## Validation and next change

The full PreviewBuild.Verify suite passed with graphics enabled, including 400-tick traced/untraced body/head equality, writer cap/disposal and unavailable-output checks. Windows build passed. The initial no-graphics verification run could not pass the existing shader coverage check; rerunning with graphics enabled passed. Saved preferences and preset hash were unchanged before the user reproduction.

No movement fix is included in this diagnostic change. Next: distinguish actual window translation/resizing from an unchanged snapshot, preserve occlusion/removal checks, and test repeated stationary-window refreshes at fractional scales alongside real dragging/resizing. Then replay the edge scenario before deciding whether a separate support-selection change is needed.

## Surface stability correction

The later SurfaceStabilityPreview applies the confirmed fix: attachment deltas are calculated from changes in window position and size, so unchanged bounds produce exactly zero without suppressing small real movement. Occlusion, removal and offscreen checks still run.

The free-climbing support lookup and roaming route no longer snap to a nearby window edge. The previous 30-unit edge switch introduced a support spring even when the background was climbable, causing attraction toward the frame. EdgesOnly retains edge routing; falling edge catches and moving-window attachment retain their separate geometry queries.

Regression checks first reproduced both defects. After the corrections, 8,000 stationary plane/edge refreshes across scales 1, 1.2, 1.5 and 2 passed, together with small translation, proportional resizing, occlusion, removal and edge-catch checks. A 1,600-tick comparison per idle/roaming scenario found matching body/head motion beside a stationary frame and on a plain background, including cursor attention. The complete Verify suite and Windows build passed. Preferences and saved presets matched the pre-test snapshot.

Visual confirmation remains necessary. This targets confirmed window-attached background jitter and edge attraction; it does not establish that every reported twitch on a bare desktop has the same cause.
