using System;
using System.Collections.Generic;
using UnityEngine;
using static CyanPreview.ReferenceContext;
namespace CyanPreview.Editor
{
    public static class SocialRefinementChecks
    {
        static void Check(bool ok,string message) { if(!ok) throw new Exception(message); }
        public static void Verify()
        { using(var context=new ReferenceContext(System.IO.Path.GetFullPath("local-data"))) Run(context); }
        public static void Run(ReferenceContext context)
        {
            using(var pet=context.CreatePet())
            {
                pet.Generate("9320"); var m=new PetMotion(pet,new BackgroundSurfaces(new Rect(-700,-400,1400,800)),42) {Roam=false,AutonomousJumps=false};
                float length=m.BodyLength; Check(length>34,"Missing unbent body extent");
                for(int i=0;i<120;i++) m.Advance(.025f);
                Check(m.FootstepRevision==0,"Resting grip updates emit footsteps");
                m.ObserveCursor(Vector2.right*100);
                m.Behavior.Social.Notice("other",Vector2.up*150,1,length*1.5f,"Other","voice");
                for(int i=0;i<20;i++) m.Advance(.025f);
                Check(m.GazeTarget==Vector2.up*150,"Held social glance lost to passive cursor");
                Check((float)Get(m,"idleBodyTurn")>2,"Social glance did not turn upper body");
                Check(m.BodyLength==length,"Bent pose changed social body length");
                m.ExploreClick(Vector2.right*120); m.Advance(.025f);
                Check(!m.SocialGaze.HasValue,"Explicit click failed to interrupt social gaze");
                m.Behavior.Social.Clear(); m.ObserveCursor(null); m.Behavior.TaskTarget=Vector2.right*350;
                for(int i=0;i<240;i++) m.Advance(.025f);
                Check(m.FootstepRevision>0,"Crawling emitted no foot contacts");
                uint steps=m.FootstepRevision; m.BeginGrab(m.Position);
                for(int i=0;i<40;i++) m.Advance(.025f);
                Check(m.FootstepRevision==steps,"Grabbed limbs emitted footsteps");
            }
            var root=new GameObject("Body length perception tests");
            try
            {
                foreach(float scale in new[]{1f,1.2f,2f})
                {
                    var pets=new List<DesktopPet>();
                    try
                    {
                        for(int i=0;i<2;i++) pets.Add(new DesktopPet(context,root.transform,DesktopPetConfiguration.From(new DesktopSettings {id="9320",scale=i==0?scale:2,sound=false,roam=false,jumps=false},"scale-"+i),new Rect(-2000,-1200,4000,2400),i));
                        var a=pets[0]; var b=pets[1]; a.Motion.ShiftWithSurface(-a.Motion.Position);
                        float worldBody=a.Motion.BodyLength*a.Config.scale;
                        b.Motion.ShiftWithSurface(b.Local(Vector2.right*worldBody*6.9f)-b.Motion.Position);
                        var sense=new PetSocialPerception(); sense.Advance(pets,false);
                        Check(a.Motion.Behavior.Social.Source==b.Config.key,"Seven-body sight range failed at size "+scale);
                        float spacing=1.5f*Mathf.Max(worldBody,b.Motion.BodyLength*b.Config.scale)/a.Config.scale;
                        Check(Mathf.Abs(a.Motion.Behavior.Social.Spacing-spacing)<.001f,"Mixed-size spacing ignores larger pet");
                        for(int i=0;i<520;i++) a.Motion.Behavior.Social.Advance(.025f);
                        b.Motion.ShiftWithSurface(b.Local(Vector2.right*worldBody*7.1f)-b.Motion.Position); sense.Advance(pets,false);
                        Check(!a.Motion.Behavior.Social.Point.HasValue,"Sight exceeded body-length range");
                        // A new foot contact outranks sight, but does not loop on the same revision.
                        b.Motion.ShiftWithSurface(b.Local(Vector2.right*worldBody*2)-b.Motion.Position);
                        Set(b.Motion,"<FootstepRevision>k__BackingField",1u); sense.Advance(pets,false);
                        Check(a.Motion.Behavior.Social.Cue=="footsteps","Foot-contact cue not heard");
                        var point=a.Motion.Behavior.Social.Point; sense.Advance(pets,false);
                        Check(a.Motion.Behavior.Social.Point==point,"Footstep replay changed locked target");
                        var sample=PickupDiagnostics.Read(a,pets,a.WorldPosition);
                        a.Motion.Advance(.025f); sample=PickupDiagnostics.Read(a,pets,a.WorldPosition);
                        Check(sample.socialLook!=null && sample.socialLook.Contains("footsteps"),"Social label does not reflect active gaze");
                        for(int i=0;i<520;i++) a.Motion.Behavior.Social.Advance(.025f);
                        b.Motion.ShiftWithSurface(b.Local(Vector2.right*worldBody*5.1f)-b.Motion.Position);
                        Set(b.Motion,"<FootstepRevision>k__BackingField",2u); sense.Advance(pets,false);
                        Check(a.Motion.Behavior.Social.Cue=="sight","Footstep hearing exceeded five body lengths");
                        for(int i=0;i<520;i++) a.Motion.Behavior.Social.Advance(.025f);
                        Check(b.Audio!=null,"Voice boundary fixture unavailable");
                        b.Motion.ShiftWithSurface(b.Local(Vector2.right*worldBody*12.1f)-b.Motion.Position);
                        Set(b.Audio,"<VocalRevision>k__BackingField",1u); sense.Advance(pets,false);
                        Check(!a.Motion.Behavior.Social.Point.HasValue,"Voice hearing exceeded twelve body lengths");
                        b.Motion.ShiftWithSurface(b.Local(Vector2.right*worldBody*11.9f)-b.Motion.Position);
                        Set(b.Audio,"<VocalRevision>k__BackingField",2u); sense.Advance(pets,false);
                        Check(a.Motion.Behavior.Social.Cue=="voice","Voice not heard within twelve body lengths");
                    }
                    finally { foreach(var p in pets) p.Dispose(); }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            Debug.Log("SOCIAL_BODY_LENGTH_SCALE_GAZE_UPPER_BODY_FOOT_CONTACT_PRIORITY_PASSED");
        }
    }
}
