using System;
using UnityEngine;
using static CyanPreview.ReferenceContext;
namespace CyanPreview.Editor
{
    public static class HeadResponseChecks
    {
        static void Check(bool ok,string message) { if(!ok) throw new Exception(message); }
        public static void Verify()
        {
            using(var context=new ReferenceContext(System.IO.Path.GetFullPath("local-data")))
            { Run(context); CompanionChecks.IdleStability(context); CompanionChecks.EdgeFootholds(context); CompanionChecks.RestingForces(context); }
        }
        static void SharedFacing(ReferenceContext context)
        {
            foreach(string cue in new[]{"cursor","sight","voice","footstep"})
            foreach(bool edge in new[]{false,true})
            using(var pet=context.CreatePet())
            {
                pet.Generate("9320");
                IPetSurfaces surfaces=edge?(IPetSurfaces)new FrameSurfaces(new Rect(-700,-400,1400,800)):new BackgroundSurfaces(new Rect(-700,-400,1400,800));
                var m=new PetMotion(pet,surfaces,42) {Roam=false,AutonomousJumps=false};
                for(int i=0;i<200;i++) m.Advance(.025f);
                var origin=m.Position; var rearOrigin=m.BodyPosition(2);
                var forward=(m.Position-m.BodyPosition(1)).normalized; var side=new Vector2(-forward.y,forward.x);
                foreach(float sign in new[]{1f,-1f})
                {
                    var target=m.Position+(forward*.5f+side*.8660254f*sign)*120;
                    if(cue!="cursor") { for(int wait=0;wait<49;wait++) m.Behavior.Social.Advance(.25f); Check(m.Behavior.Social.Notice("other",target,1,m.BodyLength*1.5f,"Other",cue,false),"Social fixture rejected target"); }
                    for(int i=0;i<12;i++) { if(cue=="cursor") m.ObserveCursor(target); m.Advance(.025f); }
                    float error=Vector2.Angle(m.HeadPosition-m.Position,target-m.Position);
                    Debug.Log($"SHARED_HEAD_FACING cue={cue} edge={edge} side={sign} error300ms={error:F2}");
                    Check(error<20,"Head failed to face "+cue+" within 300ms");
                    Check(Vector2.Distance(rearOrigin,m.BodyPosition(2))<10,"Head glance dragged resting support");
                }
                // A moving target directly behind must not flip the neck side each tick.
                float last=0; int flips=0;
                if(cue!="cursor") { for(int wait=0;wait<49;wait++) m.Behavior.Social.Advance(.25f); Check(m.Behavior.Social.Notice("other",m.Position-forward*120,1,m.BodyLength*1.5f,"Other",cue,false),"Rear fixture rejected target"); }
                for(int i=0;i<120;i++)
                {
                    var target=m.Position-forward*120+side*(i%2==0?.1f:-.1f);
                    if(cue=="cursor") m.ObserveCursor(target);
                    else m.Behavior.Social.TrackVisual("other",target,m.BodyLength,.025f,false);
                    m.Advance(.025f);
                    float angle=(float)Get(m,"idleNeckTurn");
                    if(i>40 && Mathf.Abs(angle)>10 && Mathf.Abs(last)>10 && Mathf.Sign(angle)!=Mathf.Sign(last)) flips++;
                    last=angle;
                    Check(!float.IsNaN(m.HeadPosition.x),"Rear gaze became non-finite");
                }
                Check(flips==0,"Rear gaze repeatedly flipped neck side");
                m.Behavior.Social.Clear();
                for(int i=0;i<200;i++) { m.ObserveCursor(null); m.Advance(.025f); }
                Check(Mathf.Abs((float)Get(m,"idleNeckTurn"))<1,"Expired attention left neck twisted");
            }
            Debug.Log("SHARED_HEAD_CURSOR_SOCIAL_FACING_REVERSAL_REAR_EXPIRY_PASSED");
        }
        public static void Run(ReferenceContext context)
        {
            SharedFacing(context);
            foreach(bool edge in new[]{false,true})
            foreach(float distance in new[]{100f,180f})
            using(var pet=context.CreatePet())
            using(var other=context.CreatePet())
            {
                pet.Generate("9320"); other.Generate("9320");
                IPetSurfaces surfaces=edge?(IPetSurfaces)new FrameSurfaces(new Rect(-700,-400,1400,800)):new BackgroundSurfaces(new Rect(-700,-400,1400,800));
                var m=new PetMotion(pet,surfaces,42) {Roam=false,AutonomousJumps=false};
                var untouched=new PetMotion(other,surfaces,42) {Roam=false,AutonomousJumps=false};
                for(int i=0;i<200;i++) m.Advance(.025f);
                var origin=m.Position; var rearOrigin=m.BodyPosition(2); var otherHead=untouched.HeadPosition;
                var forward=(m.Position-m.BodyPosition(1)).normalized;
                var side=new Vector2(-forward.y,forward.x);
                var before=(m.HeadPosition-m.Position).normalized;
                var target=m.Position+(forward*.5f+side*.8660254f)*distance;
                for(int i=0;i<6;i++) { m.ObserveCursor(target); m.Advance(.025f); }
                float turn=Vector2.Angle(before,m.HeadPosition-m.Position);
                Debug.Log($"HEAD_RESPONSE edge={edge} distance={distance} turn150ms={turn:F2}");
                Check(turn>12,"Cursor head response still sluggish at 150ms");
                Check(Vector2.Distance(origin,m.Position)<4,"Quick head turn pulled resting body");
                Check(!m.Behavior.InterestPoint.HasValue && !m.Behavior.TaskTarget.HasValue,"Attention created locomotion intent");
                Check(untouched.HeadPosition==otherHead,"Cursor response mutated duplicate-ID pet");
                for(int i=0;i<4;i++) { m.ObserveCursor(null); m.Advance(.025f); Check(m.GazeTarget==target,"One missing cursor sample dropped attention"); }
                for(int i=0;i<100;i++) { m.ObserveCursor(null); m.Advance(.025f); }
                Check(m.GazeTarget!=target,"Lost cursor remained a stale target");
                // Direct task/food attention must not be cleared by the passive-cursor grace timer.
                m.ObserveCursor(m.Position+Vector2.right*70); var explicitTarget=m.Position+Vector2.up*70; m.LookAt(explicitTarget);
                for(int i=0;i<10;i++) m.Advance(.025f);
                Check(m.GazeTarget==explicitTarget,"Cursor expiry cleared explicit attention");
                // Explicit targets remain finite after cursor ownership changes.
                Check(!float.IsNaN(m.HeadPosition.x),"Head became non-finite");
            }
            Vector2? reference=null;
            foreach(float dt in new[]{.025f,.1f,.25f})
            using(var pet=context.CreatePet())
            {
                pet.Generate("9320");
                var m=new PetMotion(pet,new BackgroundSurfaces(new Rect(-700,-400,1400,800)),42) {Roam=false,AutonomousJumps=false};
                for(int i=0;i<200;i++) m.Advance(.025f);
                var target=m.Position+new Vector2(60,100);
                for(int i=0;i<Mathf.RoundToInt(1/dt);i++) { m.ObserveCursor(target); m.Advance(dt); Check(m.GazeTarget==target,"Slow render frame expired a visible cursor"); }
                if(reference.HasValue) Check(Vector2.Distance(reference.Value,m.HeadPosition)<.01f,"Head response depended on render frame rate");
                reference=m.HeadPosition;
                m.BeginGrab(m.Position);
                var first=m.Position+new Vector2(70,60); var second=m.Position+new Vector2(70,-60);
                for(int i=0;i<20;i++) { m.ObserveCursor(first); m.Advance(.025f); }
                var before=m.HeadPosition-m.Position;
                for(int i=0;i<20;i++) { m.ObserveCursor(second); m.Advance(.025f); }
                Check(m.State==PetMotionState.Grabbed && Vector2.Angle(before,m.HeadPosition-m.Position)>10,"Held head stopped responding to cursor");
                Check(m.GazeTarget==second,"Held pet lost cursor target");
            }
            Debug.Log("HEAD_CURSOR_RESPONSE_LOSS_RANGE_INDEPENDENCE_FRAMERATE_HELD_CHECK_PASSED");
        }
    }
}
