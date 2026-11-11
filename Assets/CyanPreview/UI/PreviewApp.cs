using System;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace CyanPreview
{
    public sealed partial class PreviewApp : MonoBehaviour
    {
        WindowsTray tray;
        SingleInstance instance;
        readonly TrayState trayState = new TrayState();
        Camera backgroundCamera;
        string trayWarning;
        bool migrationFailed;
        ReferenceContext context;
        ReferenceBridge reference;
        SpriteMeshView view;
        Camera previewCamera;
        RenderTexture previewTexture;
        PanelTheme theme;
        readonly DesktopLauncher desktop = new DesktopLauncher();
        string id="12345", cache, status="Choose your prepared local reference folder.", error;
        string gameFolder="", appliedId, setupMessage, preferenceWarning, savedPreferences;
        const string PreferencesKey = "CompanionPreferences.v1";
        float preferenceClock;
        Task<System.Collections.Generic.List<string>> detection;
        Task<string> folderPicker;
        string detectionInput;
        bool initialized;
        string StartBlocker => GameInstallation.StartBlocker(Ready, importer!=null, Application.isEditor, !string.IsNullOrEmpty(error))
            ?? (id!=appliedId ? "Apply your appearance ID before starting the pet." : null);
        Process importer;
        Task<string> importerOutput, importerError;
        float phase, depth=.7f, desktopScale=DesktopSettings.DefaultScale, previewClock;
        bool animate=true, desktopClicks, desktopSounds=true, desktopRoam=true, autonomousJumps=true;
        bool movementOpen=true, interactionOpen, soundOpen, advancedOpen;
        int desktopVocalizeRequest;
        ClimbingMode desktopClimbingMode=ClimbingMode.EdgesAndBackgrounds;
        Vector2 panelScroll;
        bool Ready => reference != null && reference.Graphics != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (DesktopApp.Requested) { new GameObject("Desktop pet").AddComponent<DesktopApp>(); return; }
            if (FindObjectOfType<PreviewApp>() == null) new GameObject("Cyan Lizard Preview").AddComponent<PreviewApp>();
        }
        void Start()
        {
            if(!Application.isEditor)
            {
                instance=new SingleInstance();
                if(!instance.IsPrimary) { enabled=false; WindowsTray.RestoreExisting(); Application.Quit(); return; }
                try
                {
                    PreferenceMigration.Apply(PlayerPrefs.HasKey,PlayerPrefs.SetString,LegacyPreferences.Read);
                    PlayerPrefs.Save();
                }
                catch(Exception e) { migrationFailed=true; error="Your existing settings could not be imported. They have not been changed. "+e.Message; return; }
            }
            Application.runInBackground=true;
            QualitySettings.vSyncCount=0; Application.targetFrameRate=60;
            if (!Application.isEditor)
                Screen.SetResolution(Mathf.Min(540,Display.main.systemWidth),Mathf.Min(900,Display.main.systemHeight-80),FullScreenMode.Windowed);
            backgroundCamera=new GameObject("Background camera").AddComponent<Camera>();
            backgroundCamera.depth=-10; backgroundCamera.cullingMask=0;
            backgroundCamera.clearFlags=CameraClearFlags.SolidColor; backgroundCamera.backgroundColor=PanelTheme.Background;
            previewCamera=new GameObject("Preview camera").AddComponent<Camera>();
            previewCamera.orthographic=true; previewCamera.orthographicSize=48;
            previewCamera.transform.position=new Vector3(-35,0,-10);
            previewCamera.clearFlags=CameraClearFlags.SolidColor;
            previewCamera.backgroundColor=new Color(.10f,.14f,.165f);
            previewTexture=new RenderTexture(768,240,16,RenderTextureFormat.ARGB32) {name="Companion preview",filterMode=FilterMode.Bilinear};
            previewTexture.Create(); previewCamera.targetTexture=previewTexture;
            cache=PlayerPrefs.GetString("ReferenceCache",Path.Combine(Path.GetDirectoryName(Application.dataPath),"local-data"));
            id=PlayerPrefs.GetString("LastId","12345");
            var preferences=CompanionPreferences.Read(PlayerPrefs.GetString(PreferencesKey,""),out preferenceWarning);
            gameFolder=preferences.gameFolder; desktopScale=preferences.scale; desktopClimbingMode=preferences.climbingMode;
            desktopClicks=preferences.clickToExplore; desktopSounds=preferences.sound; desktopRoam=preferences.roam; autonomousJumps=preferences.jumps;
            InitializePresets(preferences);
            savedPreferences=JsonUtility.ToJson(preferences); initialized=true;
            DetectInstallation();
            view=new SpriteMeshView(transform);
            if(File.Exists(Path.Combine(cache,"reference.json"))) LoadReference();
            if(!Application.isEditor) StartCoroutine(InitializeTray());
        }
        void LoadReference()
        {
            try
            {
                if(reference != null) throw new InvalidOperationException("Restart the app to change reference installations.");
                context?.Dispose(); context=null; context=new ReferenceContext(cache); reference=context.CreatePet();
                PlayerPrefs.SetString("ReferenceCache",cache); Generate();
            }
            catch(Exception e) { Fail(e); }
        }
        void Generate()
        {
            try
            {
                reference.Generate(id); phase=0; RefreshPreview();
                status=reference.Identity+"  ·  "+reference.Species.DisplayName+"  ·  "+reference.Manifest.gameVersion;
                error=null; appliedId=id; PlayerPrefs.SetString("LastId",id); PlayerPrefs.Save();
            }
            catch(Exception e) { Fail(e); }
        }
        void RefreshPreview()
        {
            reference.Pose(phase,depth); reference.Draw(); view.Refresh(reference);
        }
        void Fail(Exception e) { error=e.Message; Debug.LogException(e); }
        DesktopSettings DesktopConfiguration() => new DesktopSettings {id=reference.Identity,cache=cache,scale=desktopScale,
            paused=trayState.Paused,climbingMode=desktopClimbingMode,clickToExplore=desktopClicks,sound=desktopSounds,vocalizeRequest=desktopVocalizeRequest,roam=desktopRoam,jumps=autonomousJumps};
        System.Collections.IEnumerator InitializeTray()
        {
            yield return null; yield return null;
            try
            {
                tray=new WindowsTray();
                if(!tray.Available) trayWarning="The tray is unavailable. Closing this window will quit Lizor.";
            }
            catch(Exception e) { trayWarning="The tray is unavailable: "+e.Message; }
        }
        void QuitApp()
        {
            if(trayState.Quitting) return;
            trayState.Quit(); if(tray!=null) tray.AllowExit=true;
            SavePreferences(); Application.Quit();
        }
        void UpdateTray()
        {
            trayState.Sync(desktop.Active);
            if(tray==null) return;
            tray.UpdateState(desktop.Active,trayState.Paused,desktopSounds);
            while(tray.TryRead(out var action))
            {
                switch(action)
                {
                    case TrayAction.Settings: tray.ShowSettings(); break;
                    case TrayAction.PauseResume: trayState.TogglePause(desktop.Active); break;
                    case TrayAction.Sound: desktopSounds=!desktopSounds; SavePreferences(); break;
                    case TrayAction.StopPet: desktop.Stop(); trayState.Sync(false); break;
                    case TrayAction.Quit: QuitApp(); break;
                    case TrayAction.Hidden:
                        SavePreferences();
                        if(PlayerPrefs.GetInt("Lizor.TrayHintShown",0)==0 && tray.NotifyHidden())
                        { PlayerPrefs.SetInt("Lizor.TrayHintShown",1); PlayerPrefs.Save(); }
                        break;
                }
            }
            bool visible=tray.PanelVisible;
            if(previewCamera!=null) previewCamera.enabled=visible;
            if(backgroundCamera!=null) backgroundCamera.enabled=visible;
            Application.targetFrameRate=visible?60:15;
        }
        void DetectInstallation()
        {
            if(detection!=null) return;
            detectionInput=gameFolder; string saved=gameFolder;
            setupMessage="Looking in Steam libraries…";
            detection=Task.Run(()=>GameInstallation.Detect(saved));
        }
        void SavePreferences()
        {
            if(!initialized) return;
            var preferences=new CompanionPreferences { gameFolder=gameFolder, scale=desktopScale, climbingMode=desktopClimbingMode,
                sound=desktopSounds, roam=desktopRoam, jumps=autonomousJumps, clickToExplore=desktopClicks };
            string json=JsonUtility.ToJson(preferences);
            try { SavePreset(); } catch(Exception e) { presetWarning="Could not save preset: "+e.Message; }
            if(json==savedPreferences) return;
            try { PlayerPrefs.SetString(PreferencesKey,json); PlayerPrefs.Save(); savedPreferences=json; }
            catch(Exception e) { preferenceWarning="Could not save preferences: "+e.Message; }
        }
        void Update()
        {
            if(migrationFailed) return;
            UpdateTray();
            if(trayState.Quitting) return;
            if(detection!=null && detection.IsCompleted)
            {
                if(detection.IsFaulted) setupMessage="Automatic detection failed. Browse to your game folder.";
                else if(detection.Result.Count==0) setupMessage="No installation found in Steam libraries. Use Browse for another location.";
                else
                {
                    if(gameFolder==detectionInput) gameFolder=detection.Result[0];
                    setupMessage=detection.Result.Count>1?"Multiple installations found. Check the selected folder or use Browse.":"Game folder found. Prepare assets below to check version compatibility.";
                }
                detection=null;
            }
            if(folderPicker!=null && folderPicker.IsCompleted)
            {
                if(folderPicker.IsFaulted) error="Folder picker: "+folderPicker.Exception.GetBaseException().Message;
                else if(folderPicker.Result!=null) { gameFolder=folderPicker.Result; setupMessage=null; }
                folderPicker=null;
            }
            preferenceClock+=Time.unscaledDeltaTime;
            if(preferenceClock>=.5f) { preferenceClock=0; SavePreferences(); }
            if(desktop.Active)
            {
                try
                {
                    while(desktop.TryReadMenu(out var action))
                    {
                        if(action==DesktopMenuAction.Stop) { desktop.Stop(); break; }
                        var updated=DesktopConfiguration(); updated.ApplyMenu(action);
                        desktopScale=updated.scale; desktopRoam=updated.roam; desktopClicks=updated.clickToExplore;
                        desktopSounds=updated.sound; desktopClimbingMode=updated.climbingMode;
                    }
                    desktop.UpdateSettings(DesktopConfiguration());
                }
                catch(Exception e) { error="Desktop settings: "+e.Message; }
            }
            if(importer != null && importer.HasExited && importerOutput.IsCompleted && importerError.IsCompleted)
            {
                if(importer.ExitCode==0) { importer.Dispose(); importer=null; LoadReference(); }
                else { error="Reference import failed: "+importerError.Result; importer.Dispose(); importer=null; }
            }
            if(!Ready || !animate || trayState.Paused || (tray!=null && !tray.PanelVisible)) return;
            previewClock+=Time.unscaledDeltaTime;
            if(previewClock<1f/30f) return;
            phase+=Mathf.Min(previewClock,.1f)*.8f; previewClock=0;
            try { RefreshPreview(); }
            catch(Exception e) { animate=false; Fail(e); }
        }
        void OnGUI()
        {
            if(migrationFailed)
            {
                GUILayout.Label(error);
                if(GUILayout.Button("Quit Lizor")) Application.Quit();
                return;
            }
            if(tray!=null && !tray.PanelVisible) return;
            if(theme==null) theme=new PanelTheme(GUI.skin);
            var previousSkin=GUI.skin; GUI.skin=theme.Skin;
            try { DrawPanel(); }
            finally { GUI.skin=previousSkin; GUI.enabled=true; }
        }
        void DrawPanel()
        {
            if(Event.current.type==EventType.KeyDown && Event.current.keyCode==KeyCode.Return && GUI.GetNameOfFocusedControl()=="CreatureId" && reference != null)
            { Generate(); Event.current.Use(); }
            float width=Mathf.Min(580,Screen.width-32), left=(Screen.width-width)/2;
            GUI.Label(new Rect(left,16,width-110,35),"LIZOR",theme.Title);
            GUI.Label(new Rect(left,52,width,22),"desktop settings & preview",theme.Small);
            GUI.Label(new Rect(left+width-96,23,96,25),desktop.Active?(trayState.Paused?"PAUSED":"RUNNING"):"STOPPED",theme.Badge);
            float heroHeight=Mathf.Clamp(Screen.height*.16f,90,144);
            var hero=new Rect(left,83,width,heroHeight);
            GUI.Box(hero,GUIContent.none,theme.Card);
            if(previewTexture!=null) GUI.DrawTexture(new Rect(hero.x+2,hero.y+2,hero.width-4,hero.height-4),previewTexture,ScaleMode.ScaleToFit,false);
            if(!Ready) GUI.Label(new Rect(hero.x+16,hero.y+hero.height/2-12,hero.width-32,45),"Prepare your game assets below to unlock your lizard.",theme.Small);
            float top=hero.yMax+10, footer=Screen.height-146;
            GUILayout.BeginArea(new Rect(left,top,width,Mathf.Max(40,footer-top-8)));
            panelScroll=GUILayout.BeginScrollView(panelScroll,false,false,GUIStyle.none,GUI.skin.verticalScrollbar);
            if(!string.IsNullOrEmpty(error))
            {
                GUILayout.BeginVertical(theme.Error); GUILayout.Label(error,theme.Error);
                if(GUILayout.Button("Dismiss")) error=null;
                GUILayout.EndVertical();
            }
            if(!string.IsNullOrEmpty(trayWarning)) GUILayout.Label(trayWarning,theme.Small);
            if(!string.IsNullOrEmpty(preferenceWarning)) GUILayout.Label(preferenceWarning,theme.Small);
            if(!Ready) DrawSetup();
            DrawPresets();
            GUILayout.BeginVertical(theme.Card);
            GUILayout.Label("Your lizard",theme.Heading);
            GUILayout.Space(8);
            GUILayout.Label("Appearance ID",theme.Small);
            GUILayout.BeginHorizontal(); GUI.SetNextControlName("CreatureId");
            id=GUILayout.TextField(id,64,GUILayout.Height(34));
            GUI.enabled=reference!=null && ReferenceBridge.TryParseId(id,out _,out _);
            if(GUILayout.Button("Apply ID",theme.Primary,GUILayout.Width(94),GUILayout.Height(34))) Generate();
            GUI.enabled=true; GUILayout.EndHorizontal();
            GUILayout.Label("Seed number or ID.spawner.number",theme.Small);
            if(!ReferenceBridge.TryParseId(id,out _,out _)) GUILayout.Label("Enter a whole-number seed or ID.spawner.number.",theme.Small);
            GUILayout.Space(10);
            GUILayout.Label("Desktop size  "+desktopScale.ToString("0.00")+"×");
            GUILayout.BeginHorizontal();
            SizePreset("Small · 1×",1); SizePreset("Medium · 1.2×",DesktopSettings.DefaultScale);
            GUILayout.EndHorizontal(); GUILayout.BeginHorizontal();
            SizePreset("Large · 1.5×",1.5f); SizePreset("Extra large · 2×",2);
            GUILayout.EndHorizontal();
            desktopScale=GUILayout.HorizontalSlider(desktopScale,.5f,2f);
            GUILayout.BeginHorizontal(); GUILayout.Label("Changes the small pet on your desktop.",theme.Small);
            if(GUILayout.Button("Reset size",GUILayout.Width(84))) desktopScale=DesktopSettings.DefaultScale;
            GUILayout.EndHorizontal(); GUILayout.EndVertical();
            GUILayout.BeginVertical(theme.Card);
            if(Section("Movement",ref movementOpen))
            {
                ToggleRow("Roam independently",ref desktopRoam); ToggleRow("Allow cyan propulsion",ref autonomousJumps);
                GUILayout.Space(6); GUILayout.Label("Climbing mode",theme.Small);
                GUILayout.BeginHorizontal();
                ModeChoice("Edges only",ClimbingMode.EdgesOnly); ModeChoice("Edges + backgrounds",ClimbingMode.EdgesAndBackgrounds);
                GUILayout.EndHorizontal();
                GUILayout.Label(desktopClimbingMode==ClimbingMode.EdgesOnly?"Climbs desktop borders and visible window edges.":"Also climbs desktop backgrounds and window interiors.",theme.Small);
            }
            GUILayout.EndVertical();
            GUILayout.BeginVertical(theme.Card);
            if(Section("Interaction",ref interactionOpen))
            {
                ToggleRow("Explore where I click",ref desktopClicks);
                GUILayout.Label("Clicks within about three body lengths invite a direct hop. Farther clicks invite longer hops that stop short of your cursor.",theme.Small);
                GUILayout.Space(6);
                GUILayout.Label("Hold the pet for 0.1 seconds to grab it, then release to drop. Move nearby to get its attention. Clicks still reach your apps.",theme.Small);
                GUILayout.Space(6); GUILayout.Label("Pet for 0.7 seconds for an affection call, with a 3-second cooldown.",theme.Small);
            }
            GUILayout.EndVertical();
            GUILayout.BeginVertical(theme.Card);
            if(Section("Sound",ref soundOpen))
            {
                ToggleRow("Desktop sounds",ref desktopSounds); GUI.enabled=desktop.Active && desktopSounds;
                if(GUILayout.Button("Play sample call",GUILayout.Height(30))) desktopVocalizeRequest++;
                GUI.enabled=true;
                if(!desktop.Active) GUILayout.Label("Start your desktop pet to hear a sample call.",theme.Small);
                else if(!desktopSounds) GUILayout.Label("Enable sounds to hear a sample call.",theme.Small);
                GUILayout.Label("Head glow and propulsion effects stay active when sound is muted.",theme.Small);
            }
            GUILayout.EndVertical();
            GUILayout.BeginVertical(theme.Card);
            if(Section("Advanced",ref advancedOpen))
            {
                ToggleRow("Gentle pose motion",ref animate);
                GUILayout.Label("VIEW ANGLE",theme.Small);
                float nextDepth=GUILayout.HorizontalSlider(depth,-.95f,.95f);
                if(nextDepth!=depth) { depth=nextDepth; if(Ready) { try { RefreshPreview(); } catch(Exception e) { Fail(e); } } }
                if(Ready)
                {
                    GUILayout.Label(status,theme.Small); GUILayout.Label(reference.Summary??"",theme.Small);
                    GUILayout.Label("Tamed companion / personality from ID",theme.Small);
                    GUILayout.Label(reference.Personality.ToString(),theme.Small);
                }
                GUI.enabled=Ready;
                if(GUILayout.Button("Save comparison image")) StartCoroutine(Capture());
                GUI.enabled=true;
                if(Ready) { GUILayout.Label("Prepared reference folder",theme.Small); GUILayout.Label(cache??"",theme.Small); }
            }
            GUILayout.EndVertical();
            GUILayout.EndScrollView(); GUILayout.EndArea();
            GUILayout.BeginArea(new Rect(left,footer,width,142));
            GUILayout.BeginHorizontal(); GUILayout.Label(desktop.Active?(trayState.Paused?"Desktop pet is paused":"Desktop pet is running"):"Desktop pet is stopped",theme.Small);
            GUILayout.FlexibleSpace();
            GUI.enabled=desktop.Active || StartBlocker==null;
            if(GUILayout.Button(desktop.Active?"Stop desktop pet":"Start desktop pet",theme.Primary,GUILayout.Width(166),GUILayout.Height(36)))
            {
                if(desktop.Active) desktop.Stop();
                else try { desktop.Start(DesktopConfiguration()); } catch(Exception e) { Fail(e); }
            }
            GUI.enabled=true; GUILayout.EndHorizontal();
            GUILayout.Label(desktop.Active?(tray!=null && tray.Available?"Close this window to keep Lizor in the tray.":"Keep this panel open while your pet is running."):StartBlocker??"Ready. Start your desktop pet when you like.",theme.Small);
            GUILayout.BeginHorizontal();
            GUI.enabled=desktop.Active;
            if(GUILayout.Button(trayState.Paused?"Resume pet":"Pause pet",GUILayout.Width(100))) trayState.TogglePause(desktop.Active);
            GUI.enabled=!Application.isEditor;
            GUILayout.FlexibleSpace(); if(GUILayout.Button("Quit Lizor",GUILayout.Width(100))) QuitApp();
            GUI.enabled=true; GUILayout.EndHorizontal();
            GUILayout.Space(5);
            GUILayout.Label("An unofficial desktop companion inspired by Rain World. Rain World belongs to Videocult and Akupara Games.",theme.Small);
            GUILayout.EndArea();
        }
        void DrawSetup()
        {
            GUILayout.BeginVertical(theme.Card);
            GUILayout.Label("RAIN WORLD INSTALLATION",theme.Heading);
            GUILayout.Label("Select the folder containing RainWorld_Data. Requires Rain World v1.11.8.",theme.Small);
            GUI.enabled=importer==null && folderPicker==null;
            gameFolder=GUILayout.TextField(gameFolder,GUILayout.Height(34));
            GUILayout.BeginHorizontal();
            if(GUILayout.Button("Browse…",GUILayout.Height(32))) folderPicker=WindowsFolderPicker.Browse();
            GUI.enabled=importer==null && folderPicker==null && detection==null;
            if(GUILayout.Button("Detect again",GUILayout.Height(32))) DetectInstallation();
            GUILayout.EndHorizontal(); GUI.enabled=true;
            if(!string.IsNullOrEmpty(setupMessage)) GUILayout.Label(setupMessage,theme.Small);
            bool validFolder=GameInstallation.IsInstallation(gameFolder);
            if(!validFolder) GUILayout.Label("Choose the game folder, not RainWorld.exe or RainWorld_Data. You can paste a quoted folder path.",theme.Small);
            GUI.enabled=importer==null && folderPicker==null && validFolder;
            if(GUILayout.Button("Prepare local game assets",theme.Primary,GUILayout.Height(34))) ImportReference();
            GUI.enabled=true;
            if(importer!=null) GUILayout.Label("Preparing your local reference… Please wait.",theme.Small);
            GUI.enabled=importer==null; GUILayout.Label("Already prepared assets? Reference folder",theme.Small); cache=GUILayout.TextField(cache??"",GUILayout.Height(32));
            GUI.enabled=importer==null;
            if(GUILayout.Button("Load prepared reference")) LoadReference();
            GUI.enabled=true; GUILayout.EndVertical();
        }
        bool Section(string label,ref bool open)
        {
            if(GUILayout.Button(label+"   "+(open?"−":"+"),theme.Heading,GUILayout.Height(24))) open=!open;
            if(open) GUILayout.Space(8);
            return open;
        }
        void ToggleRow(string label,ref bool value)
        {
            GUILayout.BeginHorizontal(); GUILayout.Label(label,GUILayout.Height(30)); GUILayout.FlexibleSpace();
            if(GUILayout.Button(value?"ON":"OFF",value?theme.Selected:GUI.skin.button,GUILayout.Width(58),GUILayout.Height(28))) value=!value;
            GUILayout.EndHorizontal();
        }
        void SizePreset(string label,float size)
        {
            if(GUILayout.Button(label,Mathf.Approximately(desktopScale,size)?theme.Selected:GUI.skin.button,GUILayout.Height(32))) desktopScale=size;
        }
        void ModeChoice(string label,ClimbingMode mode)
        {
            if(GUILayout.Button(label,desktopClimbingMode==mode?theme.Selected:GUI.skin.button,GUILayout.Height(36))) desktopClimbingMode=mode;
        }
        void ImportReference()
        {
            try
            {
                gameFolder=GameInstallation.Normalize(gameFolder);
                if (!GameInstallation.IsInstallation(gameFolder))
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
        void OnDestroy()
        {
            SavePreferences();
            tray?.Dispose(); desktop.Dispose();
            try { if(importer!=null && !importer.HasExited) importer.Kill(); }
            catch(Exception e) { Debug.LogWarning("Importer shutdown: "+e.Message); }
            importer?.Dispose(); view?.Dispose(); reference?.Dispose(); context?.Dispose(); theme?.Dispose(); instance?.Dispose();
            if(previewCamera!=null) previewCamera.targetTexture=null;
            if(previewTexture!=null) { previewTexture.Release(); Destroy(previewTexture); }
        }
    }
}
