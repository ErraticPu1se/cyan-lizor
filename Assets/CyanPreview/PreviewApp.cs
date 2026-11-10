using System;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace CyanPreview
{
    public sealed class PreviewApp : MonoBehaviour
    {
        ReferenceContext context;
        ReferenceBridge reference;
        SpriteMeshView view;
        Camera previewCamera;
        string id = "12345", cache, status = "Choose your prepared local reference folder.", error;
        float zoom = 1f, depth = .7f, phase;
        bool animate, locomotion;
        PetMotion motion;
        PetInteraction interaction;
        CyanJumpEffects jumpEffects;
        CyanPetAudio petAudio;
        bool soundEnabled = true, showDecisions;
        readonly DesktopLauncher desktop = new DesktopLauncher();
        bool desktopWindowEdges, desktopClicks;
        bool desktopSounds = true, desktopRoam = true;
        bool lizardOpen = true, movementOpen, interactionOpen, soundOpen, advancedOpen;
        int desktopVocalizeRequest;
        float desktopScale = .75f;
        string audioError;
        bool clickToExplore = true, autonomousJumps = true;
        Vector2 panelScroll;
        int surfaceMode;
        readonly FrameSurfaces movementFrame = new FrameSurfaces(new Rect(-360, -180, 720, 360));
        Rect MovementScreenRect()
        {
            var a = previewCamera.WorldToScreenPoint(new Vector3(movementFrame.Bounds.xMin * zoom, movementFrame.Bounds.yMin * zoom, 0));
            var b = previewCamera.WorldToScreenPoint(new Vector3(movementFrame.Bounds.xMax * zoom, movementFrame.Bounds.yMax * zoom, 0));
            return new Rect(a.x, Screen.height - b.y, b.x - a.x, b.y - a.y);
        }
        void BeginMotion()
        {
            reference.Pose(0, surfaceMode == 1 ? 0 : .7f);
            interaction?.Cancel();
            petAudio?.Dispose(); petAudio = null;
            jumpEffects?.Dispose();
            IPetSurfaces surfaces = surfaceMode == 1 ? (IPetSurfaces)new BackgroundSurfaces(movementFrame.Bounds)
                : surfaceMode == 2 ? (IPetSurfaces)new PoleSurfaces(movementFrame.Bounds, 0) : movementFrame;
            motion = new PetMotion(reference, surfaces) { AutonomousJumps = autonomousJumps && surfaceMode != 2 };
            jumpEffects = new CyanJumpEffects(transform, motion, reference.EffectColor, reference.Seed);
            jumpEffects.SetScale(zoom);
            audioError = null;
            try { petAudio = new CyanPetAudio(transform, context, motion, jumpEffects, reference.Seed) { Enabled = soundEnabled }; }
            catch (Exception e) { audioError = e.Message; Debug.LogWarning("Pet audio unavailable: " + e.Message); }
            interaction = new PetInteraction(motion) { ClickToExplore = clickToExplore };
        }
        string gameFolder = @"C:\Program Files (x86)\Steam\steamapps\common\Rain World";
        Process importer;
        Task<string> importerOutput, importerError;
        GUIStyle title, subtitle, small;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (DesktopApp.Requested) { new GameObject("Desktop pet").AddComponent<DesktopApp>(); return; }
            if (FindObjectOfType<PreviewApp>() == null) new GameObject("Cyan Lizor Preview").AddComponent<PreviewApp>();
        }
        void Start()
        {
            Application.targetFrameRate = 60;
            // A child overlay can leave Unity's saved window size smaller than this control panel.
            if (!Application.isEditor && (Screen.width < 900 || Screen.height < 650))
                Screen.SetResolution(Mathf.Min(1440, Display.main.systemWidth), Mathf.Min(900, Display.main.systemHeight - 48), FullScreenMode.Windowed);
            var backgroundCamera = new GameObject("Background camera").AddComponent<Camera>();
            backgroundCamera.depth = -10;
            backgroundCamera.cullingMask = 0;
            backgroundCamera.clearFlags = CameraClearFlags.SolidColor;
            backgroundCamera.backgroundColor = new Color(.10f, .12f, .14f);
            var cameraObject = new GameObject("Preview camera");
            previewCamera = cameraObject.AddComponent<Camera>();
            previewCamera.orthographic = true;
            if (FindObjectOfType<AudioListener>() == null) cameraObject.AddComponent<AudioListener>();
            previewCamera.orthographicSize = 130;
            previewCamera.transform.position = new Vector3(-40, 0, -10);
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(.12f, .16f, .19f);
            var besideApp = Path.Combine(Path.GetDirectoryName(Application.dataPath), "local-data");
            cache = PlayerPrefs.GetString("ReferenceCache", besideApp);
            id = PlayerPrefs.GetString("LastId", "12345");
            view = new SpriteMeshView(transform);
            if (File.Exists(Path.Combine(cache, "reference.json"))) LoadReference();
        }
        void LoadReference()
        {
            try
            {
                if (reference != null) throw new InvalidOperationException("Restart the app to change reference installations.");
                context = new ReferenceContext(cache);
                reference = context.CreatePet();
                PlayerPrefs.SetString("ReferenceCache", cache);
                Generate();
            }
            catch (Exception e) { Fail(e); }
        }
        void Generate()
        {
            try
            {
                reference.Generate(id);
                phase = 0;
                reference.Pose(phase, depth);
                if (locomotion) BeginMotion();
                reference.Draw();
                view.Refresh(reference);
                view.SetScale(zoom);
                status = reference.Identity + "  ·  " + reference.Species.DisplayName + "  ·  " + reference.Manifest.gameVersion;
                error = null;
                PlayerPrefs.SetString("LastId", id);
                PlayerPrefs.Save();
            }
            catch (Exception e) { Fail(e); }
        }
        void Fail(Exception e)
        {
            error = e.Message;
            Debug.LogException(e);
        }
        DesktopSettings DesktopConfiguration() => new DesktopSettings { id = reference.Identity, cache = cache, scale = desktopScale,
            windowEdges = desktopWindowEdges, clickToExplore = desktopClicks, sound = desktopSounds, vocalizeRequest = desktopVocalizeRequest, roam = desktopRoam, jumps = autonomousJumps };
        void Update()
        {
            if (desktop.Active)
            {
                try { desktop.UpdateSettings(DesktopConfiguration()); }
                catch (Exception e) { error = "Desktop settings: " + e.Message; }
            }
            if (importer != null && importer.HasExited && importerOutput.IsCompleted && importerError.IsCompleted)
            {
                if (importer.ExitCode == 0) { importer.Dispose(); importer = null; LoadReference(); }
                else
                {
                    error = "Reference import failed: " + importerError.Result;
                    importer.Dispose(); importer = null;
                }
            }
            if (previewCamera != null)
            {
                float left = (Mathf.Clamp(Screen.width * .26f, 260, 360) + 40) / Screen.width;
                previewCamera.rect = new Rect(left, .12f, 1 - left, .75f);
                previewCamera.orthographicSize = locomotion ? Mathf.Max(205, 390 / previewCamera.aspect) * zoom : 130;
                previewCamera.transform.position = new Vector3(locomotion ? 0 : -40, 0, -10);
            }
            if (reference == null || reference.Graphics == null || error != null || (!animate && !locomotion)) return;
            try
            {
                if (locomotion)
                {
                    var mouse = Input.mousePosition;
                    var point = (Vector2)previewCamera.ScreenToWorldPoint(mouse) / zoom;
                    bool inside = previewCamera.pixelRect.Contains(mouse);
                    motion.ObserveCursor(inside ? (Vector2?)point : null);
                    if (inside && Input.GetMouseButtonDown(0)) interaction.Press(point);
                    if (Input.GetMouseButton(0)) interaction.Hold(point, Time.deltaTime);
                    if (Input.GetMouseButtonUp(0)) interaction.Release(point, inside);
                    if (inside && Input.GetMouseButtonDown(1)) motion.JumpTo(point);
                    if (petAudio != null) petAudio.Enabled = soundEnabled && !desktop.Active;
                    petAudio?.Advance(Time.deltaTime);
                    motion.Advance(Time.deltaTime);
                    jumpEffects.Advance(Time.deltaTime);
                }
                else
                {
                    phase += Time.deltaTime * 1.5f;
                    reference.Pose(phase, depth);
                }
                reference.Draw(locomotion ? motion.Interpolation : 1f);
                view.Refresh(reference);
            }
            catch (Exception e) { animate = locomotion = false; Fail(e); }
        }
        void OnApplicationFocus(bool focused) { if (!focused) interaction?.Cancel(); }
        void OnGUI()
        {
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return && GUI.GetNameOfFocusedControl() == "CreatureId" && reference != null)
            { Generate(); Event.current.Use(); }
            if (title == null)
            {
                title = new GUIStyle(GUI.skin.label) { fontSize = 27, fontStyle = FontStyle.Bold };
                subtitle = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
                small = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
                title.normal.textColor = new Color(.35f, .95f, .88f);
            }
            GUI.Label(new Rect(24, 18, Screen.width - 48, 40), "CYAN LIZOR", title);
            GUI.Label(new Rect(25, 59, Screen.width - 50, 28), "Your cyan companion · desktop settings & preview", subtitle);
            float panelWidth = Mathf.Clamp(Screen.width * .26f, 260, 360);
            GUILayout.BeginArea(new Rect(24, 111, panelWidth, Mathf.Max(180, Screen.height - 250)), GUI.skin.box);
            panelScroll = GUILayout.BeginScrollView(panelScroll);
            if (reference == null)
                GUILayout.Label("Prepare your game assets below to unlock your lizard.", small);
            if (Section("Your lizard", ref lizardOpen))
            {
                GUILayout.Space(12);
                GUILayout.Label("Appearance ID", subtitle);
                GUI.SetNextControlName("CreatureId");
                id = GUILayout.TextField(id, 64, GUILayout.Height(32));
                GUILayout.Label("Seed number or ID.spawner.number", small);
                GUI.enabled = reference != null;
                if (GUILayout.Button("Apply ID", GUILayout.Height(34))) Generate();
                GUI.enabled = true;

                GUILayout.Space(10);
                GUILayout.Label("Desktop size  " + desktopScale.ToString("0.00") + "×", subtitle);
                desktopScale = GUILayout.HorizontalSlider(desktopScale, .5f, 2f);
                if (GUILayout.Button("Reset size")) desktopScale = .75f;
                GUILayout.Label("Changes the small pet on your desktop.", small);
            }
            if (Section("Movement", ref movementOpen))
            {
                desktopRoam = GUILayout.Toggle(desktopRoam, " Roam independently");
                autonomousJumps = GUILayout.Toggle(autonomousJumps, " Allow cyan propulsion");
                if (motion != null) motion.AutonomousJumps = autonomousJumps && surfaceMode != 2;
                desktopWindowEdges = GUILayout.Toggle(desktopWindowEdges, " Climb backgrounds and window edges");
                GUILayout.Label(desktopWindowEdges ? "Climbs freely and grips visible window edges." : "Stays on the monitor frame and avoids windows.", small);
            }
            if (Section("Interaction", ref interactionOpen))
            {
                desktopClicks = GUILayout.Toggle(desktopClicks, " Explore where I click");
                GUILayout.Label("Clicks within about three body lengths invite a direct hop. Farther clicks invite longer hops that stop short of your cursor.", small);
                GUILayout.Label("Hold the pet for 0.1 seconds to grab it, then release to drop. Move nearby to get its attention. Clicks still reach your apps.", small);
            }
            if (Section("Sound", ref soundOpen))
            {
                desktopSounds = GUILayout.Toggle(desktopSounds, " Desktop sounds");
                GUI.enabled = desktop.Active && desktopSounds;
                if (GUILayout.Button("Play sample call", GUILayout.Height(28))) desktopVocalizeRequest++;
                GUI.enabled = true;
                if (!desktop.Active) GUILayout.Label("Start your desktop pet to hear a sample call.", small);
                else if (!desktopSounds) GUILayout.Label("Enable sounds to hear a sample call.", small);
                GUILayout.Label("Head glow and propulsion effects stay active when sound is muted.", small);
            }
            if (Section("Advanced preview tools", ref advancedOpen))
            {
                GUILayout.Label("These controls adjust the large preview, unless stated otherwise.", small);
                GUILayout.Space(20);
                GUILayout.Label("Preview size  " + zoom.ToString("0.0") + "×", subtitle);
                float nextZoom = GUILayout.HorizontalSlider(zoom, .75f, 4f);
                if (nextZoom != zoom) { zoom = nextZoom; view?.SetScale(zoom); jumpEffects?.SetScale(zoom); }
                GUILayout.Space(15);
                GUI.enabled = !locomotion;
                GUILayout.Label("VIEW ANGLE", subtitle);
                float nextDepth = GUILayout.HorizontalSlider(depth, -.95f, .95f);
                if (nextDepth != depth && reference != null && error == null)
                {
                    depth = nextDepth;
                    try { reference.Pose(phase, depth); reference.Draw(); view.Refresh(reference); }
                    catch (Exception e) { Fail(e); }
                }
                animate = GUILayout.Toggle(animate, " Gentle pose motion");
                GUI.enabled = reference != null && error == null;
                bool nextMotion = GUILayout.Toggle(locomotion, " Crawling / gripping / propulsion");
                if (nextMotion != locomotion)
                {
                    locomotion = nextMotion;
                    try
                    {
                        if (locomotion) BeginMotion();
                        else { petAudio?.Dispose(); petAudio = null; jumpEffects?.Dispose(); jumpEffects = null; reference.Pose(phase, depth); reference.Draw(); view.Refresh(reference); }
                    }
                    catch (Exception e) { locomotion = false; Fail(e); }
                }
                if (locomotion)
                {
                    soundEnabled = GUILayout.Toggle(soundEnabled, " Pet sounds");
                    if (petAudio != null) petAudio.Enabled = soundEnabled && !desktop.Active;
                    if (GUILayout.Button("Vocalize")) petAudio?.Vocalize();
                    if (audioError != null) GUILayout.Label("Sound unavailable: " + audioError, small);
                    motion.Roam = GUILayout.Toggle(motion.Roam, " Roam after resting");
                    clickToExplore = GUILayout.Toggle(clickToExplore, " Click to explore");
                    interaction.ClickToExplore = clickToExplore;
                    motion.AutonomousJumps = autonomousJumps && surfaceMode != 2;
                    GUILayout.Label("MOVEMENT TEST SURFACE", small);
                    int nextSurface = GUILayout.SelectionGrid(surfaceMode, new[] { "Frame", "Background", "Pole" }, 3);
                    if (nextSurface != surfaceMode) { surfaceMode = nextSurface; BeginMotion(); }
                    if (surfaceMode == 2) GUILayout.Label("Pole crawling test; jumping between poles comes later.", small);
                    if (GUILayout.Button("Propel toward centre")) motion.JumpTo(new Vector2(-40, 15));
                    showDecisions = GUILayout.Toggle(showDecisions, " Show decision scores");
                    if (showDecisions) GUILayout.Label(motion.Behavior.DecisionSummary, small);
                    GUILayout.Label(motion.Behavior.Intent + " / " + motion.State + " · " + motion.GripCount + " feet gripping", small);
                    GUILayout.Label("Idle: watches cursor. Hold on pet for 0.1s to drag. Release to drop. Click elsewhere to explore. Right click to jump.", small);
                }

                GUI.enabled = true;
                if (reference != null)
                {
                    GUILayout.Label(reference.Summary ?? "", small);
                    GUILayout.Label("Tamed companion / personality from ID", small);
                    GUILayout.Label(reference.Personality.ToString(), small);
                }
                if (GUILayout.Button("Save comparison image")) StartCoroutine(Capture());
            }
            GUI.enabled = true;
            if (reference == null)
            {
                GUILayout.Label("RAIN WORLD INSTALLATION", small);
                gameFolder = GUILayout.TextField(gameFolder);
                GUI.enabled = importer == null;
                if (GUILayout.Button("Prepare local game assets")) ImportReference();
                GUI.enabled = true;
                if (importer != null) GUILayout.Label("Preparing your local reference…", small);
                GUILayout.Label("Prepared reference folder", small);
                cache = GUILayout.TextField(cache ?? "");
                if (GUILayout.Button("Load prepared reference")) LoadReference();
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            GUILayout.BeginArea(new Rect(24, Screen.height - 125, panelWidth, 110), GUI.skin.box);
            GUILayout.Label(desktop.Active ? "Desktop pet is running" : "Desktop pet is stopped", subtitle);
            GUI.enabled = desktop.Active || (reference != null && reference.Graphics != null && !Application.isEditor);
            if (GUILayout.Button(desktop.Active ? "Stop desktop pet" : "Start desktop pet", GUILayout.Height(32)))
            {
                if (desktop.Active) desktop.Stop();
                else try { desktop.Start(DesktopConfiguration()); } catch (Exception e) { Fail(e); }
            }
            GUI.enabled = true;
            GUILayout.Label(Application.isEditor ? "Desktop mode needs the Windows build." : reference == null
                ? "Prepare your game assets to start." : "Keep this panel open while your pet is running.", small);
            GUILayout.EndArea();
            if (locomotion && previewCamera != null)
            {
                var frame = MovementScreenRect();
                var oldColor = GUI.color; GUI.color = new Color(.2f, .8f, .75f, .5f);
                GUI.DrawTexture(new Rect(frame.x, frame.y, frame.width, 2), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(frame.x, frame.yMax - 2, frame.width, 2), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(frame.x, frame.y, 2, frame.height), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(frame.xMax - 2, frame.y, 2, frame.height), Texture2D.whiteTexture);
                if (surfaceMode == 2) GUI.DrawTexture(new Rect(frame.center.x - 1, frame.y, 2, frame.height), Texture2D.whiteTexture);
                GUI.color = oldColor;
            }
            GUI.Label(new Rect(panelWidth + 40, Screen.height - 85, Screen.width - panelWidth - 60, 25), status, subtitle);
            GUI.Label(new Rect(panelWidth + 40, Screen.height - 58, Screen.width - panelWidth - 60, 45),
                "Local game appearance · desktop overlay prototype available · movement is a host adaptation", small);
            if (!string.IsNullOrEmpty(error))
                GUI.Box(new Rect(panelWidth + 40, 115, Screen.width - panelWidth - 60, 220), error);
        }
        bool Section(string label, ref bool expanded)
        {
            GUILayout.Space(8);
            if (GUILayout.Button((expanded ? "−  " : "+  ") + label, GUILayout.Height(32))) expanded = !expanded;
            return expanded;
        }
        void ImportReference()
        {
            try
            {
                if (!File.Exists(Path.Combine(gameFolder, "RainWorld_Data", "Managed", "Assembly-CSharp.dll")))
                    throw new DirectoryNotFoundException("Choose the Rain World installation folder.");
                string exe = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Tools", "CyanReferenceImporter.exe");
                if (!File.Exists(exe)) throw new FileNotFoundException("Importer is not packaged here. In development, run tools/prepare_reference.py; see README.");
                cache = Path.Combine(Application.persistentDataPath, "local-data");
                importer = Process.Start(new ProcessStartInfo {
                    FileName = exe,
                    Arguments = "--game \"" + Path.GetFullPath(gameFolder).TrimEnd('\\') + "\" --output \"" + cache.TrimEnd('\\') + "\"",
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true
                });
                importerOutput = importer.StandardOutput.ReadToEndAsync();
                importerError = importer.StandardError.ReadToEndAsync();
                error = null;
            }
            catch (Exception e) { importer?.Dispose(); importer = null; Fail(e); }
        }
        System.Collections.IEnumerator Capture()
        {
            yield return new WaitForEndOfFrame();
            var image = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            image.Apply();
            var folder = Path.Combine(Application.persistentDataPath, "Comparisons");
            Directory.CreateDirectory(folder);
            var file = Path.Combine(folder, "cyan-" + (reference?.Seed ?? 0) + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".png");
            File.WriteAllBytes(file, image.EncodeToPNG());
            Destroy(image);
            status = "Saved: " + file;
        }
        void OnDestroy() { desktop.Dispose(); petAudio?.Dispose(); jumpEffects?.Dispose(); view?.Dispose(); reference?.Dispose(); context?.Dispose(); }
    }
}
