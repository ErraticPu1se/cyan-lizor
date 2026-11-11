# Setup preview performance sample

Measured on 2026-11-10 (America/Edmonton), using the local Windows SetupPreview build.

- AMD Ryzen 7 6800H, 16 logical processors; NVIDIA GeForce RTX 3060 Laptop GPU.
- Desktop work area: 2560 × 1440. One cyan, ID 9320, 1.2× desktop scale, roaming and propulsion enabled, Edges + backgrounds, sounds enabled, click exploration disabled.
- Settings panel remained open with its animated preview. Other applications were running; only the new preview and its desktop child process were counted.
- Each row contains 30 one-second samples, after discarding the initial counter sample. The follow-up was taken a few minutes into the same session.

| Scenario | CPU mean / peak | GPU engine mean / peak | Working sets mean / peak | Private committed mean | Dedicated GPU memory mean |
|---|---:|---:|---:|---:|---:|
| Panel only | 2.41% / 3.60% | 0.71% / 0.74% | 130.41 / 130.45 MiB | 291.84 MiB | 56.12 MiB |
| Panel + pet | 10.21% / 11.66% | 8.94% / 10.31% | 298.58 / 301.39 MiB | 621.03 MiB | 124.47 MiB |
| Panel + pet, follow-up | 9.65% / 11.30% | 8.66% / 10.15% | 298.24 / 302.33 MiB | 616.09 MiB | 124.18 MiB |

CPU uses process CPU-time deltas normalized across all 16 logical processors. GPU uses Windows GPU Engine utilization counters, grouping the measured processes by engine and taking the busiest engine each sample; this is not whole-machine GPU utilization. Working sets are summed across the panel and pet, so shared pages may be counted twice. Private committed memory is not the same as resident RAM. GPU memory uses Windows GPU Process Memory counters.

The follow-up working set stayed close to the initial active sample and private committed memory fell slightly. This short observation does not prove absence of memory leaks. No overnight run, cross-laptop comparison, or power/battery measurement was performed.

The active pet is materially more expensive than the panel alone. A subsequent optimization task should profile the desktop rendering/update path before changing behavior or reducing visual quality. These measurements alone do not isolate the bottleneck.

Validation: full PreviewBuild.Verify passed, Windows build succeeded, native folder-picker open/cancel and automatic detection/preparation passed in the player, and ID/size/climbing mode survived a restart. New files and settings are local to feature/first-run-setup; no new public release was uploaded.
