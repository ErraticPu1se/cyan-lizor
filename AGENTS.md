# Project engineering guidance

## Workflow

- Use the user's requested Ponytail full workflow and ECC coding standards: inspect callers and data ownership, make the smallest complete change, review the diff, and run checks appropriate to the risk.
- Ponytail source: https://github.com/DietrichGebert/ponytail/blob/main/skills/ponytail/SKILL.md. Read available skill instructions before claiming to use them; do not silently install global hooks.
- Keep changes readable and focused. Reuse existing code and Unity/.NET facilities before introducing dependencies or general-purpose frameworks.
- Preserve validation and cleanup during refactors. Non-trivial behavior changes need a small regression check covering the actual failure or invariant.
- State what was verified and what remains unverified. Original game calls alone do not prove visual parity.

## Product boundaries

- Windows desktop pet; current milestone is a single cyan appearance preview.
- Future requirements include multiple species and multiple pets. Do not implement unrequested species or present planned support as working.
- Keep UI, appearance generation, rendering, and future desktop navigation separate. Do not add pet simulation to PreviewApp or game-reflection details to new UI code.
- Keep one ReferenceContext for shared game/atlas resources. Create per-pet ReferenceBridge instances through context.CreatePet; never reinitialize shared Futile state per pet. Dispose views, then pets, then context.
- Give each pet its own application identity, separate from its Rain World appearance ID. Two pets may intentionally share an appearance ID.
- Put species-specific generation and movement capabilities behind explicit species definitions as species support is introduced; unknown species must not silently become cyan.
- Keep appearance randomness isolated from behavior randomness and other pets. Unity and game graphics calls remain on the Unity main thread.
- Version persisted settings when the pet roster is introduced; migrate existing LastId/ReferenceCache preferences without discarding them.

## Compatibility and verification

- Keep Unity 2020.3.45f1 and the current supported game assembly pinned until a deliberate compatibility review.
- Keep the Mono backend while runtime loading/reflection is required.
- Run CyanPreview.Editor.PreviewBuild.Verify for generation/adapter changes and build the Windows player for runtime changes. Exercise changed UI in the player.
- For future multi-pet support, verify interleaved generation is deterministic, duplicate appearance IDs remain independent, and removing one pet does not invalidate another's textures or rendering.
- Never commit or package game assemblies, extracted sprites, reference caches, decompiled research, credentials, or machine-local settings.
- Source lives in this repository; generated builds remain ignored. Preserve the user's license and unrelated changes.
