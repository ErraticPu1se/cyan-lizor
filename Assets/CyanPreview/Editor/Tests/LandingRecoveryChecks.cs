using System;
using UnityEngine;
using static CyanPreview.ReferenceContext;

namespace CyanPreview.Editor
{
    public static class LandingRecoveryChecks
    {
        static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        static readonly Rect Bounds = new Rect(-300, -200, 600, 400);
        public static void Run(ReferenceContext context)
        {
            var tuning = new LandingTuning(.15f, .35f, 5, .55f);
            var soft = new LandingRecovery(tuning); var hard = new LandingRecovery(tuning);
            soft.Begin(3, Vector2.up, Vector2.zero); hard.Begin(20, Vector2.up, Vector2.zero);
            Check(soft.Compression < hard.Compression && hard.DriveScale < soft.DriveScale, "Impact strength did not affect recovery");
            var otherSpecies = new LandingRecovery(new LandingTuning(.1f,.2f,2,.65f));
            otherSpecies.Begin(20,Vector2.left,Vector2.zero);
            Check(otherSpecies.Compression < hard.Compression, "Species tuning was ignored");
            hard.Advance(.1f,4,0); Check(hard.Active,"Stable feet ended recovery before minimum duration");
            hard.Advance(.25f,0,100); Check(!hard.Active,"Missing grips left recovery stuck");
            var disabled = new LandingRecovery(new LandingTuning(0,0,0,1));
            disabled.Begin(30,Vector2.up,Vector2.zero); Check(!disabled.Active,"Disabled species recovery started");
            foreach(float invalid in new[] { float.NaN, float.PositiveInfinity, -1 })
            {
                bool rejected=false; try { soft.Begin(invalid,Vector2.up,Vector2.zero); } catch(ArgumentOutOfRangeException) { rejected=true; }
                Check(rejected,"Invalid collision speed accepted");
                rejected=false; try { soft.Advance(invalid,0,0); } catch(ArgumentOutOfRangeException) { rejected=true; }
                Check(rejected,"Invalid recovery time accepted");
            }
            soft.Cancel(); soft.Begin(1.5f,Vector2.up,Vector2.zero); Check(!soft.Active,"Soft contact triggered full recovery");
            soft.Begin(10,Vector2.zero,Vector2.zero); Check(soft.Normal==Vector2.zero,"Background catch invented a terrain normal");

            foreach(var velocity in new[] { new Vector2(0,-2), new Vector2(0,-20), new Vector2(20,0), new Vector2(0,20), new Vector2(20,-20) })
            using(var pet=context.CreatePet())
            using(var untouched=context.CreatePet())
            {
                pet.Generate("9320"); untouched.Generate("9320");
                var motion=new PetMotion(pet,new FrameSurfaces(Bounds),5) { Roam=false };
                var second=new PetMotion(untouched,new FrameSurfaces(Bounds),5) { Roam=false };
                var otherPosition=second.Position;
                int sounds=0; motion.Landed += speed => sounds++;
                var point=new Vector2(velocity.x>0?291.8f:0, velocity.y>0?191.8f:velocity.y<0?-191.8f:0);
                PutInFlight(pet,motion,point,velocity);
                motion.Advance(.025f);
                Check(motion.State==PetMotionState.Crawling && motion.Recovery.Active,"Impact did not enter recovery: "+velocity);
                Check(!motion.JumpTo(Vector2.zero),"Manual jump interrupted landing before feet settled");
                Check(sounds==1,"Landing sound did not fire exactly once");
                if(velocity.x>0) Check(motion.Recovery.Normal.x<0,"Wall/corner recovery faced wrong direction");
                if(velocity.y<0) Check(motion.Recovery.Normal.y>0,"Floor recovery faced wrong direction");
                if(velocity.y>0) Check(motion.Recovery.Normal.y<0,"Ceiling recovery faced wrong direction");
                var initialHead=(Vector2)Get(Get(pet.Graphics,"head"),"pos");
                for(int step=0;step<30;step++)
                {
                    motion.Advance(.025f); pet.Draw();
                    Check(motion.State==PetMotionState.Crawling,"Recovery spontaneously launched");
                    foreach(var chunk in (Array)Get(pet.Lizard,"bodyChunks"))
                    {
                        var pos=(Vector2)Get(chunk,"pos");
                        Check(!float.IsNaN(pos.x) && !float.IsNaN(pos.y) && Bounds.Contains(pos),"Recovery escaped bounds or became non-finite");
                    }
                    foreach(var limb in (Array)Get(pet.Graphics,"limbs"))
                        Check(Vector2.Distance((Vector2)Get(limb,"pos"),(Vector2)Get(Get(limb,"connection"),"pos")) <= (float)Get(limb,"jointDist")+.02f,"Recovery stretched a foot beyond reach");
                }
                Check(!motion.Recovery.Active && motion.Recovery.Compression==0,"Recovery did not settle");
                Check(sounds==1,"Recovery contact repeated landing sound");
                Check(second.Position==otherPosition && !second.Recovery.Active,"Duplicate-ID pet shared landing state");
                Check(Vector2.Distance(initialHead,(Vector2)Get(Get(pet.Graphics,"head"),"pos"))>.01f,"Head was frozen during recovery");
                PutInFlight(pet,motion,new Vector2(0,-191),new Vector2(0,-15)); motion.Advance(.025f);
                motion.BeginGrab(motion.Position);
                Check(!motion.Recovery.Active && motion.State==PetMotionState.Grabbed,"Grabbing did not cancel recovery");
                motion.EndGrab();
                PutInFlight(pet,motion,new Vector2(0,-191),new Vector2(0,-15)); motion.Advance(.025f);
                motion.ReleaseSupport();
                Check(!motion.Recovery.Active && motion.State==PetMotionState.Airborne,"Lost support did not cancel recovery");
            }
            using(var pet=context.CreatePet())
            {
                pet.Generate("42");
                var surfaces=new DesktopSurfaces(Bounds) { Mode=ClimbingMode.EdgesAndBackgrounds };
                var motion=new PetMotion(pet,surfaces) { Roam=false };
                PutInFlight(pet,motion,new Vector2(0,-191),new Vector2(0,-15)); motion.Advance(.025f);
                var before=motion.Recovery.ContactPoint;
                motion.ShiftWithSurface(new Vector2(30,20),false);
                Check(motion.Recovery.ContactPoint==before+new Vector2(30,20),"Moving window left recovery plane behind");
                motion.ReleaseSupport(); motion.Advance(.025f);
                Check(!motion.Recovery.Active,"Closed window restored stale recovery");
            }
            Debug.Log("LANDING_RECOVERY_SPECIES_MULTIPET_EDGES_PASSED");
        }
        static void PutInFlight(ReferenceBridge pet, PetMotion motion, Vector2 point, Vector2 velocity)
        {
            motion.BeginGrab(motion.Position); motion.EndGrab();
            var chunks=(Array)Get(pet.Lizard,"bodyChunks");
            for(int i=0;i<chunks.Length;i++)
            {
                var p=point-Vector2.right*i*17;
                Set(chunks.GetValue(i),"pos",p); Set(chunks.GetValue(i),"lastPos",p); Set(chunks.GetValue(i),"vel",velocity);
            }
        }
    }
}
