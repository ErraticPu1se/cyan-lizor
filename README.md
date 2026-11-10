# Cyan Lizor

An unofficial **Windows desktop pet** featuring one cyan lizard from Rain World. Built with Unity, it generates appearance from a creature ID using your own local game installation.

## Current functionality

- **ID-based appearance:** enter a seed number or full `ID.spawner.number`, then select **Apply ID**. Graphics and personality values come from the supported local game build.
- **Desktop movement:** independent roaming, crawling, gripping, and cyan propulsion. The default surface is the primary monitor's work-area frame. An optional mode combines background climbing with visible window-edge attachment; attached pets follow window movement and resizing.
- **Friendly interaction:** cursor attention and optional **Explore where I click** behavior. The pet approaches, investigates, and settles with idle body movement. The cursor is not treated as prey.
- **Grabbing:** hold the pet for **0.1 seconds**, drag, and release to drop. Clicks outside the pet pass through to your applications; click exploration is off by default.
- **Personality-influenced decisions:** utility-based resting, exploration, and invited cursor-following. Energy, sympathy, and bravery influence the desktop adaptation's decisions.
- **Sound and effects:** cyan charge/launch sounds, calls, head bubbles, propulsion particles, and smoothly fading head lighting. Desktop sound can be muted independently of visual effects.
- **Expandable controls:** Your lizard, Movement, Interaction, Sound, and Advanced preview tools. Desktop size has a live slider and Reset button; pet status and Start/Stop remain visible outside the scrolling settings.
- **Preview tools:** separate preview size, view angle, pose animation, frame/background/pole movement tests, decision scores, and comparison screenshots.

## Requirements

- Windows x64; desktop integration currently uses the **primary monitor only**.
- Your own **Rain World v1.11.8** installation. Compatibility is checked against the supported game assembly fingerprint; another build with a similar version label may be rejected.
- Packaged builds do **not** require Unity Hub, Python, or Docker.

## Run a packaged build

1. Extract the **entire ZIP** into a writable folder and open `CyanLizor.exe`.
2. On first launch, enter your Rain World installation folder (the folder containing `RainWorld_Data`) and select **Prepare local game assets**.
3. Enter an appearance ID under **Your lizard** and select **Apply ID**.
4. Select **Start desktop pet**. Keep the control panel open; stopping the pet or closing the panel ends its desktop process.
5. Expand the settings sections to adjust size, roaming, propulsion, window climbing, click exploration, and sound. **Play sample call** is available while the desktop pet is running with sound enabled.

Asset preparation reads your installation without modifying it and stores a local cache under your Windows user profile. Keep all packaged support folders beside the executable. Builds and local game caches are not included in a source checkout; downloading the repository is not the same as downloading a runnable build.

## Current limitations

- One cyan lizard; no multiple-pet or other-species controls.
- No den, feeding/hunting, cursor-petting/purring interaction, tray controls, installer, or automatic updates in this build.
- Background climbing and window-edge climbing share one toggle. Sound has an on/off control; volume and glow-strength sliders are not available.
- Movement and utility decisions are desktop adaptations, not a complete Rain World room simulation or exact AI reproduction. Window-edge navigation is local rather than a full route planner around overlapping applications.
- The preview uses a neutral palette; in-game lighting, mod hooks, and special creature variants are not reproduced. Original graphics calls do not guarantee pixel-identical in-game presentation.
- Secondary monitors, mixed-DPI setups, and performance on other laptops are not verified. Desktop rendering currently uses synchronous GPU-to-CPU readback.
- Desktop mode runs in the Windows player, not inside the Unity Editor.

## Build from source

Use **Unity 2020.3.45f1** with Windows build support and the Mono backend.

1. Clone the repository and add its root folder to Unity Hub.
2. Install the Python dependencies and prepare your local reference:

   ```powershell
   pip install -r tools/requirements.txt
   python tools/prepare_reference.py --game "C:\Program Files (x86)\Steam\steamapps\common\Rain World"
   ```

3. Open the project, select **Cyan Lizor → Create preview scene**, then press Play.
4. Use **Cyan Lizor → Build Windows preview** to produce `Builds/Windows/CyanLizor.exe`.

Run the automated verification suite with `$UnityEditor` set to your Unity editor executable:

```powershell
& $UnityEditor -batchmode -projectPath $PWD -executeMethod CyanPreview.Editor.PreviewBuild.Verify -quit -logFile verify.log
```

To include the self-contained asset importer in a shareable ZIP:

```powershell
pip install pyinstaller==6.22.3
python -m PyInstaller --noconfirm --onefile --name CyanReferenceImporter --collect-all UnityPy --collect-all archspec --collect-all astc_encoder --exclude-module fmod_toolkit --exclude-module pyfmodex --distpath Builds/Importer --workpath Builds/ImporterWork --specpath Builds tools/prepare_reference.py
python tools/collect_notices.py
.\tools\package.ps1
```

The packaging script includes the player, importer, and dependency notices and rejects game assemblies or reference caches. Send the resulting ZIP, not just the executable or your `local-data` folder.

## Troubleshooting

For desktop issues, include your Windows version, screen resolution and scaling, lizard ID, and steps to reproduce the problem. The desktop log is at:

```text
%USERPROFILE%\AppData\LocalLow\ErraticPu1se\Cyan Lizor Preview\desktop-player.log
```

## Credits

Rain World belongs to its respective creators and rights holders. This is an unofficial companion project. Game assemblies, extracted graphics, audio, and decompiled game sources are not committed or distributed with the app. Game resources are read from each user's own supported installation. Packaged importer dependencies include their third-party license notices.
