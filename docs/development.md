# Development

To build the published Windows preview, check out tag `v0.1.0-preview.1` before following these instructions. The release is built from commit `9dc7a56`.

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


## Interaction verification

`PreviewBuild.Verify` includes contact timing, cooldown/mute, the local cyan love clip, independent duplicate-ID pets, flexible dragging, extreme cursor movement, limb reach, bounded release, window movement/resizing, and support loss. Native window-control hit-testing still needs hands-on checks across applications. Custom title bars use a conservative 40-screen-pixel top strip to avoid interpreting close/drag clicks as exploration. Only one desktop pet and cyan are currently exposed in the app.
