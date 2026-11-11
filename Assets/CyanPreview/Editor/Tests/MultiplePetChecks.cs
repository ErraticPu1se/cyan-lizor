using System;
using System.Linq;
using System.IO;
using UnityEngine;
using static CyanPreview.ReferenceContext;
namespace CyanPreview.Editor
{
    public static class MultiplePetChecks
    {
        static void Check(bool value,string message) { if(!value) throw new Exception(message); }
        static void Reject(Action action,string message) { bool rejected=false; try { action(); } catch(InvalidDataException) { rejected=true; } Check(rejected,message); }
        public static void Run()
        {
            var legacy=new DesktopSettings {id="9320",scale=1.5f,sound=false,roam=false};
            legacy.ValidateRoster(); Check(legacy.Roster.Length==1 && !legacy.Roster[0].sound && !legacy.Roster[0].roam && legacy.Roster[0].scale==1.5f,"Legacy preferences were changed");
            var a=DesktopPetConfiguration.From(legacy,"a"); var b=DesktopPetConfiguration.From(legacy,"b");
            legacy.pets=new[]{a,b}; legacy.ValidateRoster();
            legacy.pets=new[]{a,a}; Reject(legacy.ValidateRoster,"Duplicate runtime IDs accepted");
            legacy.pets=new DesktopPetConfiguration[0]; Reject(legacy.ValidateRoster,"Empty roster accepted");
            legacy.pets=new[]{a,b,DesktopPetConfiguration.From(legacy,"c"),DesktopPetConfiguration.From(legacy,"d")}; Reject(legacy.ValidateRoster,"Pet cap bypassed");
            legacy.pets=new[]{a}; a.scale=float.NaN; Reject(legacy.ValidateRoster,"NaN scale accepted"); a.scale=1;
            a.species="white"; Reject(legacy.ValidateRoster,"Unsupported species accepted"); a.species="cyan";
            a.key="bad|key"; Reject(legacy.ValidateRoster,"Menu identity separator accepted");
            var food=new SharedFood(); Check(food.Begin() && !food.Begin(),"Duplicate morsel placement"); food.Place(Vector2.one);
            Check(food.Take("a") && !food.Take("b") && !food.TakeCursor(),"Two holders acquired one morsel");
            food.Remove("b",Vector2.zero); Check(food.Holder=="a","Removing bystander dropped food");
            food.Remove("a",Vector2.zero); Check(food.Available,"Removing carrier stranded food");
            Check(food.TakeCursor() && !food.Take("a"),"Pet stole cursor-held food");
            food.Drop(Vector2.one*5); Check(food.Take("b"),"Release did not restore availability");
            Check(!food.EnterDen("a") && food.EnterDen("b") && !food.Take("a") && !food.TakeCursor(),"Den commitment not exclusive");
            food.Remove("b",Vector2.zero); Check(food.Owner==FoodOwner.None,"Removed hidden carrier left food stuck");
            food.Begin(); food.Cancel(); Check(food.Owner==FoodOwner.None && food.Holder==null,"Cancelled placement retained ownership");
            for(int round=0;round<50;round++)
            {
                food.Begin(); food.Place(Vector2.zero); int winners=0;
                foreach(string key in new[]{"a","b","c"}) if(food.Take(key)) winners++;
                Check(winners==1,"Same-frame pickup has multiple winners"); food.Cancel();
            }
            var low=new PetScuffle(); var high=new PetScuffle();
            for(int i=0;i<100;i++) { low.Advance(.1f,true,true,40,false,0,0); high.Advance(.1f,true,true,40,false,1,0); }
            Check(high.Tension>low.Tension,"Personality has no rivalry effect");
            var fight=new PetScuffle(); int bites=0;
            for(int i=0;i<100;i++) { fight.Advance(.1f,true,true,40,true,1,0); if(fight.BiteStarted) bites++; }
            Check(bites==2 && fight.Phase==ScufflePhase.Cooldown,"Unbounded fight or missing cooldown");
            for(int i=0;i<20;i++) fight.Advance(.1f,true,true,40,true,1,0);
            Check(!fight.Active,"Fight restarted before cooldown");
            var interrupted=new PetScuffle();
            for(int i=0;i<20;i++) interrupted.Advance(.1f,true,true,40,true,1,0);
            Check(interrupted.Active,"Fight did not start"); interrupted.Advance(.1f,false,true,40,true,1,0);
            Check(!interrupted.Active && !interrupted.BiteStarted,"Turning off fighting left attack active");
            var grabbed=new PetScuffle(); for(int i=0;i<20;i++) grabbed.Advance(.1f,true,true,40,true,1,0);
            grabbed.Advance(.1f,true,false,40,true,1,0); Check(!grabbed.Active,"Grab failed to interrupt fight");
            float tension=low.Tension; low.Advance(float.NaN,true,true,20,true,1,0); low.Advance(0,true,true,20,true,1,0);
            Check(low.Tension==tension,"Invalid or paused time advanced fight");
            Debug.Log("MULTIPLE_PET_ROSTER_OWNERSHIP_RIVALRY_CHECK_PASSED");
        }
        public static void SharedFeeding(ReferenceContext context)
        {
            var root=new GameObject("Shared feeding integration");
            var host=root.AddComponent<DesktopApp>();
            var native=new WindowsDesktop(System.Diagnostics.Process.GetCurrentProcess().Id);
            var area=new Rect(-native.WorkArea.width/2,-native.WorkArea.height/2,native.WorkArea.width,native.WorkArea.height);
            Set(host,"native",native); Set(host,"settings",new DesktopSettings());
            var list=(System.Collections.Generic.List<DesktopPet>)Get(host,"pets");
            var food=(SharedFood)Get(host,"food");
            try
            {
                for(int i=0;i<3;i++)
                {
                    var config=DesktopPetConfiguration.From(new DesktopSettings {id="9320",sound=false,roam=false,jumps=false},"pet-"+i);
                    list.Add(new DesktopPet(context,root.transform,config,area,i));
                }
                food.Begin(); food.Place(Vector2.zero);
                for(int i=0;i<60;i++) Call(host,"AdvanceFeeding",.1f);
                foreach(var p in list) p.Motion.ShiftWithSurface(-p.Motion.Position);
                Call(host,"AdvanceFeeding",.025f);
                Check(food.Owner==FoodOwner.Pet && list.Count(p=>p.Feeding.Stage==FeedingStage.Carrying)==1,"Host granted two mouth holders");
                var carrier=list.Find(p=>p.Config.key==food.Holder);
                carrier.Motion.BeginGrab(carrier.Motion.Position);
                Call(host,"AdvanceFeeding",.025f);
                Check(food.Holder!=carrier.Config.key && carrier.Feeding.Stage!=FeedingStage.Carrying,"Grabbed carrier kept ownership");
                carrier.Motion.EndGrab(); food.Cancel();
                foreach(var p in list) Call(host,"ResetInterest",p);
                food.Begin(); food.Place(Vector2.zero); food.TakeCursor();
                for(int i=0;i<70;i++) Call(host,"AdvanceFeeding",.1f);
                Check(food.Owner==FoodOwner.Cursor && list.All(p=>p.Feeding.Stage!=FeedingStage.Carrying),"Host let pet steal cursor food");
                food.Drop(Vector2.zero);
                for(int i=0;i<60;i++) Call(host,"AdvanceFeeding",.1f);
                Check(food.Owner==FoodOwner.Pet,"Dropped morsel not picked up");
                carrier=list.Find(p=>p.Config.key==food.Holder);
                carrier.Motion.ShiftWithSurface(carrier.Feeding.Entrance-carrier.Motion.Position);
                Call(host,"AdvanceFeeding",.025f);
                for(int i=0;i<400 && food.Owner==FoodOwner.Pet;i++) { carrier.Advance(.025f,false); Call(host,"AdvanceFeeding",.025f); }
                Check(food.Owner==FoodOwner.Den,"Den entry did not commit ownership");
                Check(list.Where(p=>p!=carrier).All(p=>!p.FollowingFood && !p.Feeding.Active && !p.Motion.Behavior.TaskTarget.HasValue),"Rivals pursued food into den");
                food.Cancel();
                Call(host,"BeginFeeding"); Check(food.Owner==FoodOwner.None,"New placement interrupted returning pet");
                Debug.Log("MULTIPLE_PET_SHARED_FEEDING_HOST_CHECK_PASSED");
            }
            finally { foreach(var p in list) p.Dispose(); list.Clear(); native.Dispose(); UnityEngine.Object.DestroyImmediate(root); }
        }
        public static void Motion(ReferenceContext context)
        {
            var root=new GameObject("Multiple-pet checks"); var area=new Rect(-700,-400,1400,800);
            DesktopPet a=null,b=null,c=null;
            try
            {
                var config=new DesktopSettings {id="9320",sound=false,roam=false,jumps=false};
                a=new DesktopPet(context,root.transform,DesktopPetConfiguration.From(config,"a"),area,0);
                b=new DesktopPet(context,root.transform,DesktopPetConfiguration.From(config,"b"),area,1);
                c=new DesktopPet(context,root.transform,DesktopPetConfiguration.From(new DesktopSettings {id="3616",scale=2,sound=false},"c"),area,2);
                Check(a.Pet.PetId!=b.Pet.PetId && a.Pet.Summary==b.Pet.Summary,"Duplicate appearance instances not independent");
                var untouched=b.Motion.Position;
                a.Input.Press(a.Motion.Position); a.Input.Hold(a.Motion.Position+new Vector2(100,150),.11f);
                for(int i=0;i<30;i++) a.Advance(.025f,false);
                Check(a.Motion.State==PetMotionState.Grabbed && b.Motion.Position==untouched,"Grabbing one pet moved another");
                a.Input.Release(a.Motion.Position,true);
                for(int i=0;i<50;i++) { a.Advance(.025f,false); b.Advance(.025f,false); c.Advance(.025f,false); }
                // Original graphics animation with our bounded impulses; never invoke a game attack.
                a.Motion.RivalTarget=b.Motion.Position; a.Motion.RivalBiting=true;
                var state=context.Blank("LizardState"); Set(state,"health",1f); Set(Get(a.Pet.Lizard,"abstractPhysicalObject"),"state",state); var health=Get(state,"health"); var dead=Get(a.Pet.Lizard,"dead");
                for(int i=0;i<80;i++) { a.Motion.ScuffleImpulse(Vector2.right*4); a.Advance(.025f,false); }
                Check(Equals(health,Get(state,"health")) && Equals(dead,Get(a.Pet.Lizard,"dead")),"Scuffle injured pet");
                b.Dispose(); b=null;
                for(int i=0;i<20;i++) { a.Advance(.025f,false); c.Advance(.025f,false); }
                Check(a.Pet.OrderedSprites.Any() && c.Pet.OrderedSprites.Any(),"Removing pet invalidated shared rendering");
                Check(c.Root.transform.localScale.x==2 && a.Root.transform.localScale.x==1.2f,"Per-pet scale leaked");
                foreach(var mesh in root.GetComponentsInChildren<MeshFilter>()) foreach(var v in mesh.sharedMesh.vertices)
                    Check(!float.IsNaN(v.x) && !float.IsInfinity(v.y),"Invalid multi-pet mesh");
                Debug.Log("MULTIPLE_PET_GRAB_SCALE_REMOVAL_NONLETHAL_CHECK_PASSED");
            }
            finally { a?.Dispose(); b?.Dispose(); c?.Dispose(); UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
