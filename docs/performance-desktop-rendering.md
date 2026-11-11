# Desktop rendering optimization

Measured on 2026-11-10 (America/Edmonton). Baseline: first-run setup commit `5190e81` with opt-in timing instrumentation. Optimized: this change on `feature/desktop-performance`.

## Findings and changes

The overlay already cropped its render target, but it combined all renderer bounds before clipping them to the desktop. Rain World hides a placeholder sprite at x = -10000, y = 0. Including that off-screen sprite expanded the crop across empty screen space. Empty particle meshes could also contribute origin or stale bounds.

- Clip each renderer to the desktop viewport before combining its bounds. Skip disabled/inactive renderers and empty meshes. Partially visible geometry remains included.
- Disable the particle renderer when its batch is empty and re-enable it on emission.
- Pack pixels from the existing native texture buffer instead of allocating a fresh Color32 array each frame. Retain vertical flipping, BGRA ordering, and premultiplied-alpha clamping.
- Reuse the renderer-list buffer.
- Set VSync to zero so the existing 60 FPS target is honored in both the panel and pet. The measured baseline was rendering above that target. Physics, interpolation, window tracking, input, AI, and audio timing code remain unchanged.

Unity documents that VSync overrides targetFrameRate: [Unity API reference](https://docs.unity3d.com/ja/2020.3/ScriptReference/QualitySettings-vSyncCount.html).

## Matched before/after runs

AMD Ryzen 7 6800H (16 logical processors), RTX 3060 Laptop GPU, 2560 × 1440 desktop. One cyan ID 9320 at 1.2×, Edges only, roaming/propulsion/sound/click exploration enabled. Panel with animated preview remained visible. Each fresh run warmed up for eight seconds, then collected 30 one-second samples. Before and after applications did not overlap; no Unity build/test ran during collection. Other desktop applications remained open. Autonomous movement is not a deterministic replay, so these are local observations rather than universal performance guarantees.

| Metric (panel + pet) | Before average / peak | After average / peak | Average reduction |
|---|---:|---:|---:|
| CPU | 9.52% / 10.50% | 4.88% / 5.80% | 48.7% |
| Busiest GPU engine | 9.23% / 11.06% | 2.94% / 4.71% | 68.1% |
| Combined working sets | 321.09 MiB / 328.24 MiB | 274.26 MiB / 275.24 MiB | 14.6% |
| Private committed memory | 659.56 MiB / 695.31 MiB | 599.92 MiB / 602.50 MiB | 9.0% |
| Dedicated GPU memory | 135.99 MiB / 152.00 MiB | 120.56 MiB / 121.05 MiB | 11.3% |

CPU is process CPU-time change divided by elapsed time and all 16 logical processors. GPU is the busiest GPU engine after grouping the measured processes, not whole-machine GPU use. Working sets are summed and may double-count shared pages; private committed memory is not resident RAM.

## Per-frame timings

Five-second diagnostic aggregates, weighted by frame count. Times are elapsed CPU-side durations, including waits; they do not isolate GPU shader execution. Frame includes the listed render/simulation work and excludes window/input handling.

| Metric | Before | After |
|---|---:|---:|
| Observed FPS | 74.242 | 60.000 |
| Average captured pixels | 1057063.441 | 58899.225 |
| Window refresh, ms | 1.382 | 1.048 |
| Motion, ms | 0.368 | 0.540 |
| Mesh refresh, ms | 2.902 | 3.340 |
| GPU readback, ms | 1.561 | 0.584 |
| Pixel packing, ms | 4.904 | 0.275 |
| Windows presentation, ms | 0.978 | 0.239 |
| Simulation + rendering section, ms | 11.066 | 5.324 |

Mesh refresh/reflection is now the largest measured section. Further work should profile its allocation and reflection costs; window scanning was left at its existing cadence to preserve attachment responsiveness.

## Verification

- Full PreviewBuild.Verify passed, including grabbing, extreme pointer motion, propulsion, sound cooldown/mute, window movement/resizing/support loss, landing recovery, and delayed edge catching.
- New regression cases cover empty/stale meshes, inactive/disabled objects, far off-screen placeholders, partially visible effects, all-offscreen geometry, and particle batches that empty and restart.
- Pixel-buffer checks cover vertical orientation, BGRA mapping, alpha clamping, and invalid dimensions/buffer lengths.
- Rendered full-viewport and cropped images of cyan 9320 with glow and with a head particle: 12,165 visible pixels each, zero mismatches and zero escaped/clipped pixels in both cases (test permits at most eight rasterization-edge differences).
- Windows build passed; the optimized player opened, loaded saved preferences, and started its native overlay successfully.

## Profiling again

Set `CYAN_DESKTOP_PROFILE=1` in the launching process environment before opening the player. The desktop child inherits it and writes DESKTOP_PROFILE aggregates to desktop-player.log every five seconds. It is off by default and does not change simulation decisions. Close that app and remove the variable before normal use. External CPU/GPU/memory counters must still be sampled separately.

## Limits

No long-duration leak/battery test or second-laptop measurement was performed. Native dragging and window interactions rely on the existing automated regression suite; exhaustive hands-on checks across third-party windows remain pending. The new build is local; the public release ZIP has not been replaced.
