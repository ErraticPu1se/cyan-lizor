using System;
using System.Collections;
using System.Collections.Generic;
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
        List<DesktopWindow> previousWindows = new List<DesktopWindow>();
        float refresh;
        bool ready, leftWasDown;
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
                ready = true; leftWasDown = native.LeftDown; RefreshWindows();
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
            input = new PetInteraction(motion) { ClickToExplore = settings.clickToExplore };
            cameraView.orthographicSize = area.height / (2 * settings.scale);
            previousWindows.Clear();
        }
        void RefreshWindows()
        {
            var snapshot = native.Snapshot(settings.scale);
            long attached = motion.State == PetMotionState.Crawling && motion.GripCount > 0
                ? surfaces.WindowAtEdge(motion.Position, out _, out _) : 0;
            if (attached != 0)
            foreach (var old in previousWindows)
            foreach (var next in snapshot)
                if (old.Id == attached && next.Id == attached && Vector2.Distance(old.Bounds.size, next.Bounds.size) < 3)
                {
                    var offset = next.Bounds.position - old.Bounds.position;
                    if (offset.sqrMagnitude > 0) motion.ShiftWithSurface(offset);
                }
            surfaces.UpdateWindows(snapshot); previousWindows = snapshot;
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
                    RefreshWindows();
                }
                var cursor = native.Cursor;
                var point = DesktopCoordinates.ToWorld(cursor, native.WorkArea, settings.scale);
                motion.LookAt(point);
                bool down = native.LeftDown, hit = motion.Contains(point);
                native.SetClickThrough(!hit && motion.State != PetMotionState.Grabbed);
                if (down && !leftWasDown && native.WorkArea.Contains(cursor) && (hit || !native.ControllerFocused)) input.Press(point);
                if (down) input.Hold(point, Time.unscaledDeltaTime);
                if (!down && leftWasDown) input.Release(point, native.WorkArea.Contains(cursor));
                leftWasDown = down;
                motion.Advance(Time.unscaledDeltaTime); pet.Draw(motion.Interpolation); view.Refresh(pet);
            }
            catch (Exception e) { Debug.LogException(e); Application.Quit(); }
        }
        void OnDestroy()
        {
            input?.Cancel(); view?.Dispose(); pet?.Dispose(); context?.Dispose(); native?.Dispose();
        }
    }
}
