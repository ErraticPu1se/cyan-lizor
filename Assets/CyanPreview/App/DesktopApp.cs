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
        public int vocalizeRequest, feedRequest, cancelFeedRequest, scuffleRequest;
        public string scuffleFirst, scuffleSecond;
        public bool paused;
        public bool allowFighting;
        public DesktopPetConfiguration[] pets;
        public const int MaximumPets = 3;
        public DesktopPetConfiguration[] Roster => pets ?? new[] { DesktopPetConfiguration.From(this) };
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
        public void ValidateRoster()
        {
            var roster=Roster;
            if(roster.Length<1 || roster.Length>MaximumPets) throw new InvalidDataException("Choose one to three pets.");
            var keys=new System.Collections.Generic.HashSet<string>();
            foreach(var pet in roster) { if(pet==null) throw new InvalidDataException("Missing pet."); pet.Validate(); if(!keys.Add(pet.key)) throw new InvalidDataException("Duplicate pet instance."); }
        }
        public void Validate()
        {
            UpgradeClimbingMode(); ValidateRoster();
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
        public DesktopSocialStatus ReadSocialStatus()
        {
            try { return Active && File.Exists(file+".social") ? JsonUtility.FromJson<DesktopSocialStatus>(File.ReadAllText(file+".social")) : null; }
            catch(IOException) { return null; }
            catch(ArgumentException) { return null; }
        }
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
        public bool TryReadMenu(out DesktopMenuAction action) => TryReadMenu(out _,out action);
        public bool TryReadMenu(out string key,out DesktopMenuAction action)
        {
            key="primary";
            action = DesktopMenuAction.None;
            var directory = Path.GetDirectoryName(file);
            if (!Directory.Exists(directory)) return false;
            var requests = Directory.GetFiles(directory, Path.GetFileName(file) + ".menu-*");
            requests = Array.FindAll(requests, path => !path.EndsWith(".tmp", StringComparison.Ordinal));
            Array.Sort(requests, StringComparer.Ordinal);
            if (requests.Length == 0) return false;
            string request = requests[0];
            string value = File.ReadAllText(request); File.Delete(request);
            int separator=value.IndexOf('|'); if(separator>=0) { key=value.Substring(0,separator); value=value.Substring(separator+1); }
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
        readonly System.Collections.Generic.List<DesktopPet> pets = new System.Collections.Generic.List<DesktopPet>();
        Camera cameraView;
        DesktopAlphaRenderer desktopRenderer;
        DesktopPet pointerPet;
        float refresh, holdElapsed;
        uint pressTime;
        bool ready, buttonHeld, foodPressed, menuOpen;
        Rect Bounds => new Rect(-native.WorkArea.width/2,-native.WorkArea.height/2,native.WorkArea.width,native.WorkArea.height);
        Vector2 World(Vector2 screen) => DesktopCoordinates.ToWorld(screen,native.WorkArea,1);
        Vector2 Clamp(Vector2 p) => new Vector2(Mathf.Clamp(p.x,Bounds.xMin+10,Bounds.xMax-10),Mathf.Clamp(p.y,Bounds.yMin+10,Bounds.yMax-10));
        IEnumerator Start()
        {
            try
            {
                var args=Environment.GetCommandLineArgs(); int index=Array.IndexOf(args,"--desktop-settings");
                if(index<0 || index+1>=args.Length) throw new InvalidDataException("Desktop settings argument missing.");
                settingsFile=args[index+1]; settings=LoadSettings(); native=new WindowsDesktop(settings.controllerPid);
                QualitySettings.antiAliasing=0; Application.runInBackground=true; QualitySettings.vSyncCount=0; Application.targetFrameRate=60;
                cameraView=new GameObject("Desktop camera").AddComponent<Camera>(); cameraView.gameObject.AddComponent<AudioListener>();
                cameraView.orthographic=true; cameraView.clearFlags=CameraClearFlags.SolidColor; cameraView.enabled=false;
                cameraView.backgroundColor=Color.clear; cameraView.transform.position=new Vector3(0,0,-10);
                Screen.SetResolution((int)native.WorkArea.width,(int)native.WorkArea.height,FullScreenMode.Windowed);
                context=new ReferenceContext(settings.cache); foodView=new FoodMorselView(transform); SyncPets(false); handledScuffleRequest=settings.scuffleRequest;
            }
            catch(Exception e) { Debug.LogException(e); Application.Quit(); yield break; }
            yield return null; yield return null;
            try
            {
                if(!native.Initialize()) throw new InvalidOperationException("Owned overlay window not found.");
                desktopRenderer=new DesktopAlphaRenderer(native.WindowHandle,cameraView,transform);
                native.HitTest=cursor=>!settings.paused && (food.Owner==FoodOwner.Placing || HitPet(World(cursor))!=null || HitFood(World(cursor)));
                ready=true; RefreshWindows(); Debug.Log("DESKTOP_OVERLAY_READY pets="+pets.Count);
            }
            catch(Exception e) { Debug.LogException(e); Application.Quit(); }
        }
        DesktopSettings LoadSettings()
        {
            var value=JsonUtility.FromJson<DesktopSettings>(File.ReadAllText(settingsFile));
            if(value==null) throw new InvalidDataException("Missing desktop settings."); value.Validate(); return value;
        }
        void SyncPets(bool displayChanged)
        {
            var roster=settings.Roster;
            for(int i=pets.Count-1;i>=0;i--)
            {
                var p=pets[i];
                if(Array.Exists(roster,c=>c.key==p.Config.key)) continue;
                foodView.Show(false,Vector2.zero); food.Remove(p.Config.key,Clamp(p.WorldMouth));
                if(pointerPet==p) CancelPointer(); p.Dispose(); pets.RemoveAt(i); scuffles.Clear();
            }
            foreach(var config in roster)
            {
                var p=pets.Find(x=>x.Config.key==config.key);
                if(p==null)
                {
                    p=new DesktopPet(context,transform,config,Bounds,nextOrder++); pets.Add(p);
                    var position=new Vector2(Bounds.xMin+Bounds.width*(pets.Count/(float)(roster.Length+1)),Bounds.yMin+16*config.scale);
                    p.Motion.ShiftWithSurface(p.Local(position)-p.Motion.Position);
                }
                else if(displayChanged || p.Config.id!=config.id || p.Config.scale!=config.scale)
                {
                    foodView.Show(false,Vector2.zero); food.Remove(p.Config.key,Clamp(p.WorldMouth));
                    if(pointerPet==p) CancelPointer(); p.Rebuild(config,Bounds,Clamp(p.WorldPosition)); scuffles.Clear();
                }
                p.Configure(config,settings.paused);
            }
            cameraView.orthographicSize=native.WorkArea.height/2;
            if(displayChanged) CancelFeeding();
        }
        int nextOrder;
        DesktopPet HitPet(Vector2 point)
        { for(int i=pets.Count-1;i>=0;i--) if(pets[i].Contains(point)) return pets[i]; return null; }
        DesktopPet NearestPet(Vector2 point)
        {
            DesktopPet best=null; float distance=float.MaxValue;
            foreach(var p in pets) if(!p.Feeding.Active && !p.FollowingFood && !p.Motion.RivalTarget.HasValue && p.Config.clickToExplore)
            { float d=(p.WorldPosition-point).sqrMagnitude; if(d<distance) { best=p; distance=d; } }
            return best;
        }
        bool HitFood(Vector2 point) => food.Available && (food.Position-point).sqrMagnitude<=18*18;
        void RefreshWindows() { foreach(var p in pets) p.RefreshWindows(native.Snapshot(p.Config.scale)); }
        void CancelPointer()
        {
            pointerPet?.Input.Cancel(); pointerPet=null; foodPressed=buttonHeld=false;
            if(food.Owner==FoodOwner.Cursor) food.Drop(Clamp(food.Position));
        }
        void Update()
        {
            if(!ready || menuOpen) return;
            try
            {
                float dt=Time.unscaledDeltaTime; refresh-=dt;
                if(refresh<=0)
                {
                    refresh=.25f; DesktopSettings next;
                    try { next=LoadSettings(); } catch(IOException) { next=settings; }
                    if(!next.running || !native.ControllerAlive) { Application.Quit(); return; }
                    bool vocalize=next.vocalizeRequest!=settings.vocalizeRequest;
                    bool feed=next.feedRequest!=settings.feedRequest, cancel=next.cancelFeedRequest!=settings.cancelFeedRequest;
                    if(next.paused!=settings.paused) { CancelPointer(); native.ClearButtonEvents(); }
                    settings=next; bool resized=native.RefreshWorkArea();
                    if(resized) Screen.SetResolution((int)native.WorkArea.width,(int)native.WorkArea.height,FullScreenMode.Windowed);
                    SyncPets(resized); ProcessScuffleRequest();
                    if(cancel) CancelFeeding(); else if(feed && !settings.paused) BeginFeeding();
                    if(vocalize && !settings.paused && pets.Count>0) pets[0].Audio?.Vocalize();
                }
                if(settings.paused) { AdvanceScuffles(0); native.ClearButtonEvents(); native.SetClickThrough(true); RefreshWindows(); DrawFrame(0,true); return; }
                var point=World(native.Cursor);
                foreach(var p in pets) if(!p.Feeding.Frozen) p.Motion.ObserveCursor(p.Local(point));
                if(food.Owner==FoodOwner.Placing) { PlaceFoodInput(point); RefreshWindows(); DrawFrame(dt,false); return; }
                if(native.TryReadMenuRequest(out var menuPoint))
                {
                    var selected=HitPet(World(menuPoint)); if(selected==null) return;
                    menuOpen=true;
                    try
                    {
                        CancelPointer(); var action=native.ShowPetMenu(menuPoint,selected.Config.MenuSettings());
                        if(action==DesktopMenuAction.Feed) BeginFeeding();
                        else if(action==DesktopMenuAction.Settings) native.ShowController();
                        else if(action!=DesktopMenuAction.None)
                        {
                            string request=settingsFile+".menu-"+DateTime.UtcNow.Ticks+"-"+Guid.NewGuid().ToString("N");
                            File.WriteAllText(request+".tmp",selected.Config.key+"|"+(int)action); File.Move(request+".tmp",request);
                        }
                    }
                    finally { menuOpen=false; native.ClearButtonEvents(); }
                    return;
                }
                while(native.TryReadButton(out var button))
                {
                    var eventPoint=World(button.Position);
                    if(button.Down)
                    {
                        CancelPointer(); buttonHeld=true; pressTime=button.Time; holdElapsed=0;
                        if(!native.WorkArea.Contains(button.Position)) continue;
                        // Mouth-held food is part of its pet. Loose food gets first refusal.
                        foodPressed=HitFood(eventPoint);
                        if(foodPressed) continue;
                        pointerPet=HitPet(eventPoint);
                        if(pointerPet!=null) pointerPet.Input.Press(pointerPet.Local(eventPoint));
                        else if(!button.ControllerFocused && !native.IsWindowControl(button.Position))
                        {
                            var nearest=NearestPet(eventPoint);
                            if(nearest!=null && !nearest.Surfaces.IsWindowChrome(nearest.Local(eventPoint),nearest.Config.scale)) nearest.Motion.ExploreClick(nearest.Local(eventPoint));
                        }
                    }
                    else
                    {
                        AdvanceHold(eventPoint,button.Time);
                        pointerPet?.Input.Release(pointerPet.Local(eventPoint),native.WorkArea.Contains(button.Position)); pointerPet=null;
                        if(food.Owner==FoodOwner.Cursor) food.Drop(OfferPoint(eventPoint));
                        foodPressed=buttonHeld=false;
                    }
                }
                AdvanceHold(point,unchecked((uint)Environment.TickCount));
                native.SetClickThrough(pointerPet==null && food.Owner!=FoodOwner.Cursor && (buttonHeld || (HitPet(point)==null && !HitFood(point))));
                RefreshWindows();
                var hovered=HitPet(point);
                foreach(var p in pets)
                    if(p.Input.Hover(p.Local(point),dt,buttonHeld || hovered!=p || p.Feeding.UsingRoute || p.Motion.RivalTarget.HasValue,p.Audio!=null && p.Audio.CanPettingCall)) p.Audio.PettingCall();
                ResolvePetContacts(dt); AdvanceFeeding(dt); AdvanceScuffles(dt); DrawFrame(dt,false);
            }
            catch(Exception e) { Debug.LogException(e); Application.Quit(); }
        }
        void AdvanceHold(Vector2 point,uint now)
        {
            if(!buttonHeld) return; float elapsed=unchecked(now-pressTime)/1000f;
            if(foodPressed && elapsed>=PetInteraction.GrabDelay && food.Available) food.TakeCursor();
            if(food.Owner==FoodOwner.Cursor) food.Move(Clamp(point));
            pointerPet?.Input.Hold(pointerPet.Local(point),Mathf.Max(0,elapsed-holdElapsed)); holdElapsed=elapsed;
        }
        void DrawFrame(float dt,bool paused)
        {
            foreach(var p in pets) p.Advance(dt,paused);
            RefreshFoodVisual(); PublishFeedingStatus(); desktopRenderer.Render(native.WorkArea,1); DesktopFrameProfile.EndFrame();
        }
        void OnDestroy()
        {
            foodView?.Dispose(); desktopRenderer?.Dispose(); native?.Dispose();
            foreach(var p in pets) p.Dispose(); pets.Clear(); context?.Dispose();
        }
    }
}
