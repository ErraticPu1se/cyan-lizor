using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace CyanPreview
{
    [Serializable]
    public sealed class DesktopSettings
    {
        public int schema = 1, controllerPid;
        public string id, cache;
        public float scale = .75f;
        public bool running = true, windowEdges, clickToExplore, roam = true, jumps = true;
        public void Validate()
        {
            if (schema != 1 || controllerPid <= 0 || !ReferenceBridge.TryParseId(id, out _, out _)
                || string.IsNullOrEmpty(cache) || !File.Exists(Path.Combine(cache, "reference.json"))
                || float.IsNaN(scale) || float.IsInfinity(scale) || scale < .5f || scale > 2)
                throw new InvalidDataException("Invalid desktop pet settings.");
        }
    }
    public sealed class DesktopLauncher : IDisposable
    {
        Process process;
        string file => Path.Combine(Application.persistentDataPath, "desktop-" + Process.GetCurrentProcess().Id + ".json");
        string previous;
        bool stopping;
        public bool Active => process != null && !process.HasExited;
        public void UpdateSettings(DesktopSettings settings)
        {
            settings.controllerPid = Process.GetCurrentProcess().Id; settings.running = !stopping; settings.Validate();
            string json = JsonUtility.ToJson(settings);
            if (json == previous) return;
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            string temp = file + ".tmp"; File.WriteAllText(temp, json);
            if (File.Exists(file)) File.Replace(temp, file, null); else File.Move(temp, file);
            previous = json;
        }
        public void Start(DesktopSettings settings)
        {
            if (Active) return;
            process?.Dispose(); stopping = false; UpdateSettings(settings);
            process = Process.Start(new ProcessStartInfo {
                FileName = Path.Combine(Path.GetDirectoryName(Application.dataPath), "CyanLizor.exe"),
                Arguments = "--desktop-pet --desktop-settings \"" + file + "\" -logFile \"" + Path.Combine(Application.persistentDataPath, "desktop-player.log") + "\"",
                UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden
            });
        }
        public void Stop()
        {
            stopping = true;
            if (File.Exists(file))
            {
                var settings = JsonUtility.FromJson<DesktopSettings>(File.ReadAllText(file));
                settings.running = false; UpdateSettings(settings);
            }
        }
        public void Dispose() { Stop(); process?.Dispose(); }
    }
    public sealed class DesktopApp : MonoBehaviour
    {
        public static bool Requested => Array.IndexOf(Environment.GetCommandLineArgs(), "--desktop-pet") >= 0;
        string settingsFile;
        DesktopSettings settings;
        WindowsDesktop native;
        ReferenceContext context;
        ReferenceBridge pet;
        SpriteMeshView view;
        PetMotion motion;
        PetInteraction input;
        DesktopSurfaces surfaces;
        Camera cameraView;
        float refresh;
        bool ready, buttonHeld;
        uint pressTime;
        float holdElapsed;
        IEnumerator Start()
        {
            try
            {
                var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "--desktop-settings");
                if (index < 0 || index + 1 >= args.Length) throw new InvalidDataException("Desktop settings argument missing.");
                settingsFile = args[index + 1]; settings = LoadSettings();
                native = new WindowsDesktop(settings.controllerPid);
                QualitySettings.antiAliasing = 0; Application.runInBackground = true; Application.targetFrameRate = 60;
                cameraView = new GameObject("Desktop camera").AddComponent<Camera>();
                cameraView.orthographic = true; cameraView.clearFlags = CameraClearFlags.SolidColor;
                cameraView.backgroundColor = new Color(1, 0, 1, 1); cameraView.transform.position = new Vector3(0, 0, -10);
                Screen.SetResolution((int)native.WorkArea.width, (int)native.WorkArea.height, FullScreenMode.Windowed);
                context = new ReferenceContext(settings.cache); pet = context.CreatePet();
                view = new SpriteMeshView(transform) { HeadGlowEnabled = false };
                RebuildPet();
            }
            catch (Exception e) { Debug.LogException(e); Application.Quit(); yield break; }
            yield return null; yield return null;
            try
            {
                if (!native.Initialize()) throw new InvalidOperationException("Owned overlay window not found.");
                native.HitTest = cursor => motion.Contains(DesktopCoordinates.ToWorld(cursor, native.WorkArea, settings.scale));
                ready = true; RefreshWindows();
                Debug.Log("DESKTOP_OVERLAY_READY " + native.WorkArea);
            }
            catch (Exception e) { Debug.LogException(e); Application.Quit(); }
        }
        DesktopSettings LoadSettings()
        {
            var value = JsonUtility.FromJson<DesktopSettings>(File.ReadAllText(settingsFile));
            if (value == null) throw new InvalidDataException("Missing desktop settings.");
            value.Validate(); return value;
        }
        void RebuildPet(Vector2? keepPosition = null)
        {
            input?.Cancel();
            var area = native.WorkArea;
            surfaces = new DesktopSurfaces(new Rect(-area.width / (2 * settings.scale), -area.height / (2 * settings.scale), area.width / settings.scale, area.height / settings.scale)) { UseWindowEdges = settings.windowEdges };
            pet.Generate(settings.id); pet.Pose(0, .7f);
            motion = new PetMotion(pet, surfaces) { Roam = settings.roam, AutonomousJumps = settings.jumps };
            if (keepPosition.HasValue)
            {
                var restingPoint = surfaces.Nearest(keepPosition.Value, out var inward) + inward * 14;
                motion.ShiftWithSurface(restingPoint - motion.Position);
            }
            motion.StateChanged += state => Debug.Log("DESKTOP_STATE " + state + " " + motion.Position);
            input = new PetInteraction(motion) { ClickToExplore = settings.clickToExplore };
            cameraView.orthographicSize = area.height / (2 * settings.scale);
        }
        void RefreshWindows()
        {
            var delta = surfaces.UpdateWindows(native.Snapshot(settings.scale), motion.Position,
                motion.State == PetMotionState.Crawling && motion.GripCount > 0 && !input.CapturesPointer);
            if (delta.sqrMagnitude > 0) motion.ShiftWithSurface(delta, resetVelocity: false);
        }

        void Update()
        {
            if (!ready) return;
            try
            {
                refresh -= Time.unscaledDeltaTime;
                if (refresh <= 0)
                {
                    refresh = .25f;
                    DesktopSettings next;
                    try { next = LoadSettings(); } catch (IOException) { next = settings; }
                    if (!next.running || !native.ControllerAlive) { Application.Quit(); return; }
                    bool rebuild = next.id != settings.id || next.scale != settings.scale || next.windowEdges != settings.windowEdges;
                    var keepPosition = motion.Position * settings.scale / next.scale;
                    settings = next;
                    if (native.RefreshWorkArea())
                    {
                        Screen.SetResolution((int)native.WorkArea.width, (int)native.WorkArea.height, FullScreenMode.Windowed); rebuild = true;
                    }
                    if (rebuild) RebuildPet(keepPosition);
                    motion.Roam = settings.roam; motion.AutonomousJumps = settings.jumps;
                    input.ClickToExplore = settings.clickToExplore;
                }
                var cursor = native.Cursor;
                var point = DesktopCoordinates.ToWorld(cursor, native.WorkArea, settings.scale);
                motion.LookAt(point);
                while (native.TryReadButton(out var button))
                {
                    var eventPoint = DesktopCoordinates.ToWorld(button.Position, native.WorkArea, settings.scale);
                    if (button.Down)
                    {
                        buttonHeld = true; pressTime = button.Time; holdElapsed = 0;
                        if (native.WorkArea.Contains(button.Position) && (motion.Contains(eventPoint) || !button.ControllerFocused))
                            input.Press(eventPoint);
                    }
                    else
                    {
                        AdvanceHold(eventPoint, button.Time);
                        input.Release(eventPoint, native.WorkArea.Contains(button.Position)); buttonHeld = false;
                    }
                }
                AdvanceHold(point, unchecked((uint)Environment.TickCount));
                // The native button callback owns pet presses; other application drags pass through.
                native.SetClickThrough(!input.CapturesPointer && (buttonHeld || !motion.Contains(point)));
                RefreshWindows();
                motion.Advance(Time.unscaledDeltaTime); pet.Draw(motion.Interpolation); view.Refresh(pet);
            }
            catch (Exception e) { Debug.LogException(e); Application.Quit(); }
        }
        void AdvanceHold(Vector2 point, uint now)
        {
            if (!buttonHeld) return;
            float elapsed = unchecked(now - pressTime) / 1000f;
            input.Hold(point, Mathf.Max(0, elapsed - holdElapsed)); holdElapsed = elapsed;
        }
        void OnDestroy()
        {
            native?.Dispose(); input?.Cancel(); view?.Dispose(); pet?.Dispose(); context?.Dispose();
        }
    }
}
