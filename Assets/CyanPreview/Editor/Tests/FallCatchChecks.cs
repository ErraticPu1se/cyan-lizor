using System;
using UnityEngine;
using static CyanPreview.ReferenceContext;

namespace CyanPreview.Editor
{
    public static class FallCatchChecks
    {
        static void Check(bool condition,string message) { if(!condition) throw new Exception(message); }
        static readonly Rect Bounds=new Rect(-300,-250,600,500);
        public static void Run(ReferenceContext context)
        {
            var progress=new FallCatchProgress(50); progress.Reset(100);
            progress.Advance(.025f,40,-20); Check(!progress.Ready,"Fast throw bypassed minimum airborne time");
            progress.Reset(100); progress.Advance(.1f,51,-10); Check(!progress.Ready,"Caught before one body length");
            progress.Advance(.025f,50,-10); Check(progress.Ready,"One-body drop did not enable catching");
            progress.Reset(100); progress.Advance(.1f,150,10); Check(!progress.Ready,"Caught while rising");
            progress.Advance(.1f,101,-10); Check(!progress.Ready,"Upward throw used release height instead of apex");
            progress.Advance(.025f,100,-10); Check(progress.Ready,"Apex drop never enabled catching");
            progress.Reset(100); Check(!progress.Ready && progress.DropDistance==0,"Regrab retained fall readiness");
            bool rejected=false; try { progress.Advance(float.NaN,0,0); } catch(ArgumentOutOfRangeException) { rejected=true; }
            Check(rejected,"Invalid time accepted");
            var noEdge=new DesktopSurfaces(Bounds) {Mode=ClimbingMode.EdgesAndBackgrounds};
            Check(!noEdge.TryCatchEdge(Vector2.zero,24,out _,out _),"Background was treated as a catch edge");
            Check(!noEdge.TryCatchEdge(new Vector2(0,-240),24,out _,out _),"Ground catch bypassed collision landing");
            var window=new DesktopWindow(1,new Rect(-100,-200,200,400));
            noEdge.UpdateWindows(new[] {new DesktopWindow(2,new Rect(-140,-220,280,440)),window});
            Check(!noEdge.TryCatchEdge(new Vector2(100,0),24,out _,out _),"Covered edge was caught through foreground window");
            noEdge.UpdateWindows(new[] {window});
            Check(noEdge.TryCatchEdge(new Vector2(110,0),24,out var edge,out var normal) && edge.x==100 && normal.x>0,"Outside edge catch pulled through window");
            noEdge.UpdateWindows(new[] {new DesktopWindow(1,new Rect(-50,-200,200,400))});
            Check(!noEdge.TryCatchEdge(new Vector2(110,0),24,out _,out _),"Moved window retained its old catch edge");
            noEdge.UpdateWindows(new DesktopWindow[0]);
            Check(!noEdge.TryCatchEdge(new Vector2(110,0),24,out _,out _),"Closed window retained a catch target");
            foreach(var mode in new[] {ClimbingMode.EdgesOnly,ClimbingMode.EdgesAndBackgrounds})
            foreach(bool desktopEdge in new[] {false,true})
            using(var pet=context.CreatePet())
            using(var other=context.CreatePet())
            {
                pet.Generate("9320"); other.Generate("9320");
                var surfaces=new DesktopSurfaces(Bounds) {Mode=mode};
                surfaces.UpdateWindows(new[] {window});
                var motion=new PetMotion(pet,surfaces,8) {Roam=false,AutonomousJumps=false};
                var second=new PetMotion(other,new DesktopSurfaces(Bounds) {Mode=mode},8) {Roam=false};
                var secondPosition=second.Position;
                PlaceAndRelease(pet,motion,new Vector2(desktopEdge?286:86,150));
                float initialY=((Vector2)Get(((Array)Get(pet.Lizard,"bodyChunks")).GetValue(1),"pos")).y;
                int sounds=0; motion.Landed+=speed=>sounds++;
                bool caught=false;
                for(int i=0;i<45;i++)
                {
                    motion.Advance(.025f);
                    float drop=initialY-((Vector2)Get(((Array)Get(pet.Lizard,"bodyChunks")).GetValue(1),"pos")).y;
                    if(motion.State==PetMotionState.Crawling)
                    {
                        Check(drop>=49.9f,"Caught before falling one body length");
                        Check(motion.Position.y>-180,"Missed reachable side edge and landed on floor instead");
                        Check(motion.GripCount>0,"Catch had no planted foot"); caught=true; break;
                    }
                }
                Check(caught,"Failed delayed catch: "+mode+" desktop="+desktopEdge);
                Check(sounds==0,"Edge grip emitted floor impact sound");
                Check(second.Position==secondPosition && !second.FallCatch.Ready,"Catch changed another pet with same ID");
                for(int i=0;i<20;i++) motion.Advance(.025f);
                Check(motion.State==PetMotionState.Crawling,"Catch did not settle");
                foreach(var limb in (Array)Get(pet.Graphics,"limbs"))
                    Check(Vector2.Distance((Vector2)Get(limb,"pos"),(Vector2)Get(Get(limb,"connection"),"pos")) <= (float)Get(limb,"jointDist")+.02f,"Catch stretched a limb");
                PlaceAndRelease(pet,motion,new Vector2(86,150));
                for(int i=0;i<6;i++) motion.Advance(.025f);
                motion.BeginGrab(motion.Position); motion.Advance(.2f);
                Check(motion.State==PetMotionState.Grabbed && !motion.FallCatch.Ready,"Regrab allowed an old catch");
                motion.EndGrab(); Check(!motion.FallCatch.Ready,"New release skipped catch delay");
                // Remove the only nearby edge while the pet is falling.
                surfaces.UpdateWindows(new DesktopWindow[0]);
                for(int i=0;i<100;i++) motion.Advance(.025f);
                Check(motion.State==PetMotionState.Crawling && motion.Position.y<-210,"Missing edge did not fall to floor");
            }
            Debug.Log("DELAYED_EDGE_CATCH_DISTANCE_GRAB_OCCLUSION_MULTIPET_PASSED");
        }
        static void PlaceAndRelease(ReferenceBridge pet,PetMotion motion,Vector2 point)
        {
            motion.BeginGrab(motion.Position);
            motion.ShiftWithSurface(point-motion.Position);
            motion.EndGrab();
            foreach(var chunk in (Array)Get(pet.Lizard,"bodyChunks")) Set(chunk,"vel",Vector2.zero);
        }
    }
}
