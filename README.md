
# Cyan Lizor

A Windows desktop-lizard project. The first milestone is a Unity appearance lab
for comparing cyan lizard IDs with Rain World.

## Preview scope

- Seed number or full `ID.spawner.number` input.
- Original local game graphics constructor and draw routines, accessed through a
  small reflection adapter; no game DLLs or sprites are committed or distributed.
- Size slider, view angle, controlled pose motion, and comparison screenshots.
- Neutral preview palette. This is not a complete Rain World room simulation.
- Desktop roaming, window climbing, den placement, and petting/audio come later.

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

- `Assets/CyanPreview/ReferenceBridge.cs`: local-game compatibility boundary.
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
