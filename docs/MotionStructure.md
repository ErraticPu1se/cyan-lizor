# Motion code map

All files in `Assets/CyanPreview/Pets/Motion` form the same `PetMotion` partial class. Each pet has its own controller and mutable state. Splitting files does not introduce extra controllers or change the tick order.

| File | Start here when debugging |
| --- | --- |
| PetMotion.cs | Construction, shared fields, state transitions, fixed 40 Hz update order |
| PetMotion.Body.cs | Body forces, constraints, landing, surface translation; calls resting-attention update before forces |
| PetMotion.Crawling.cs | Move/explore requests and idle hold |
| PetMotion.Attention.cs | Cursor observation, resting upper-body rotation, head/neck response |
| PetMotion.Limbs.cs | Grip search, stepping, delayed edge catching |
| PetMotion.Graphics.cs | Animation state, voice/camouflage presentation, limbs, tail, cosmetics |
| PetMotion.Poles.cs | Pole attachment/posture and window-top floor support |
| PetMotion.Airborne.cs | Jump requests, recovery/jump planning and launch |
| PetMotion.Grabbing.cs | Grab, drag, release, and held-body physics |
| PetMotion.Social.cs | Rival/social motion integration |
| PetMotion.Bounds.cs | External displacement and body access |
| PetMotion.HitTesting.cs | Pointer hit geometry |
| PetMotion.Diagnostics.cs | Motion trace collection |

## Update order

`Advance` runs fixed ticks. `Step` updates window support and cursor persistence, handles grabbed physics separately, otherwise updates recovery/jump planning, asks behavior for a destination, steps the body, updates feet, and finally applies graphics. Keep this order when changing individual parts.

## Species configuration

`Assets/CyanPreview/Species/LizardSpecies.cs` is the shared species contract and registry. `CyanSpecies.cs` and `WhiteSpecies.cs` provide their capabilities, sounds, and tuning. `LizardBodyFactory.cs` constructs the reference game's appearance/physics objects; it is not the desktop movement loop.

`MotionTuning.cs` contains immutable movement tuning. Its `AttentionTuning` exposes deliberate/small head smoothing, angular limits per 40 Hz tick, and upper-body turn smoothing/limits. Both species currently retain the same pre-refactor attention defaults. To tune White alone, supply an `AttentionTuning` to its `MotionTuning` constructor in `WhiteSpecies.cs`.

Use tuning for numeric differences, capabilities for supported movements, and separate per-pet ability components for distinct behavior (tongue, camouflage, propulsion). Do not copy the motion controller per species or store per-pet state in the shared species definitions. Add species extension points only when an actual ability requires different logic.

## Verification

`MotionReplayChecks.Verify` logs a deterministic Cyan/White body/head replay hash for before/after comparisons. `PreviewBuild.Verify` runs the full regression suite, including pole, attention, tongue, and multi-pet checks. Runtime changes also require a Windows build and a visual preview check.
