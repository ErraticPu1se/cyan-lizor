using System;
using System.Collections.Generic;
using UnityEngine;
using static CyanPreview.ReferenceContext;
namespace CyanPreview.Editor
{
    public static class CombatApproachChecks
    {
        static void Check(bool value,string message) { if(!value) throw new Exception(message); }
        public static void Verify() { using(var c=new ReferenceContext(System.IO.Path.GetFullPath("local-data"))) Run(c); }
        public static void Run(ReferenceContext context)
        {
            var tuning=new BiteTuning(25,0,90,1.4f,1,12);
            int referenceHomes=-1;
            foreach(float dt in new[]{.0125f,.025f,.1f})
            {
                var bite=new PetBite(3,tuning); int homes=0;
                for(int i=0;i<Mathf.RoundToInt(.6f/dt);i++)
                { var result=bite.Advance(dt,true,true,true,false,true,true,true); if(result==BiteDecision.Home) homes+=bite.HomingTicks; Check(result!=BiteDecision.Hit,"Homing manufactured contact"); }
                Debug.Log("HOMING_TICKS dt="+dt+" count="+homes);
                Check(homes>0 && (referenceHomes<0 || homes==referenceHomes),"Homing strength depended on render frame rate"); referenceHomes=homes;
                Check(bite.Advance(.025f,true,true,true,true,true,true,true)==BiteDecision.Hit,"Homing prevented contact bite");
                Check(bite.HomingTicks==0,"Hit also applied homing");
                for(int i=0;i<10;i++) Check(bite.Advance(.025f,true,true,true,false,true,true,true)==BiteDecision.None,"Cooldown allowed homing");
                bite.ReceiveHit(); Check(bite.Advance(.025f,true,true,true,false,true,true,true)==BiteDecision.None,"Hit recovery allowed homing");
            }
            foreach(string condition in new[]{"far","unaligned","airborne","occupied","disabled"})
            {
                var bite=new PetBite(3,tuning);
                for(int i=0;i<25;i++) Check(bite.Advance(.025f,condition!="disabled",condition!="occupied",true,false,condition!="unaligned",condition!="airborne",condition!="far")!=BiteDecision.Home,"Unsafe homing: "+condition);
            }
            var root=new GameObject("Combat approach regression"); var host=root.AddComponent<DesktopApp>();
            var pets=(List<DesktopPet>)Get(host,"pets"); var native=new WindowsDesktop(System.Diagnostics.Process.GetCurrentProcess().Id);
            Set(host,"native",native); Set(host,"settings",new DesktopSettings {allowFighting=false});
            try
            {
                foreach(float scale in new[]{1f,2f})
                {
                    var config=new DesktopSettings {id="9320",scale=scale,sound=false,roam=false,jumps=false,climbingMode=ClimbingMode.EdgesAndBackgrounds};
                    var area=new Rect(-700,-400,1400,800);
                    var a=new DesktopPet(context,root.transform,DesktopPetConfiguration.From(config,"a"),area,0); pets.Add(a);
                    var b=new DesktopPet(context,root.transform,DesktopPetConfiguration.From(config,"b"),area,1); pets.Add(b);
                    Pose(a,Vector2.zero,Vector2.right); Pose(b,new Vector2(100,0),Vector2.left);
                    Check(PetContacts.ApproachSpeed(a,b)==.2f,"Frontal confrontation did not slow");
                    Pose(b,new Vector2(100,0),Vector2.right); Check(PetContacts.ApproachSpeed(a,b)==.8f,"Rear pursuit did not close distance");
                    Pose(b,new Vector2(240,0),Vector2.left); Check(PetContacts.ApproachSpeed(a,b)==.8f,"Distant approach did not accelerate");
                    Call(host,"ConfigureRival",a,b,false,false);
                    Check(a.Motion.Behavior.TaskRunSpeed==.8f,"Host did not apply combat speed");
                    for(int i=0;i<40;i++) a.Motion.Behavior.Update(a.Motion.Position,a.Surfaces);
                    Check(a.Motion.Behavior.RunSpeed>.79f,"Combat speed remained generic task speed");
                    Call(host,"AdvanceScuffles",.025f); Check(!a.Motion.Behavior.TaskRunSpeed.HasValue,"Disabled fight leaked combat speed");
                    a.Motion.Behavior.TaskTarget=Vector2.right*100;
                    for(int i=0;i<100;i++) a.Motion.Behavior.Update(a.Motion.Position,a.Surfaces);
                    Check(Mathf.Abs(a.Motion.Behavior.RunSpeed-.35f)<.001f,"Normal task speed changed");
                    a.Motion.Behavior.TaskTarget=null; a.Motion.Behavior.Rest();
                    for(int i=0;i<120;i++) a.Motion.Advance(.025f);
                    var pos=a.Motion.Position; var forward=(pos-a.Motion.BodyPosition(1)).normalized;
                    Check(a.Motion.GripCount>0,"Homing fixture has no footing");
                    var velocity=a.Motion.BodyVelocity(0);
                    a.Motion.BiteHoming(pos+forward*60,1.4f);
                    Check(Vector2.Dot(a.Motion.BodyVelocity(0)-velocity,forward)>0,"Ready bite did not reach toward body target");
                    Check(a.Motion.Position==pos,"Homing teleported head");
                    velocity=a.Motion.BodyVelocity(0);
                    foreach(var invalid in new[]{new Vector2(float.NaN,0),new Vector2(float.PositiveInfinity,0),pos-forward*60})
                        a.Motion.BiteHoming(invalid,1.4f);
                    a.Motion.BiteHoming(pos+forward*60,float.NaN);
                    Check(a.Motion.BodyVelocity(0)==velocity,"Invalid/rear target applied homing");
                    var food=(SharedFood)Get(host,"food"); food.Begin(); food.Place(a.WorldMouth); food.Take(a.Config.key);
                    a.Motion.Behavior.TaskRunSpeed=.8f; Call(host,"ConfigureRival",a,b,true,false);
                    Check(!a.Motion.Behavior.TaskRunSpeed.HasValue,"Carrier inherited combat speed"); food.Cancel();
                    a.Motion.BeginGrab(pos); velocity=a.Motion.BodyVelocity(0);
                    a.Motion.BiteHoming(pos+forward*60,1.4f); Check(a.Motion.BodyVelocity(0)==velocity,"Homing pulled held pet");
                    Check(!(bool)Get(a.Pet.Lizard,"dead") && !(bool)Get(b.Pet.Lizard,"dead"),"Combat approach killed a pet");
                    a.Dispose(); b.Dispose(); pets.Clear();
                }
            }
            finally { foreach(var p in pets) p.Dispose(); pets.Clear(); native.Dispose(); UnityEngine.Object.DestroyImmediate(root); }
            Debug.Log("COMBAT_APPROACH_HOMING_GATES_SCALE_RESET_CHECK_PASSED");
        }
        static void Pose(DesktopPet pet,Vector2 origin,Vector2 forward)
        {
            var chunks=(Array)Get(pet.Pet.Lizard,"bodyChunks");
            for(int i=0;i<3;i++) { var p=origin-forward*i*17; Set(chunks.GetValue(i),"pos",p); Set(chunks.GetValue(i),"lastPos",p); Set(chunks.GetValue(i),"vel",Vector2.zero); }
        }
    }
}
