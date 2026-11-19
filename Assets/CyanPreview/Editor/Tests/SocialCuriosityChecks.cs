using System;
using System.Collections.Generic;
using UnityEngine;
using static CyanPreview.ReferenceContext;
namespace CyanPreview.Editor
{
    public static class SocialCuriosityChecks
    {
        static void Check(bool value,string message) { if(!value) throw new Exception(message); }
        public static void Run(ReferenceContext context)
        {
            var bounds=new Rect(-700,-400,1400,800); var free=new BackgroundSurfaces(bounds);
            var behavior=new PetBehavior(42); Vector2 previous=Vector2.right;
            for(int i=0;i<100;i++)
            {
                var next=(Vector2)Call(behavior,"NextRoamingDestination",Vector2.zero,free);
                Check(Vector2.Angle(previous,next)<=35.01f,"Roaming lost directional continuity"); previous=next.normalized;
            }
            var corner=new Vector2(686,386);
            Check(Vector2.Distance((Vector2)Call(behavior,"NextRoamingDestination",corner,free),corner)>30,"Roaming stuck at corner");
            var social=new PetCuriosity();
            Check(!social.Notice("x",new Vector2(float.NaN,0),1),"Invalid social cue accepted");
            Check(social.Notice("a",Vector2.right*200,.95f) && !social.CanApproach,"Missing initial glance");
            Check(!social.Notice("b",Vector2.zero,1),"Repeated notice bypassed cooldown");
            for(int i=0;i<44;i++) social.Advance(.025f);
            Check(social.CanApproach,"Glance never allows approach");
            for(int i=0;i<160;i++) social.Advance(.025f);
            Check(!social.Point.HasValue && !social.Notice("b",Vector2.zero,1),"Interest did not expire independently of cooldown");
            for(int i=0;i<320;i++) social.Advance(.025f);
            Check(social.Notice("b",Vector2.zero,1),"Cooldown never recovers");
            var curious=new PetBehavior(42); curious.Social.Notice("a",Vector2.right*200,1);
            for(int i=0;i<44;i++) curious.Update(Vector2.zero,free);
            Check(curious.Intent==PetIntent.Socializing && curious.Update(Vector2.zero,free).Value.x<160,"Social approach lacks personal space");
            curious.Target(Vector2.up*100); curious.Update(Vector2.zero,free);
            Check(curious.Intent==PetIntent.Investigating,"Social cue interrupted cursor click");
            curious.TaskTarget=Vector2.left*100; Check(curious.Update(Vector2.zero,free)==curious.TaskTarget,"Social cue interrupted task");
            var stationary=new PetBehavior(42) {Roam=false}; stationary.Social.Notice("a",Vector2.right*200,1);
            for(int i=0;i<30;i++) Check(!stationary.Update(Vector2.zero,free).HasValue,"Social approach ignored disabled roaming");

            var root=new GameObject("Social curiosity checks"); var pets=new List<DesktopPet>();
            try
            {
                for(int i=0;i<2;i++) pets.Add(new DesktopPet(context,root.transform,DesktopPetConfiguration.From(new DesktopSettings {id="9320",sound=false,roam=true,jumps=false},"instance-"+i),bounds,i));
                var a=pets[0]; var b=pets[1]; a.Motion.ShiftWithSurface(-a.Motion.Position); b.Motion.ShiftWithSurface(new Vector2(150,0)-b.Motion.Position);
                var perception=new PetSocialPerception(); perception.Advance(pets,false);
                Check(a.Motion.Behavior.Social.Source==b.Config.key && b.Motion.Behavior.Social.Source==a.Config.key,"Duplicate appearance IDs confused social identity");
                Check(!a.Motion.RivalTarget.HasValue && !b.Motion.RivalTarget.HasValue,"Curiosity started fighting");
                a.Motion.RivalTarget=Vector2.right; perception.Advance(pets,false);
                Check(!a.Motion.Behavior.Social.Point.HasValue && a.Motion.RivalTarget==Vector2.right,"Curiosity interfered with rivalry");
                a.Motion.RivalTarget=null; pets.Remove(b); perception.Advance(pets,false);
                Check(!a.Motion.Behavior.Social.Point.HasValue,"Removed source left stale attention");
                // Isolate hearing from sight: the source is outside visual range.
                pets.Add(b); b.Motion.ShiftWithSurface(b.Local(Vector2.right*a.Motion.BodyLength*a.Config.scale*9)-b.Motion.Position);
                for(int i=0;i<600;i++) a.Motion.Behavior.Social.Advance(.025f);
                perception=new PetSocialPerception(); perception.Advance(pets,false);
                Check(!a.Motion.Behavior.Social.Point.HasValue,"Distant source was seen outside visual range");
                Check(b.Audio!=null,"Audio fixture unavailable"); b.Audio.Enabled=true;
                Check(b.Audio.Vocalize(),"Vocal fixture did not play");
                perception.Advance(pets,false);
                Check(a.Motion.Behavior.Social.Source==b.Config.key,"Played vocal call did not attract distant listener");
                var remembered=a.Motion.Behavior.Social.Point;
                b.Motion.ShiftWithSurface(Vector2.up*20); perception.Advance(pets,false);
                Check(a.Motion.Behavior.Social.Point==remembered,"Persistent call refreshed a moving chase target");
                Check(!b.Audio.Vocalize(),"Vocal cooldown failed");
                a.Motion.Behavior.Social.Clear(); perception.Advance(pets,true);
                Check(!a.Motion.Behavior.Social.Point.HasValue,"Paused perception created interest");
                pets.Remove(b); b.Dispose();
                for(int i=0;i<600;i++) a.Motion.Behavior.Social.Advance(.025f);
                perception.Advance(pets,false); Check(!a.Motion.Behavior.Social.Point.HasValue,"Lone pet noticed itself");
                Check(a.Audio==null || a.Audio.VocalRevision==0,"Social interest produced a call-response loop");
            }
            finally { foreach(var pet in pets) pet.Dispose(); UnityEngine.Object.DestroyImmediate(root); }
            Debug.Log("DIRECTIONAL_ROAM_SOCIAL_GLANCE_SPACING_EXPIRY_IDENTITY_PRIORITY_PASSED");
        }
    }
}
