using System;
using System.Collections.Generic;
using UnityEngine;
using static CyanPreview.ReferenceContext;
namespace CyanPreview.Editor
{
    public static class SoundInvestigationChecks
    {
        static void Check(bool ok,string message) { if(!ok) throw new Exception(message); }
        public static void Run(ReferenceContext context)
        {
            var surface=new BackgroundSurfaces(new Rect(-1500,-800,3000,1600));
            var priority=new PetCuriosity(); priority.Notice("seen",Vector2.zero,.4f);
            Check(priority.Notice("heard",Vector2.right*200,1,75,"Caller","voice"),"Sight cooldown suppressed a fresh vocal cue");
            Check(!priority.Notice("heard",Vector2.left*200,1,75,"Caller","voice"),"Vocal priority bypassed its own cooldown");
            var b=new PetBehavior(42); b.Social.Notice("call",Vector2.right*350,1,75,"Caller","voice");
            for(int i=0;i<60;i++) b.Update(Vector2.zero,surface);
            Check(b.SoundInvestigation && b.Intent==PetIntent.Investigating,"Vocal cue never became sound investigation");
            var remembered=b.InterestPoint;
            Check(remembered.HasValue && Mathf.Abs(remembered.Value.x-275)<.01f,"Sound approach ignored personal space");
            Check(!b.Social.Notice("call",Vector2.left*350,1,75,"Caller","voice"),"Repeated calls redirect investigation during cooldown");
            Check(b.InterestPoint==remembered,"Moving caller changed remembered sound location");
            b.Target(Vector2.up*200); Check(!b.SoundInvestigation,"Explicit click retained sound cause");
            var stopped=new PetBehavior(42) {Roam=false}; stopped.Social.Notice("call",Vector2.right*350,1,75,"Caller","voice");
            for(int i=0;i<100;i++) Check(!stopped.Update(Vector2.zero,surface).HasValue && !stopped.SoundInvestigation,"Disabled roaming investigated sound");
            var root=new GameObject("Sound and held-pet awareness"); var pets=new List<DesktopPet>();
            try
            {
                for(int i=0;i<2;i++) pets.Add(new DesktopPet(context,root.transform,DesktopPetConfiguration.From(new DesktopSettings {id="9320",scale=1,sound=true,roam=true,jumps=false},"sound-"+i),new Rect(-1500,-800,3000,1600),i));
                var listener=pets[0]; var caller=pets[1]; listener.Motion.ShiftWithSurface(-listener.Motion.Position);
                caller.Motion.ShiftWithSurface(Vector2.right*listener.Motion.BodyLength*9-caller.Motion.Position);
                var sense=new PetSocialPerception(); sense.Advance(pets,false);
                Check(caller.Audio!=null && caller.Audio.FoodPickupCall(),"Love-call fixture failed"); sense.Advance(pets,false);
                Check(listener.Motion.Behavior.Social.Cue=="voice","Love call not perceived");
                for(int i=0;i<80;i++) listener.Advance(.025f,false);
                Check(listener.Motion.Behavior.SoundInvestigation && listener.Audio.VocalRevision==0,"Sound investigation answered with an automatic call");
                listener.Motion.BeginGrab(listener.Motion.Position);
                for(int i=0;i<520;i++) listener.Motion.Behavior.Social.Advance(.025f);
                caller.Motion.ShiftWithSurface(listener.Motion.Position+Vector2.up*120-caller.Motion.Position);
                sense.Advance(pets,false);
                Check(listener.Motion.Behavior.Social.Source==caller.Config.key,"Grabbed listener lost awareness");
                for(int i=0;i<20;i++) listener.Motion.Advance(.025f);
                Check(listener.Motion.State==PetMotionState.Grabbed && !listener.Motion.Behavior.SoundInvestigation && listener.Motion.GazeTarget==listener.Motion.Behavior.Social.Point.Value,"Grabbed listener did not glance without locomotion");
                // Other pets see the held pet but never use it as a moving pursuit destination.
                caller.Motion.Behavior.Rest(); for(int i=0;i<520;i++) caller.Motion.Behavior.Social.Advance(.025f);
                sense.Advance(pets,false);
                Check(caller.Motion.Behavior.Social.Source==listener.Config.key && !caller.Motion.Behavior.Social.ApproachAllowed,"Unheld pet cannot notice held pet, or can chase it");
                var point=caller.Motion.Behavior.Social.Point; listener.Motion.DragTo(listener.Motion.Position+Vector2.right*100); listener.Motion.Advance(.025f); sense.Advance(pets,false);
                Check(caller.Motion.Behavior.Social.Point==point,"Dragging updates a pursuit target");
                Check(!PetContacts.Available(listener),"Grabbed pet lost collision/fighting exception");
                listener.Motion.EndGrab(); sense.Advance(pets,false);
                Check(listener.Motion.State==PetMotionState.Airborne && !listener.Motion.Behavior.SoundInvestigation,"Release immediately triggered sound locomotion");
            }
            finally { foreach(var pet in pets) pet.Dispose(); UnityEngine.Object.DestroyImmediate(root); }
            Debug.Log("SOUND_LOCATION_LOVE_CALL_NO_REPLY_HELD_AWARENESS_NO_PURSUIT_PASSED");
        }
    }
}
