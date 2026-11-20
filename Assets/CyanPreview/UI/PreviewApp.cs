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
        bool advancedOpen, preparedAssetsOpen;
        int desktopVocalizeRequest;
        string vocalizePetKey;
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
                Screen.SetResolution(Mathf.Min(540,Display.main.systemWidth),Mathf.Min(840,Display.main.systemHeight-80),FullScreenMode.Windowed);
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
            desktopClicks=preferences.clickToExplore; desktopSounds=preferences.sound; desktopRoam=preferences.roam; autonomousJumps=preferences.jumps; muteAll=preferences.muteAll;
            showAttentionLabels=PlayerPrefs.GetInt(AttentionLabelsKey,1)!=0;
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
                context?.Dispose(); context=null; context=new ReferenceContext(cache); reference=context.CreatePet(presets?.Data.Current.species??"cyan");
                PlayerPrefs.SetString("ReferenceCache",cache); Generate();
            }
            catch(Exception e) { Fail(e); }
        }
        void Generate()
        {
            try
            {
                if(reference.Species.Id!=selectedSpecies)
                {
                    SavePreset(); SelectPreset(PetSelection.Create(presets.Data,selectedSpecies,id,CurrentPreferences())); return;
                }
                reference.Generate(id); phase=0; RefreshPreview();
                status=reference.Identity+"  ��  "+reference.Species.DisplayName+"  ��  "+reference.Manifest.gameVersion;
                error=null; appliedId=id; PlayerPrefs.SetString("LastId",id); PlayerPrefs.Save();
            }
            catch(Exception e) { Fail(e); }
        }
        void RefreshPreview()
        {
            var shown=selectionPreview??reference;
            shown.Pose(phase,depth); shown.Draw(); view.Refresh(shown);
        }
        void Fail(Exception e) { error=e.Message; Debug.LogException(e); }
        DesktopSettings PrimaryConfiguration() => new DesktopSettings {id=reference.Identity,species=reference.Species.Id,cache=cache,scale=desktopScale,
            paused=trayState.Paused,climbingMode=desktopClimbingMode,clickToExplore=desktopClicks,sound=desktopSounds,vocalizeRequest=desktopVocalizeRequest,vocalizePetKey=vocalizePetKey,roam=desktopRoam,jumps=autonomousJumps};
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
            tray.UpdateState(desktop.Active,trayState.Paused,!muteAll);
            while(tray.TryRead(out var action))
            {
                switch(action)
                {
                    case TrayAction.Settings: tray.ShowSettings(); break;
                    case TrayAction.PauseResume: trayState.TogglePause(desktop.Active); break;
                    case TrayAction.Sound: muteAll=!muteAll; SavePreferences(); break;
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
            setupMessage="Looking in Steam libraries��";
            detection=Task.Run(()=>GameInstallation.Detect(saved));
        }
        void SavePreferences()
        {
            if(!initialized) return;
            var preferences=new CompanionPreferences { gameFolder=gameFolder, scale=desktopScale, climbingMode=desktopClimbingMode,
                sound=desktopSounds, roam=desktopRoam, jumps=autonomousJumps, clickToExplore=desktopClicks, muteAll=muteAll };
            string json=JsonUtility.ToJson(preferences);
            // Preset changes are explicit; browsing and editing a live pet never overwrite a saved preset.
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
                    while(desktop.TryReadMenu(out var petKey,out var action))
                    {
                        if(action==DesktopMenuAction.Stop) { RemoveActivePet(petKey); continue; }
                        if(petKey!="primary") { extraPets.Find(p=>p.key==petKey)?.ApplyMenu(action); continue; }
                        var updated=PrimaryConfiguration(); updated.ApplyMenu(action);
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
        void DrawSetup()
        {
            GUILayout.BeginVertical(theme.Card);
            GUILayout.Label("RAIN WORLD INSTALLATION",theme.Heading);
            GUILayout.Label("Select the folder containing RainWorld_Data. Requires Rain World v1.11.8.",theme.Small);
            GUI.enabled=importer==null && folderPicker==null;
            gameFolder=GUILayout.TextField(gameFolder,GUILayout.Height(34));
            GUILayout.BeginHorizontal();
            if(GUILayout.Button("Browse��",GUILayout.Height(32))) folderPicker=WindowsFolderPicker.Browse();
            GUI.enabled=importer==null && folderPicker==null && detection==null;
            if(GUILayout.Button("Detect again",GUILayout.Height(32))) DetectInstallation();
            GUILayout.EndHorizontal(); GUI.enabled=true;
            if(!string.IsNullOrEmpty(setupMessage)) GUILayout.Label(setupMessage,theme.Small);
            bool validFolder=GameInstallation.IsInstallation(gameFolder);
            if(!validFolder) GUILayout.Label("Choose the game folder, not RainWorld.exe or RainWorld_Data. You can paste a quoted folder path.",theme.Small);
            GUI.enabled=importer==null && folderPicker==null && validFolder;
            if(GUILayout.Button("Prepare local game assets",theme.Primary,GUILayout.Height(34))) ImportReference();
            GUI.enabled=true;
            if(importer!=null) GUILayout.Label("Preparing your local reference�� Please wait.",theme.Small);
            if(Section("Advanced: prepared assets",ref preparedAssetsOpen))
            {
            GUI.enabled=importer==null; GUILayout.Label("Reference folder",theme.Small); cache=GUILayout.TextField(cache??"",GUILayout.Height(32));
            GUI.enabled=importer==null;
            if(GUILayout.Button("Load prepared reference")) LoadReference();
            }
            GUI.enabled=true; GUILayout.EndVertical();
        }
        bool Section(string label,ref bool open)
        {
            if(GUILayout.Button(label+"   "+(open?"?":"+"),theme.Heading,GUILayout.Height(24))) open=!open;
            if(open) GUILayout.Space(8);
            return open;
        }
        void ToggleRow(string label,ref bool value)
        {
            GUILayout.BeginHorizontal(); GUILayout.Label(label,GUILayout.Height(30)); GUILayout.FlexibleSpace();
            if(GUILayout.Button(value?"ON":"OFF",value?theme.Selected:GUI.skin.button,GUILayout.Width(58),GUILayout.Height(28))) value=!value;
            GUILayout.EndHorizontal();
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
            importer?.Dispose(); view?.Dispose(); selectionPreview?.Dispose(); reference?.Dispose(); context?.Dispose(); theme?.Dispose(); instance?.Dispose();
            if(previewCamera!=null) previewCamera.targetTexture=null;
            if(previewTexture!=null) { previewTexture.Release(); Destroy(previewTexture); }
        }
    }
}

