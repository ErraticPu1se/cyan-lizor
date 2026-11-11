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
            var fight=new PetScuffle();
            for(int i=0;i<105;i++) fight.Advance(.1f,true,true,40,true,1,0);
            Check(fight.Phase==ScufflePhase.Cooldown,"Unbounded fight or missing cooldown");
            for(int i=0;i<20;i++) fight.Advance(.1f,true,true,40,true,1,0);
            Check(!fight.Active,"Fight restarted before cooldown");
            var interrupted=new PetScuffle();
            for(int i=0;i<20;i++) interrupted.Advance(.1f,true,true,40,true,1,0);
            Check(interrupted.Active,"Fight did not start"); interrupted.Advance(.1f,false,true,40,true,1,0);
            Check(!interrupted.Active,"Turning off fighting left attack active");
            var grabbed=new PetScuffle(); for(int i=0;i<20;i++) grabbed.Advance(.1f,true,true,40,true,1,0);
            grabbed.Advance(.1f,true,false,40,true,1,0); Check(!grabbed.Active,"Grab failed to interrupt fight");
            float tension=low.Tension; low.Advance(float.NaN,true,true,20,true,1,0); low.Advance(0,true,true,20,true,1,0);
            Check(low.Tension==tension,"Invalid or paused time advanced fight");
            var manual=new PetScuffle();
            Check(manual.TryStartManual() && !manual.TryStartManual(),"Manual start duplicated an active encounter");
            manual.Advance(0,true,true,400,false,0,0); Check(manual.Phase==ScufflePhase.Approaching,"Paused approach advanced");
            manual.Advance(.1f,true,true,50,false,0,0); Check(manual.Phase==ScufflePhase.Posture,"Manual approach did not reach posture");

            for(int i=0;i<90;i++) manual.Advance(.1f,true,true,40,false,0,0);
            Check(manual.Phase==ScufflePhase.Cooldown && !manual.TryStartManual(),"Manual encounter bypassed duration limit/cooldown");
            var unreachable=new PetScuffle(); unreachable.TryStartManual();
            for(int i=0;i<210;i++) unreachable.Advance(.1f,true,true,500,false,0,0);
            Check(!unreachable.Active && !string.IsNullOrEmpty(unreachable.Outcome),"Unreachable manual pair had no timeout feedback");
            var cancelled=new PetScuffle(); cancelled.TryStartManual(); cancelled.Advance(.1f,true,false,100,false,0,0);
            Check(!cancelled.Active,"Manual approach survived a grab/unavailable pet");
            var status=new PetSocialStatus {state="Calm"}; var displayFight=new PetScuffle();
            for(int i=0;i<21;i++) displayFight.Advance(.1f,true,true,40,true,1,0);
            DesktopApp.UpdateSocialStatus(status,displayFight,"Other pet",true);
            Check(status.active && status.state=="Posture" && status.rival=="Other pet","Indicator did not expose actual posture state");
            DesktopApp.UpdateSocialStatus(status,new PetScuffle(),"Bystander",true);
            Check(status.active && status.rival=="Other pet","Bystander overwrote active rivalry indicator");
            displayFight.Interrupt(); status=new PetSocialStatus {state="Calm"};
            DesktopApp.UpdateSocialStatus(status,displayFight,"Other pet",true);
            Check(!status.active && status.state=="Cooldown","Indicator did not show interrupted fight cooldown");
            var pursuit=new PetScuffle();
            pursuit.Advance(0,true,true,100,true,1,0,true);
            pursuit.Advance(float.NaN,true,true,100,true,1,0,true);
            pursuit.Advance(.025f,false,true,100,true,1,0,true);
            pursuit.Advance(.025f,true,false,100,true,1,0,true);
            Check(!pursuit.Active,"Invalid, disabled or unavailable carrier chase started");
            pursuit.Advance(.025f,true,true,300,true,0,0,true);
            Check(pursuit.Active,"Food carrier still waited for tension buildup");
            pursuit.Interrupt(); pursuit.Advance(.025f,true,true,30,true,1,0,true);
            Check(!pursuit.Active,"Food competition bypassed combat cooldown");
            Debug.Log("MULTIPLE_PET_ROSTER_OWNERSHIP_RIVALRY_CHECK_PASSED");
        }
        public static void CarrierCompetition(ReferenceContext context)
        {
            var root=new GameObject("Automatic carrier competition");
            var host=root.AddComponent<DesktopApp>();
            var native=new WindowsDesktop(System.Diagnostics.Process.GetCurrentProcess().Id);
            var area=new Rect(-native.WorkArea.width/2,-native.WorkArea.height/2,native.WorkArea.width,native.WorkArea.height);
            var settings=new DesktopSettings {allowFighting=true};
            Set(host,"native",native); Set(host,"settings",settings);
            var pets=(System.Collections.Generic.List<DesktopPet>)Get(host,"pets");
            var food=(SharedFood)Get(host,"food");
            var pairs=(System.Collections.Generic.Dictionary<string,PetScuffle>)Get(host,"scuffles");
            try
            {
                for(int i=0;i<3;i++)
                {
                    var config=DesktopPetConfiguration.From(new DesktopSettings {id="9320",scale=1,sound=false,roam=false,jumps=true},"chase-"+i);
                    var pet=new DesktopPet(context,root.transform,config,area,i); pets.Add(pet);
                    pet.Motion.ShiftWithSurface(new Vector2(-200+i*200,0)-pet.Motion.Position);
                }
                var challenger=pets[0]; var carrier=pets[2];
                food.Begin(); food.Place(carrier.WorldPosition);
                foreach(var pet in pets) pet.FoodRevision=food.Revision;
                carrier.Feeding.Begin(); carrier.Feeding.Place(carrier.Motion.Position,carrier.Surfaces.Bounds);
                Call(challenger.Motion,"BeginJump",carrier.Motion.Position,.5f);
                Call(host,"AdvanceFeeding",.025f);
                Check(food.Holder==carrier.Config.key,"Automatic competition pickup fixture failed");
                Check(challenger.Motion.State==PetMotionState.Charging,"Pickup cancelled challenger charge");
                Call(host,"AdvanceScuffles",.025f);
                Check(challenger.Motion.RivalTarget.HasValue,"Pickup did not immediately start automatic carrier pursuit");
                Check(!pets[1].Motion.RivalTarget.HasValue,"Non-carrier pair stole food rivalry priority");
                foreach(var attackerState in new[]{PetMotionState.Charging,PetMotionState.Airborne,PetMotionState.Crawling})
                    foreach(var carrierState in new[]{PetMotionState.Charging,PetMotionState.Airborne,PetMotionState.Crawling})
                    {
                        Call(challenger.Motion,"ChangeState",attackerState); Call(carrier.Motion,"ChangeState",carrierState);
                        carrier.Motion.ShiftWithSurface(Vector2.right*3);
                        Call(host,"AdvanceFeeding",.025f); Call(host,"AdvanceScuffles",.025f);
                        Check(challenger.Motion.RivalTarget.HasValue && challenger.Motion.Behavior.TaskTarget==challenger.Local(PetContacts.TargetPoint(challenger,carrier)),"Flight transition lost moving body target");
                        Check(!challenger.Motion.Behavior.HoldPosition && challenger.Motion.AutonomousJumps,"Pursuit was held or jump preference disabled");
                        Check(carrier.Motion.Behavior.TaskTarget==carrier.Feeding.Target && !carrier.Motion.RivalTarget.HasValue,"Chase overwrote carrier escape route");
                        Check(!carrier.Bite.Lunging && !carrier.Motion.RivalBiting,"Food carrier attacked");
                    }
                // The production social/feeding/motion sequence must actually advance a grounded challenger.
                challenger.Config.jumps=false;
                var start=challenger.WorldPosition;
                for(int i=0;i<100;i++)
                {
                    Call(host,"AdvanceFeeding",.025f); Call(host,"AdvanceScuffles",.025f);
                    challenger.Advance(.025f,false);
                }
                Check(Vector2.Distance(challenger.WorldPosition,carrier.WorldPosition)<Vector2.Distance(start,carrier.WorldPosition)-20,"Automatic chase exposed a target without approaching it");
                Check(!challenger.Motion.AutonomousJumps,"Chase ignored disabled jumping");
                settings.allowFighting=false; Call(host,"AdvanceScuffles",.025f); Call(host,"AdvanceFeeding",.025f);
                Check(pets.All(p=>!p.Motion.RivalTarget.HasValue) && challenger.FollowingFood,"Fighting off did not restore peaceful food following");
                settings.allowFighting=true; pairs.Clear(); Call(host,"AdvanceScuffles",.025f);
                challenger.Motion.BeginGrab(challenger.Motion.Position); Call(host,"AdvanceScuffles",.025f);
                Check(!challenger.Motion.RivalTarget.HasValue,"Grabbed challenger retained combat");
                challenger.Motion.EndGrab(); pairs.Clear();
                settings.paused=true; Call(host,"AdvanceScuffles",0f);
                Check(pets.All(p=>!p.Motion.RivalTarget.HasValue),"Paused pets pursued food");
                settings.paused=false; Call(host,"AdvanceScuffles",.025f);
                food.EnterDen(carrier.Config.key); carrier.Feeding.Cancel();
                // Den route makes the carrier unavailable even while still partially visible.
                carrier.Feeding.Begin(); carrier.Feeding.Place(carrier.Motion.Position,carrier.Surfaces.Bounds);
                carrier.Feeding.Advance(.025f,carrier.Motion.Position,carrier.Motion.MouthPosition,false,carrier.Surfaces.Bounds);
                carrier.Feeding.Advance(.025f,carrier.Feeding.Entrance,carrier.Motion.MouthPosition,false,carrier.Surfaces.Bounds);
                Call(host,"AdvanceScuffles",.025f); Call(host,"AdvanceFeeding",.025f);
                Check(pets.Take(2).All(p=>!p.Motion.RivalTarget.HasValue && !p.Motion.Behavior.TaskTarget.HasValue),"Challengers followed carrier into den");
                food.Cancel(); carrier.Feeding.Cancel(); pairs.Clear();
                food.Begin(); food.Place(carrier.WorldPosition); food.TakeCursor();
                Call(host,"AdvanceScuffles",.025f);
                Check(pets.All(p=>!p.Motion.RivalTarget.HasValue),"Cursor-held food started carrier attack");
                food.Drop(carrier.WorldPosition); food.Take(carrier.Config.key); pairs.Clear();
                food.Remove(carrier.Config.key,carrier.WorldPosition); pets.Remove(carrier); carrier.Dispose();
                Call(host,"AdvanceScuffles",.025f);
                Check(pets.All(p=>!p.Motion.RivalTarget.HasValue) && food.Available,"Removed carrier stranded combat or food");
                Debug.Log("AUTOMATIC_FOOD_CARRIER_CHASE_FLIGHT_TRANSITIONS_CHECK_PASSED");
            }
            finally { foreach(var pet in pets) pet.Dispose(); pets.Clear(); native.Dispose(); UnityEngine.Object.DestroyImmediate(root); }
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
                var hostSettings=(DesktopSettings)Get(host,"settings");
                hostSettings.allowFighting=true; hostSettings.scuffleFirst="pet-0"; hostSettings.scuffleSecond="pet-0"; hostSettings.scuffleRequest=1;
                Call(host,"ProcessScuffleRequest"); Check(Get(host,"manualPair")==null,"Host accepted same-pet fight");
                hostSettings.scuffleSecond="missing"; hostSettings.scuffleRequest++;
                Call(host,"ProcessScuffleRequest"); Check(Get(host,"manualPair")==null,"Host accepted removed selection");
                hostSettings.scuffleSecond="pet-1"; hostSettings.paused=true; hostSettings.scuffleRequest++;
                Call(host,"ProcessScuffleRequest"); Check(Get(host,"manualPair")==null,"Host accepted fight while paused");
                hostSettings.paused=false; hostSettings.scuffleRequest++;
                Call(host,"ProcessScuffleRequest"); var selectedPair=Get(host,"manualPair"); Check(selectedPair!=null,"Valid manual pair rejected");
                Call(host,"ProcessScuffleRequest"); Check(Equals(selectedPair,Get(host,"manualPair")),"Polling replayed manual request");
                hostSettings.scuffleRequest++; Call(host,"ProcessScuffleRequest"); Check(Equals(selectedPair,Get(host,"manualPair")),"Repeated button press replaced encounter");
                Call(host,"AdvanceScuffles",.025f);
                Check(!list[2].Motion.RivalTarget.HasValue,"Third pet joined selected manual pair");
                hostSettings.paused=true; Call(host,"AdvanceScuffles",0f);
                Check(Get(host,"manualPair")==null && list.All(p=>!p.Motion.RivalTarget.HasValue),"Pause did not cancel manual scuffle");
                hostSettings.paused=false;
                var encounters=(System.Collections.Generic.Dictionary<string,PetScuffle>)Get(host,"scuffles"); encounters.Clear();
                list[0].Motion.BeginGrab(list[0].Motion.Position); hostSettings.scuffleRequest++;
                Call(host,"ProcessScuffleRequest"); Check(Get(host,"manualPair")==null,"Held pet entered manual fight");
                list[0].Motion.EndGrab(); hostSettings.scuffleRequest++;
                Call(host,"ProcessScuffleRequest"); Check(Get(host,"manualPair")==null,"Airborne pet entered manual fight");
                Call(list[0].Motion,"ChangeState",PetMotionState.Crawling);
                hostSettings.scuffleRequest++; Call(host,"ProcessScuffleRequest"); Check(Get(host,"manualPair")!=null,"Pair failed to restart after cancellation");
                // SyncPets clears encounters whenever a participant is removed or rebuilt.
                encounters.Clear(); Call(host,"AdvanceScuffles",.025f);
                Check(Get(host,"manualPair")==null && ((string)Get(host,"manualMessage")).Contains("removed"),"Roster change left manual encounter pending");
                encounters.Clear(); hostSettings.scuffleRequest++;
                food.Begin(); food.Place(list[1].WorldPosition); food.Take(list[1].Config.key);
                list[1].Feeding.Begin(); list[1].Feeding.Place(list[1].Motion.Position,list[1].Surfaces.Bounds,list[1].Motion.HeadToTailLength);
                list[1].Feeding.Advance(.025f,list[1].Motion.Position,list[1].Motion.MouthPosition,false,list[1].Surfaces.Bounds);
                Call(host,"ProcessScuffleRequest");
                foreach(var carrierState in new[]{PetMotionState.Charging,PetMotionState.Airborne,PetMotionState.Crawling})
                {
                    Call(list[1].Motion,"ChangeState",carrierState);
                    Call(host,"AdvanceScuffles",.025f);
                    Check(list[0].Motion.RivalTarget.HasValue && Get(host,"manualPair")!=null,"Carrier "+carrierState+" cancelled challenger target");
                    Check(list[0].Motion.Behavior.TaskTarget==list[0].Local(PetContacts.TargetPoint(list[0],list[1])),"Carrier transition lost body pursuit target");
                    Check(!list[1].Bite.Lunging && !list[1].Motion.RivalBiting,"Mouth-occupied carrier attacked during transition");
                }
                list[1].Feeding.Advance(.025f,list[1].Feeding.Entrance,list[1].Motion.MouthPosition,false,list[1].Surfaces.Bounds,true);
                Check(list[1].Feeding.UsingRoute,"Den-entry fixture did not commit its route");
                Call(host,"AdvanceScuffles",.025f);
                Check(!list[0].Motion.RivalTarget.HasValue && Get(host,"manualPair")==null,"Carrier den entry retained pursuit");
                list[1].Feeding.Cancel(); encounters.Clear(); hostSettings.scuffleRequest++;
                Call(host,"ProcessScuffleRequest");
                Call(list[1].Motion,"ChangeState",PetMotionState.Airborne);
                Call(host,"AdvanceScuffles",.025f);
                Check(list[0].Motion.RivalTarget.HasValue,"Airborne carrier could not resume after reset");
                food.Drop(list[1].WorldMouth); Call(host,"AdvanceScuffles",.025f);
                Check(!list[0].Motion.RivalTarget.HasValue,"Airborne non-carrier retained special pursuit eligibility");
                Call(list[1].Motion,"ChangeState",PetMotionState.Crawling);
                food.Cancel(); foreach(var p in list) Call(host,"ResetInterest",p);
                hostSettings.allowFighting=false; encounters.Clear();
                food.Begin(); food.Place(new Vector2(300,0));
                for(int i=0;i<60;i++) Call(host,"AdvanceFeeding",.1f);
                foreach(var p in list) p.Motion.ShiftWithSurface(p.Local(food.Position)-p.Motion.Position);
                foreach(var follower in list.Skip(1)) { follower.Config.jumps=true; Call(follower.Motion,"BeginJump",new Vector2(400,200),.5f); }
                Call(host,"AdvanceFeeding",.025f);
                Check(list.Skip(1).All(p=>p.Motion.State==PetMotionState.Crawling && !p.Motion.AutonomousJumps),"Pickup retained follower charge or jump permission");
                foreach(var follower in list.Skip(1)) follower.Configure(follower.Config,false);
                Check(list.Skip(1).All(p=>!p.Motion.AutonomousJumps),"Settings refresh re-enabled follower jumps");
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
                foreach(var p in list) p.Motion.ShiftWithSurface(-p.Motion.Position);
                for(int i=0;i<60;i++) Call(host,"AdvanceFeeding",.1f);
                Check(food.Owner==FoodOwner.Pet,"Dropped morsel not picked up");
                carrier=list.Find(p=>p.Config.key==food.Holder);
                carrier.Motion.ShiftWithSurface(carrier.Feeding.Entrance-carrier.Motion.Position);
                Call(host,"AdvanceFeeding",.025f);
                for(int i=0;i<400 && food.Owner==FoodOwner.Pet;i++) { carrier.Advance(.025f,false); Call(host,"AdvanceFeeding",.025f); }
                Check(food.Owner==FoodOwner.Den,"Den entry did not commit ownership");
                Check(list.Where(p=>p!=carrier).All(p=>!p.FollowingFood && !p.Feeding.Active && !p.Motion.Behavior.TaskTarget.HasValue),"Rivals pursued food into den");
                var followerAfterDen=list.First(p=>p!=carrier);
                Call(followerAfterDen.Motion,"BeginJump",new Vector2(400,200),.5f);
                Call(host,"ResetInterest",followerAfterDen);
                Check(followerAfterDen.Motion.State==PetMotionState.Crawling,"Pursuit cancellation left a pending charge");
                followerAfterDen.Motion.BeginGrab(followerAfterDen.Motion.Position); followerAfterDen.Motion.EndGrab();
                Call(host,"ResetInterest",followerAfterDen);
                Check(followerAfterDen.Motion.State==PetMotionState.Airborne,"Cancelling pursuit snapped an airborne pet to ground");
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
                Call(a.Motion,"ChangeState",PetMotionState.Crawling);
                var body=(Array)Get(a.Pet.Lizard,"bodyChunks");
                foreach(var chunk in body) Set(chunk,"vel",Vector2.zero);
                Check(a.Motion.BiteToward(a.Motion.Position+Vector2.right*30),"Bite impulse rejected");
                Check(((Vector2)Get(body.GetValue(0),"vel")).x>0 && ((Vector2)Get(body.GetValue(1),"vel")).x<0 && ((Vector2)Get(body.GetValue(2),"vel")).x<0,"Bite moved whole body rigidly instead of opposing front/rear forces");
                Check(!a.Motion.BiteToward(new Vector2(float.NaN,0)),"Invalid bite target accepted");
                a.Motion.BeginGrab(a.Motion.Position); Check(!a.Motion.BiteToward(a.Motion.Position+Vector2.right*30),"Held pet attacked"); a.Motion.EndGrab();
                Call(a.Motion,"ChangeState",PetMotionState.Crawling);
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
