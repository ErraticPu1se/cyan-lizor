# Lizor macOS handoff — start here

## Status and agreed scope

This branch is **preparation only, not a working Mac app**. It contains a dependency audit,
an identity-only preset catalog, and portable handoff checks. No Mac runtime, native plugin,
standalone appearance exporter, or exported asset pack has been implemented yet.

The owner uses an **M4 Pro MacBook** and wants **one active cyan lizard**, selected from:

| Appearance ID | Display name |
| --- | --- |
| 9320 | 3 droplets |
| 7205 | 你长的很像我的一个故人 |
| 3616 | big fluffy tail |

The Mac app must run without Rain World, its DLLs, its install path, or the Windows importer.
Arbitrary ID generation and simultaneous pets are out of scope. Preserve the flexible body,
cursor attention, independent roaming, grabbing, petting, and effects as the port progresses.
Do not replace them with a fixed prerecorded animation and call it equivalent.

## Branches and known-good baseline

- Continue on `prototype/macos-standalone`, or create a Mac feature branch from it.
- This branch starts from `6f5f5a7`, the Windows feeding prototype the owner visually approved.
- `prototype/feeding-edge-den`: current Windows feeding baseline, including bottom hops,
  live idle movement during food placement, and food between the jaw layers.
- `prototype/feeding-crawl-den` at `b0d3ee0`: preserved crawl-out version for later reuse.
- `main` remains separate. Do not merge either prototype into it as part of Mac setup.
- The README still describes the Windows release; it is intentionally unchanged.

Normal crawling must not be retuned just to accommodate feeding. The rejected foot-lift
experiment was reverted before this baseline. The cyan den uses an explicit task jump;
non-hopping species are future work, not already supported by an automatic fallback.

## Start on the Mac

```sh
git clone --branch prototype/macos-standalone https://github.com/ErraticPu1se/cyan-lizor.git
cd cyan-lizor
python3 tools/verify_macos_handoff.py
python3 -m unittest discover -s tools/tests -p 'test_macos_handoff.py'
# Optional: keep actual Mac implementation on another branch.
git switch -c feature/macos-runtime
```

Read [the dependency audit](docs/macos-dependency-audit.md), then inspect the current
source before editing. These commands only validate the handoff and catalog; they do
not build or certify a Mac application. Python 3.8+ and Git suffice for these checks.
Do not run the Windows bootstrap as a Mac player: it currently reaches Win32 calls.

## Next implementation order

1. Inventory installed Unity, Xcode, macOS version and native architecture. Record an
   explicit Mac toolchain decision; do not upgrade the Windows project silently.
2. Define the standalone appearance package and the Windows-only export/validation
   procedure. The three IDs in `macos/presets.json` are names/identities, **not appearance
   data**. Exact colours, cosmetic shapes, geometry, textures and personality still need
   to be captured or recreated. Do not invent values and call them ID-matched.
3. Introduce a separate Mac/standalone bootstrap and assembly boundary. Bypass game
   detection/import and Windows preference migration. Keep Windows reference classes
   out of the standalone execution path rather than adding empty fake game objects.
4. Implement and test the standalone body/limb/tail model and pose-to-mesh renderer,
   starting with 9320 in a normal window. Reuse existing behaviour and geometry logic
   where it is independent of game objects. Compare held, bent, idle and jump poses.
5. Add the other two presets and enforce one active runtime pet. Switching must dispose
   the previous pet, audio and views; shared immutable assets should remain shared.
6. Add the macOS overlay/menu bar/input adapter. Prove transparency, click-through,
   dragging, Retina conversion, Dock/work area, display changes and sleep/resume on hardware.
   Window attachment/global clicks need their own capability and permission checks.
7. Add native arm64 packaging and a clean-machine/no-game test, then test feeding and
   audio against the existing Windows baseline. Do not publish a Mac release early.

## What transfers through GitHub

Source, this handoff, the audit, and the three approved preset names/IDs. Windows Unity
Library/Builds, installed game, cache, sounds/textures, decompiled research, user registry,
selected preset and machine paths do not transfer. No local chat history is required.

No standalone asset pack exists yet. A Mac agent cannot generate the exact three game
appearances from this catalog alone. It can implement the standalone architecture and
Mac host, but matching those appearances needs the Windows capture/export stage or
independently authored replacements. Record missing assets explicitly.

Keep any future personal asset transfer outside Git (the `macos/private-assets/` folder
is ignored). Do not copy the Windows AssetBundle into a Mac build and assume it loads.
Use portable, explicitly mapped asset files and rebuild any Unity bundles for the target.
Follow the repository rule against committing game DLLs, extracted sprites or caches.
Public distribution of bundled assets is a separate unresolved packaging decision.

## Preferences

Do not overwrite existing Windows PlayerPrefs or saved-pets.v1.json. The catalog deliberately
contains no Windows UUIDs, selected-pet state, game paths or local preferences. On Mac use
versioned application storage and stable catalog keys. If personal preferences are transferred
later, import a sanitized copy and map IDs to catalog keys; do not replace the catalog or
silently reset the user's choices during an upgrade.

## Verification recorded on Windows

- The feeding baseline's full Unity verification and Windows build passed before branching;
  the owner confirmed all three feeding changes looked right.
- This preparation changes no Unity runtime files or project version. Run the portable
  checks above for current handoff validation.
- No macOS compile, arm64 execution, transparency, audio, permission flow or performance
  test has been performed. The agent on the Mac must establish those results.
