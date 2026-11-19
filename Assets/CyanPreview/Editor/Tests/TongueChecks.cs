using System;
using UnityEngine;
using static CyanPreview.ReferenceContext;
namespace CyanPreview.Editor
{
    public static class TongueChecks
    {
        static void Check(bool value,string message) { if(!value) throw new Exception(message); }
        public static void Verify() { using(var c=new ReferenceContext(System.IO.Path.GetFullPath("local-data"))) Run(c); }
        static void HostCatch(ReferenceContext context)
        {
            var root=new GameObject("White tongue feeding integration"); var host=root.AddComponent<DesktopApp>();
            var native=new WindowsDesktop(System.Diagnostics.Process.GetCurrentProcess().Id);
            var area=new Rect(-native.WorkArea.width/2,-native.WorkArea.height/2,native.WorkArea.width,native.WorkArea.height);
            Set(host,"native",native); Set(host,"settings",new DesktopSettings());
            var list=(System.Collections.Generic.List<DesktopPet>)Get(host,"pets"); var food=(SharedFood)Get(host,"food");
            try
            {
                var config=DesktopPetConfiguration.From(new DesktopSettings {id="24556",species="white",sound=false,roam=false},"white-test");
                var pet=new DesktopPet(context,root.transform,config,area,0); list.Add(pet);
                for(int round=0;round<2;round++)
                {
                    food.Cancel(); pet.Feeding.Cancel(); pet.FoodRevision=-1;
                    food.Begin(); food.Place(pet.WorldMouth+Vector2.right*180*config.scale);
                    bool caught=false;
                    for(int frame=0;frame<240;frame++)
                    {
                        Call(host,"AdvanceFeeding",.025f); pet.Advance(.025f,false);
                        if(food.Owner==FoodOwner.Tongue)
                        {
                            caught=true;
                            if(round==1)
                            {
                                pet.Motion.BeginGrab(pet.Motion.Position); Call(host,"AdvanceFeeding",.025f);
                                Check(food.Available&&!pet.Tongue.Active&&!pet.Motion.TongueActive,"Grab stranded a tongue-held morsel"); break;
                            }
                        }
                        if(food.Owner==FoodOwner.Pet) break;
                    }
                    Check(caught,"Host never caught food with tongue");
                    if(round==0) Check(food.Owner==FoodOwner.Pet && pet.Feeding.Stage==FeedingStage.Carrying,"Host did not hand tongue food to feeding");
                }
            }
            finally { foreach(var pet in list) pet.Dispose(); list.Clear(); native.Dispose(); UnityEngine.Object.DestroyImmediate(root); }
        }
        static void Curious(ReferenceContext context)
        {
            using(var white=context.CreatePet("white"))
            {
                white.Generate("24556");
                int previousResponses=-1;
                foreach(float distance in new[]{70f,100f,130f})
                {
                    int responses=0;
                    for(int seed=0;seed<100;seed++)
                    {
                        var b=new PetBehavior(seed,white.Personality,1);
                        // Real click fallback retains a strong investigation cue; do not reset it as the old test did.
                        b.Target(Vector2.right*distance);
                        if(b.ChooseCursorFlick(distance)) responses++;
                    }
                    Check(responses>=previousResponses,"Farther clicks did not favor flicking"); previousResponses=responses;
                    Debug.Log("CURSOR_FLICK_REPLAY distance="+distance+" responses="+responses);
                    Check(responses>0 && responses<=100,"Prior click/normal cursor distance suppressed every flick at "+distance);
                }
            }
            int chosen=0;
            for(int seed=0;seed<100;seed++)
            {
                var behavior=new PetBehavior(seed,null,1);
                bool pick=behavior.ChooseCursorFlick(40); float fresh=behavior.CursorFlickUtility; if(pick) chosen++;
                Check((float)Get(behavior,"flickNovelty")==1 && (float)Get(behavior,"flickCooldown")==0,"Decision consumed interest before a flick started");
                if(pick) { behavior.CursorFlickStarted(); Check(!behavior.ChooseCursorFlick(40),"Repeated click bypassed flick cooldown"); }
                var tired=new PetBehavior(seed,null,1); Set(tired,"fatigue",1f); tired.ChooseCursorFlick(40);
                Check(tired.CursorFlickUtility<=fresh,"Fatigue increased curiosity");
            }
            Check(chosen>0 && chosen<100,"Curious flick is never chosen or happens every click");
            var cooldown=new PetBehavior(42) {HoldPosition=true}; cooldown.CursorFlickStarted();
            var surface=new BackgroundSurfaces(new Rect(-500,-500,1000,1000));
            for(int tick=0;tick<159;tick++) cooldown.Update(Vector2.zero,surface);
            Check(!cooldown.ChooseCursorFlick(130),"Flick cooldown ended before four seconds");
            for(int tick=0;tick<2;tick++) cooldown.Update(Vector2.zero,surface);
            Check((float)Get(cooldown,"flickCooldown")==0 && (float)Get(cooldown,"flickNovelty")>.99f,"Four-second cooldown or interest recovery remained blocked");
            foreach(float distance in new[]{40f,70f,85f,130f})
            {
                int before=0,after=0; const int count=1000;
                float distanceWeight=Mathf.Lerp(.55f,1.6f,Mathf.SmoothStep(0,1,Mathf.InverseLerp(50,150,distance)));
                for(int seed=0;seed<count;seed++)
                {
                    float oldScore=distanceWeight*1.3f*(1-.15f*.25f)*(1-.5f*.25f)*(.25f+(float)new System.Random(seed^49271).NextDouble());
                    if(oldScore>.65f) before++;
                    if(new PetBehavior(seed).ChooseCursorFlick(distance)) after++;
                }
                Check(Mathf.Abs(after-Mathf.Min(count,before*2))<25,"Cursor shot selection rate did not double");
                Debug.Log("CURSOR_SHOT_RATE distance="+distance+" before="+before+" after="+after+" / "+count);
            }
            var foodShot=new PetTongue(); var cursorShot=new PetTongue(); var shotTarget=Vector2.right*100;
            foodShot.TryStart(Vector2.zero,Vector2.right,shotTarget); cursorShot.TryStartCurious(Vector2.zero,Vector2.right,shotTarget,50);
            for(int frame=0;frame<43;frame++)
            {
                foodShot.Advance(.025f,Vector2.zero,Vector2.right,shotTarget,()=>false);
                cursorShot.Advance(.025f,Vector2.zero,Vector2.right,null,()=>{throw new Exception("Cursor shot tried attachment");});
                Check(foodShot.Stage==cursorShot.Stage && Vector2.Distance(foodShot.Tip,cursorShot.Tip)<.001f,"Cursor shot differs from food wind-up/launch");
            }
            Check(cursorShot.Tip.x>60,"Cursor tongue retained the short flick reach");
            var outside=new PetBehavior(1); Check(!outside.ChooseCursorFlick(500)&&!outside.ChooseCursorFlick(float.NaN),"Invalid/far cursor triggered flick");
            var flick=new PetTongue(); int sounds=0; flick.Sound+=stage=>{ Check(stage!=2,"Cursor shot played an attachment sound"); sounds++; };
            Check(flick.TryStartCurious(Vector2.zero,Vector2.right,new Vector2(80,0),50),"Could not start curious flick");
            var aim=flick.Aim;
            flick.Advance(0,Vector2.zero,Vector2.right,null,null); Check(flick.Stage==TongueStage.Aiming,"Pause advanced flick");
            for(int i=0;i<80;i++)
            {
                flick.Advance(.025f,Vector2.zero,Vector2.right,new Vector2(500,0),()=>{throw new Exception("Curious tongue attempted to claim an object");});
                Check(flick.Aim==aim && !flick.Holding && !flick.Delivered,"Curious flick chased or caught its target");
            }
            Check(!flick.Active && sounds==3,"Cursor shot did not prepare, launch, and retract");
            var root=new GameObject("Cursor flick host check"); var host=root.AddComponent<DesktopApp>();
            var list=(System.Collections.Generic.List<DesktopPet>)Get(host,"pets"); var food=(SharedFood)Get(host,"food");
            try
            {
                var config=DesktopPetConfiguration.From(new DesktopSettings {id="24556",species="white",sound=false,clickToExplore=true},"curious-test");
                var pet=new DesktopPet(context,root.transform,config,new Rect(-800,-500,1600,1000),0); list.Add(pet);
                var m=pet.Motion; var target=m.MouthPosition+Vector2.right*45;
                Set(m.Behavior,"tongueRandom",new System.Random(42));
                // Repeated real clicks, with ordinary behavior updates between attempts.
                for(int attempt=0;attempt<20 && !pet.Tongue.Active;attempt++)
                {
                    var facing=(m.HeadPosition-m.BodyPosition(0)).normalized;
                    target=m.MouthPosition+facing*100;
                    Call(host,"CursorClick",pet,target);
                    if(!pet.Tongue.Active) for(int frame=0;frame<160 && !pet.Tongue.Active;frame++)
                    { Call(host,"AdvanceTongue",pet,.025f); pet.Advance(.025f,false); }
                }
                Check(pet.Tongue.Active && pet.Tongue.Curious,"Host click did not select curiosity");
                Call(host,"CursorClick",pet,target+Vector2.right*10);
                Check(m.Behavior.HoldPosition && food.Owner==FoodOwner.None,"Repeated click replaced the gesture or claimed food");
                var originalAim=pet.Tongue.Aim;
                var latest=target+Vector2.right*10;
                Check(pet.AfterFlickTarget==latest && pet.Tongue.Aim==originalAim,"New click restarted the flick instead of updating its destination");
                for(int frame=0;frame<100 && pet.Tongue.Active;frame++) Call(host,"AdvanceTongue",pet,.025f);
                Check(!pet.Tongue.Active && !pet.AfterFlickTarget.HasValue && m.Behavior.InterestPoint.HasValue
                    && !m.Behavior.HoldPosition,"Completed flick did not resume exploration");
                for(int frame=0;frame<25;frame++) pet.Tongue.Advance(.025f,m.MouthPosition,Vector2.right,null,null);
                pet.Tongue.TryStartCurious(m.MouthPosition,Vector2.right,m.MouthPosition+Vector2.right*80,50);
                pet.AfterFlickTarget=latest;
                food.Begin(); food.Place(target*config.scale);
                Call(host,"AdvanceTongue",pet,.025f);
                Check(!pet.Tongue.Active && !m.Behavior.HoldPosition && !pet.AfterFlickTarget.HasValue && food.Available,"Food did not interrupt curiosity safely");
                food.Cancel(); pet.Tongue.Advance(1, m.MouthPosition,Vector2.right,null,null);
                for(int i=0;i<10;i++) pet.Tongue.Advance(.1f,m.MouthPosition,Vector2.right,null,null);
                Check(pet.Tongue.TryStartCurious(m.MouthPosition,Vector2.right,m.MouthPosition+Vector2.right*80,50),"Could not restart test flick");
                m.BeginGrab(m.Position); Call(host,"AdvanceTongue",pet,.025f);
                Check(!pet.Tongue.Active&&!m.TongueActive,"Grab did not interrupt curious flick");
            }
            finally { foreach(var pet in list) pet.Dispose(); list.Clear(); UnityEngine.Object.DestroyImmediate(root); }
            Debug.Log("CURIOUS_TONGUE_CHECKS_PASSED chosen="+chosen+"/100");
        }
        static void TurnBeforeShot(ReferenceContext context)
        {
            Check(PetTongue.CursorInRange(350,50) && !PetTongue.CursorInRange(350.1f,50),"Seven-body cursor range boundary failed");
            Check(PetTongue.CursorRange(100)==PetTongue.AttackRange,"Cursor shot exceeded food attack reach");
            var root=new GameObject("Cursor turn integration"); var host=root.AddComponent<DesktopApp>();
            var list=(System.Collections.Generic.List<DesktopPet>)Get(host,"pets");
            try
            {
                var config=DesktopPetConfiguration.From(new DesktopSettings {id="2455",species="white",sound=false,clickToExplore=true},"turn-test");
                var bounds=new Rect(-1500,-1500,3000,3000);
                var pet=new DesktopPet(context,root.transform,config,bounds,0); list.Add(pet);
                foreach(var direction in new[]{Vector2.right,Vector2.up,Vector2.left})
                {
                    pet.Rebuild(config,bounds,null); var m=pet.Motion;
                    var target=m.MouthPosition+direction*Mathf.Min(m.BodyLength*5,350);
                    Call(host,"CursorClick",pet,target);
                    Check(pet.PendingTongueClick==target && !pet.Tongue.Active,"Click skipped the turn phase");
                    Check((float)Get(m.Behavior,"flickCooldown")==0,"Turning spent the shot cooldown");
                    int frames=0;
                    for(;frames<120 && !pet.Tongue.Active;frames++)
                    { Call(host,"AdvanceTongue",pet,.025f); pet.Advance(.025f,false); }
                    Debug.Log("CURSOR_TURN direction="+direction+" frames="+frames+" head="+(m.HeadPosition-m.Position)+" target="+target+" pending="+pet.PendingTongueClick);
                    Check(pet.Tongue.Active && pet.Tongue.Curious,"Turn did not produce a visible-range shot for "+direction);
                    Check(Vector2.Dot((m.HeadPosition-m.Position).normalized,(target-m.HeadPosition).normalized)>.7f,"Shot fired before facing the click");
                    Check(!pet.PendingTongueClick.HasValue && pet.AfterFlickTarget==target,"Turn lost the exploration destination");
                    Check(!m.Behavior.CanConsiderCursorFlick(100),"Shot did not start its four-second cooldown");
                }
                pet.Rebuild(config,bounds,null);
                var first=pet.Motion.MouthPosition+Vector2.left*200;
                Call(host,"CursorClick",pet,first);
                var latest=pet.Motion.MouthPosition+Vector2.up*200;
                Call(host,"CursorClick",pet,latest);
                Check(pet.PendingTongueClick==latest,"New click did not replace pending turn");
                pet.Motion.BeginGrab(pet.Motion.Position); Call(host,"AdvanceTongue",pet,.025f);
                Check(!pet.PendingTongueClick.HasValue && !pet.Motion.TongueLookTarget.HasValue && !pet.Tongue.Active,"Grab did not cancel queued shot");
                pet.Rebuild(config,bounds,null); Call(host,"CursorClick",pet,latest);
                config.clickToExplore=false; Call(host,"AdvanceTongue",pet,.025f);
                Check(!pet.PendingTongueClick.HasValue && !pet.Motion.Behavior.HoldPosition,"Disabling clicks stranded turn hold");
                config.clickToExplore=true;
                Call(host,"CursorClick",pet,pet.Motion.MouthPosition+Vector2.right*500);
                Check(!pet.PendingTongueClick.HasValue && !pet.Tongue.Active,"Out-of-range click queued a tongue shot");
            }
            finally { foreach(var pet in list) pet.Dispose(); list.Clear(); UnityEngine.Object.DestroyImmediate(root); }
            Debug.Log("CURSOR_TURN_CHECKS_PASSED");
        }
        static void VisibleFlick(ReferenceContext context)
        {
            var root=new GameObject("Rendered tongue visibility check");
            var cameraObject=new GameObject("Tongue check camera"); var camera=cameraObject.AddComponent<Camera>();
            camera.orthographic=true; camera.orthographicSize=64; camera.aspect=2;
            camera.transform.position=new Vector3(0,0,-10); camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=new Color(.22f,.25f,.27f,1);
            var rt=new RenderTexture(256,128,24); camera.targetTexture=rt;
            var pixels=new Texture2D(256,128,TextureFormat.RGBA32,false);
            var previous=RenderTexture.active; var view=new SpriteMeshView(root.transform);
            using(var pet=context.CreatePet("white"))
            {
                try
                {
                    pet.Generate("2455"); pet.Pose(0,.7f); pet.Draw(); view.Refresh(pet);
                    camera.Render(); RenderTexture.active=rt; pixels.ReadPixels(new Rect(0,0,256,128),0,0); pixels.Apply();
                    var before=pixels.GetPixels32(); var tongue=new PetTongue(); var mouth=pet.HeadPosition+Vector2.right*10;
                    Check(tongue.TryStartCurious(mouth,Vector2.right,mouth+Vector2.right*100,50),"Visibility fixture did not start");
                    for(int frame=0;frame<43;frame++) tongue.Advance(.025f,mouth,Vector2.right,null,null);
                    Check(tongue.Active,"Flick finished before visible sample");
                    TongueGraphics.Apply(pet,tongue); pet.Draw(); view.Refresh(pet);
                    camera.Render(); RenderTexture.active=rt; pixels.ReadPixels(new Rect(0,0,256,128),0,0); pixels.Apply();
                    var after=pixels.GetPixels32(); int changed=0;
                    for(int i=0;i<before.Length;i++) if(Mathf.Abs(before[i].r-after[i].r)+Mathf.Abs(before[i].g-after[i].g)+Mathf.Abs(before[i].b-after[i].b)>30) changed++;
                    Check(changed>30,"Tongue state changed but its pixels were not visible");
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(System.IO.Path.GetTempPath(),"lizor-curious-tongue.png"),pixels.EncodeToPNG());
                    Debug.Log("CURIOUS_TONGUE_VISIBLE_PIXELS="+changed);
                }
                finally { view.Dispose(); RenderTexture.active=previous; camera.targetTexture=null; UnityEngine.Object.DestroyImmediate(pixels); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(cameraObject); UnityEngine.Object.DestroyImmediate(root); }
            }
        }
        public static void Run(ReferenceContext context)
        {
            var food=new SharedFood(); food.Begin(); food.Place(new Vector2(180,0));
            var tongue=new PetTongue();
            Check(!tongue.TryStart(Vector2.zero,Vector2.left,food.Position),"Tongue fired backwards");
            Check(!tongue.TryStart(Vector2.zero,Vector2.right,new Vector2(500,0)),"Tongue exceeded attack range");
            Check(tongue.TryStart(Vector2.zero,Vector2.right,food.Position),"Tongue did not prepare");
            bool caught=false,delivered=false;
            using(var pet=context.CreatePet("white"))
            {
                pet.Generate("24556");
                for(int i=0;i<200;i++)
                {
                    tongue.Advance(.025f,Vector2.zero,Vector2.right,food.Available?(Vector2?)food.Position:null,()=>food.TakeTongue("a"));
                    if(i<39) Check(tongue.Stage==TongueStage.Aiming,"Tongue fired before windup");
                    if(tongue.Holding)
                    {
                        caught=true; Check(!food.Take("b")&&!food.TakeTongue("b")&&!food.TakeCursor(),"Tongue catch was stolen");
                        food.Move(tongue.Tip);
                    }
                    TongueGraphics.Apply(pet,tongue); pet.Draw();
                    foreach(var point in tongue.Points) Check(!float.IsNaN(point.x)&&!float.IsInfinity(point.y),"Tongue mesh invalid");
                    if(tongue.Delivered) { delivered=true; Check(food.SwallowTongue("a"),"Tongue handoff failed"); break; }
                }
            }
            Check(caught&&delivered&&food.Owner==FoodOwner.Pet,"Tongue did not deliver food");
            Check(!tongue.TryStart(Vector2.zero,Vector2.right,new Vector2(180,0)),"Tongue ignored cooldown");
            // Cursor movement before impact can produce a real miss, with bounded retraction.
            var miss=new PetTongue(); miss.TryStart(Vector2.zero,Vector2.right,new Vector2(180,0));
            for(int i=0;i<200;i++) miss.Advance(.025f,Vector2.zero,Vector2.right,i<40?new Vector2(180,0):new Vector2(180,180),()=>{throw new Exception("Miss hit displaced food");});
            Check(!miss.Active&&!miss.Holding,"Miss did not retract");
            var cancel=new PetTongue(); cancel.TryStart(Vector2.zero,Vector2.right,new Vector2(180,0));
            cancel.Advance(.025f,Vector2.zero,Vector2.right,null,null); Check(!cancel.Active,"Lost target did not cancel preparation");
            food.Drop(Vector2.zero); Check(food.TakeTongue("a"),"Could not reserve after drop");
            food.Remove("a",Vector2.one); Check(food.Available,"Removed pet stranded tongue food");
            cancel.Cancel(); cancel.Advance(float.NaN,Vector2.zero,Vector2.right,null,null); Check(!cancel.Active,"Invalid time activated tongue");
            TurnBeforeShot(context);
            VisibleFlick(context);
            Curious(context);
            HostCatch(context);
            Debug.Log("WHITE_TONGUE_CHECKS_PASSED");
        }
    }
}
