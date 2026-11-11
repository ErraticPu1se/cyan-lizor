using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace CyanPreview
{
    public enum DesktopMenuAction { None, Settings, Small, Medium, Large, Roaming, ExploreClicks, Sound, Stop, Climbing, EdgesOnly, EdgesAndBackgrounds, ExtraLarge, Feed }

    [Serializable]
    public sealed class DesktopSettings
    {
        public int schema = 2, controllerPid;
        public ClimbingMode climbingMode = ClimbingMode.EdgesAndBackgrounds;
        public string id, cache;
        public const float DefaultScale = 1.2f;
        public float scale = DefaultScale;
        public bool sound = true;
        public int vocalizeRequest, feedRequest, cancelFeedRequest;
        public bool paused;
        public bool running = true, windowEdges, clickToExplore, roam = true, jumps = true;
        public void ApplyMenu(DesktopMenuAction action)
        {
            switch (action)
            {
                case DesktopMenuAction.Small: scale = 1; break;
                case DesktopMenuAction.Medium: scale = DefaultScale; break;
                case DesktopMenuAction.Large: scale = 1.5f; break;
                case DesktopMenuAction.ExtraLarge: scale = 2; break;
                case DesktopMenuAction.Roaming: roam = !roam; break;
                case DesktopMenuAction.ExploreClicks: clickToExplore = !clickToExplore; break;
                case DesktopMenuAction.Sound: sound = !sound; break;
                case DesktopMenuAction.Climbing: climbingMode = climbingMode == ClimbingMode.EdgesOnly ? ClimbingMode.EdgesAndBackgrounds : ClimbingMode.EdgesOnly; break;
                case DesktopMenuAction.EdgesOnly: climbingMode = ClimbingMode.EdgesOnly; break;
                case DesktopMenuAction.EdgesAndBackgrounds: climbingMode = ClimbingMode.EdgesAndBackgrounds; break;
                default: throw new ArgumentOutOfRangeException(nameof(action));
            }
        }
        public void UpgradeClimbingMode()
        {
            if (schema == 1) { climbingMode = windowEdges ? ClimbingMode.EdgesAndBackgrounds : ClimbingMode.EdgesOnly; schema = 2; }
            if (schema != 2 || !Enum.IsDefined(typeof(ClimbingMode), climbingMode))
                throw new InvalidDataException("Invalid climbing settings.");
        }
        public void Validate()
        {
            UpgradeClimbingMode();
            if (schema != 2 || controllerPid <= 0 || !ReferenceBridge.TryParseId(id, out _, out _)
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
        public string FeedingStatus { get { try { return File.Exists(file+".feeding")?File.ReadAllText(file+".feeding"):""; } catch(IOException) { return ""; } } }
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
        public bool TryReadMenu(out DesktopMenuAction action)
        {
            action = DesktopMenuAction.None;
            var directory = Path.GetDirectoryName(file);
            if (!Directory.Exists(directory)) return false;
            var requests = Directory.GetFiles(directory, Path.GetFileName(file) + ".menu-*");
            requests = Array.FindAll(requests, path => !path.EndsWith(".tmp", StringComparison.Ordinal));
            Array.Sort(requests, StringComparer.Ordinal);
            if (requests.Length == 0) return false;
            string request = requests[0];
            string value = File.ReadAllText(request); File.Delete(request);
            if (!int.TryParse(value, out int number) || !Enum.IsDefined(typeof(DesktopMenuAction), number)) return false;
            action = (DesktopMenuAction)number; return true;
        }
        public void Start(DesktopSettings settings)
        {
            if (Active) return;
            process?.Dispose(); stopping = false; UpdateSettings(settings);
            process = Process.Start(new ProcessStartInfo {
                FileName = Process.GetCurrentProcess().MainModule.FileName,
                Arguments = "--desktop-pet --desktop-settings \"" + file + "\" -logFile \"" + Path.Combine(Application.persistentDataPath, "desktop-player.log") + "\"",
                UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden
            });
        }
        public void Stop()
        {
            stopping = true;
            if (!Active) return;
            if (File.Exists(file))
            {
                var settings = JsonUtility.FromJson<DesktopSettings>(File.ReadAllText(file));
                settings.running = false; UpdateSettings(settings);
            }
        }
        public void Dispose()
        {
            try { Stop(); if(Active && !process.WaitForExit(1500)) process.Kill(); }
            catch(Exception e) { Debug.LogWarning("Desktop pet shutdown: "+e.Message); }
            finally { process?.Dispose(); process=null; }
        }
    }
    public sealed partial class DesktopApp : MonoBehaviour
    {
        public static bool Requested => Array.IndexOf(Environment.GetCommandLineArgs(), "--desktop-pet") >= 0;
        string settingsFile;
        DesktopSettings settings;
        WindowsDesktop native;
        ReferenceContext context;
        ReferenceBridge pet;
        SpriteMeshView view;
        PetMotion motion;
        CyanJumpEffects jumpEffects;
        CyanPetAudio petAudio;
        PetInteraction input;
        DesktopSurfaces surfaces;
        DenRouteSurfaces denSurfaces;
        Camera cameraView;
        DesktopAlphaRenderer desktopRenderer;
        float refresh;
        bool ready, buttonHeld, menuOpen;
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
                QualitySettings.antiAliasing = 0; Application.runInBackground = true; QualitySettings.vSyncCount = 0; Application.targetFrameRate = 60;
                cameraView = new GameObject("Desktop camera").AddComponent<Camera>();
                cameraView.gameObject.AddComponent<AudioListener>();
                cameraView.orthographic = true; cameraView.clearFlags = CameraClearFlags.SolidColor;
                cameraView.enabled = false; cameraView.backgroundColor = Color.clear; cameraView.transform.position = new Vector3(0, 0, -10);
                Screen.SetResolution((int)native.WorkArea.width, (int)native.WorkArea.height, FullScreenMode.Windowed);
                context = new ReferenceContext(settings.cache); pet = context.CreatePet();
                view = new SpriteMeshView(transform) { DesktopTransparency = true };
                RebuildPet();
            }
            catch (Exception e) { Debug.LogException(e); Application.Quit(); yield break; }
            yield return null; yield return null;
            try
            {
                if (!native.Initialize()) throw new InvalidOperationException("Owned overlay window not found.");
                desktopRenderer = new DesktopAlphaRenderer(native.WindowHandle, cameraView, transform);
                native.HitTest = cursor => !settings.paused && !feeding.Frozen && (feeding.Stage==FeedingStage.Placing || motion.Contains(DesktopCoordinates.ToWorld(cursor, native.WorkArea, settings.scale)));
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
            petAudio?.Dispose(); petAudio = null;
            jumpEffects?.Dispose(); jumpEffects = null;
            var area = native.WorkArea;
            surfaces = new DesktopSurfaces(new Rect(-area.width / (2 * settings.scale), -area.height / (2 * settings.scale), area.width / settings.scale, area.height / settings.scale)) { Mode = settings.climbingMode };
            pet.Generate(settings.id); pet.Pose(0, .7f);
            denSurfaces = new DenRouteSurfaces(surfaces,surfaces.Bounds);
            motion = new PetMotion(pet, denSurfaces) { Roam = settings.roam, AutonomousJumps = settings.jumps };
            if (keepPosition.HasValue)
            {
                var restingPoint = surfaces.Nearest(keepPosition.Value, out var inward) + inward * 14;
                motion.ShiftWithSurface(restingPoint - motion.Position);
            }
            motion.StateChanged += state => Debug.Log("DESKTOP_STATE " + state + " " + motion.Position);
            jumpEffects = new CyanJumpEffects(transform, motion, pet.EffectColor, pet.Seed);
            try { petAudio = new CyanPetAudio(transform, context, motion, jumpEffects, pet.Seed) { Enabled = settings.sound && !settings.paused }; }
            catch (Exception e) { Debug.LogWarning("Desktop pet audio unavailable: " + e.Message); }
            input = new PetInteraction(motion) { ClickToExplore = settings.clickToExplore };
            cameraView.orthographicSize = area.height / (2 * settings.scale);
            ResetFeeding();
        }
        void RefreshWindows()
        {
            if(denSurfaces.Active) { surfaces.UpdateWindows(native.Snapshot(settings.scale)); return; }
            var delta = surfaces.UpdateWindows(native.Snapshot(settings.scale), motion.Position,
                motion.State == PetMotionState.Crawling && motion.GripCount > 0 && !input.CapturesPointer);
            if (surfaces.SupportLost) motion.ReleaseSupport();
            else if (delta.sqrMagnitude > 0) { motion.Behavior.Rest(); motion.ShiftWithSurface(delta, resetVelocity: false); }
        }

        void Update()
        {
            if (!ready || menuOpen) return;
            try
            {
                refresh -= Time.unscaledDeltaTime;
                if (refresh <= 0)
                {
                    refresh = .25f;
                    DesktopSettings next;
                    try { next = LoadSettings(); } catch (IOException) { next = settings; }
                    if (!next.running || !native.ControllerAlive) { Application.Quit(); return; }
                    bool rebuild = next.id != settings.id || next.scale != settings.scale;
                    var keepPosition = motion.Position * settings.scale / next.scale;
                    bool modeChanged = next.climbingMode != settings.climbingMode;
                    bool vocalize = next.vocalizeRequest != settings.vocalizeRequest;
                    bool feed=next.feedRequest!=settings.feedRequest, cancelFeed=next.cancelFeedRequest!=settings.cancelFeedRequest;
                    if(next.paused!=settings.paused) { input.Cancel(); native.ClearButtonEvents(); buttonHeld=false; }
                    settings = next;
                    if (native.RefreshWorkArea())
                    {
                        Screen.SetResolution((int)native.WorkArea.width, (int)native.WorkArea.height, FullScreenMode.Windowed); rebuild = true;
                    }
                    if (rebuild) RebuildPet(keepPosition);
                    else if (modeChanged) { surfaces.Mode = settings.climbingMode; motion.RefreshSurfaceSupport(); }
                    motion.Roam = settings.roam; motion.AutonomousJumps = settings.jumps;
                    if(cancelFeed) CancelFeeding(); else if(feed && !settings.paused) BeginFeeding();
                    input.ClickToExplore = settings.clickToExplore;
                    if (petAudio != null) { petAudio.Enabled = settings.sound && !settings.paused; if (vocalize && !settings.paused) petAudio.Vocalize(); }
                }
                if(settings.paused)
                {
                    native.ClearButtonEvents(); buttonHeld=false; native.SetClickThrough(true);
                    RefreshWindows();
                    pet.Draw(motion.Interpolation); view.Refresh(pet); desktopRenderer.Render(native.WorkArea,settings.scale);
                    return;
                }
                var cursor = native.Cursor;
                var point = DesktopCoordinates.ToWorld(cursor, native.WorkArea, settings.scale);
                if(PlaceFoodInput(point)) return;
                if(feeding.Frozen) { native.ClearButtonEvents(); native.SetClickThrough(true); petAudio?.Advance(Time.unscaledDeltaTime); AdvanceFeeding(Time.unscaledDeltaTime); pet.Draw(motion.Interpolation); view.Refresh(pet); desktopRenderer.Render(native.WorkArea,settings.scale); return; }
                motion.ObserveCursor(point);
                if (native.TryReadMenuRequest(out var menuPoint))
                {
                    // A native modal menu pauses this pet only; discard its input on dismissal.
                    menuOpen = true;
                    try
                    {
                        input.Cancel(); buttonHeld = false;
                        var action = native.ShowPetMenu(menuPoint, settings);
                        if (action == DesktopMenuAction.Feed) BeginFeeding();
                        else if (action == DesktopMenuAction.Settings) native.ShowController();
                        else if (action != DesktopMenuAction.None)
                        {
                            string request = settingsFile + ".menu-" + DateTime.UtcNow.Ticks + "-" + Guid.NewGuid().ToString("N");
                            string temp = request + ".tmp";
                            File.WriteAllText(temp, ((int)action).ToString()); File.Move(temp, request);
                        }
                    }
                    finally { menuOpen = false; native.ClearButtonEvents(); }
                    return;
                }
                while (native.TryReadButton(out var button))
                {
                    var eventPoint = DesktopCoordinates.ToWorld(button.Position, native.WorkArea, settings.scale);
                    if (button.Down)
                    {
                        buttonHeld = true; pressTime = button.Time; holdElapsed = 0;
                        if (native.WorkArea.Contains(button.Position) && (motion.Contains(eventPoint) || (!button.ControllerFocused && !surfaces.IsWindowChrome(eventPoint, settings.scale) && !native.IsWindowControl(button.Position))))
                            { if(feeding.Active && !motion.Contains(eventPoint)) continue; input.Press(eventPoint); }
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
                using (DesktopFrameProfile.Measure(DesktopFrameProfile.Stage.Windows)) RefreshWindows();
                if (input.Hover(point, Time.unscaledDeltaTime, buttonHeld, petAudio != null && petAudio.CanPettingCall)) petAudio.PettingCall();
                AdvanceFeeding(Time.unscaledDeltaTime);
                using (DesktopFrameProfile.Measure(DesktopFrameProfile.Stage.Frame))
                {
                    petAudio?.Advance(Time.unscaledDeltaTime);
                    using (DesktopFrameProfile.Measure(DesktopFrameProfile.Stage.Motion)) if(!feeding.Frozen) motion.Advance(Time.unscaledDeltaTime);
                    using (DesktopFrameProfile.Measure(DesktopFrameProfile.Stage.Effects)) jumpEffects.Advance(Time.unscaledDeltaTime);
                    using (DesktopFrameProfile.Measure(DesktopFrameProfile.Stage.Draw)) pet.Draw(motion.Interpolation);
                    using (DesktopFrameProfile.Measure(DesktopFrameProfile.Stage.Mesh)) view.Refresh(pet);
                    desktopRenderer.Render(native.WorkArea, settings.scale);
                }
                DesktopFrameProfile.EndFrame();
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
            foodView?.Dispose(); desktopRenderer?.Dispose(); native?.Dispose(); input?.Cancel(); petAudio?.Dispose(); jumpEffects?.Dispose(); view?.Dispose(); pet?.Dispose(); context?.Dispose();
        }
    }
}
