using System;
using System.Collections.Generic;
using UnityEngine;
using static CyanPreview.ReferenceContext;
namespace CyanPreview.Editor
{
    public static class MovingAttentionChecks
    {
        static void Check(bool ok,string message) { if(!ok) throw new Exception(message); }
        public static void Run(ReferenceContext context)
        {
            var c=new PetCuriosity(); c.Notice("still",Vector2.right*100,.3f);
            for(int i=0;i<20;i++) c.Advance(.025f);
            Check(!c.Notice("moving",Vector2.up*100,1,75,"Moving","sight",false,true),"Movement bypassed attention hold");
            for(int i=0;i<12;i++) c.Advance(.025f);
            Check(c.Notice("moving",Vector2.up*100,.9f,75,"Moving","sight",false,true),"Stronger moving cue never replaced stationary interest");
            var destination=c.Point.Value; var before=c.GazePoint.Value;
            c.TrackVisual("moving",Vector2.right*1000,50,.1f,true);
            Check(c.Point==destination && !c.ApproachAllowed,"Gaze tracking became pursuit");
            Check(Vector2.Distance(before,c.GazePoint.Value)<=30.001f,"Cursor warp snapped visual target");
            c.TrackVisual("moving",Vector2.zero,50,float.NaN,true);
            Check(!float.IsNaN(c.GazePoint.Value.x),"Invalid elapsed time poisoned gaze");
            for(int i=0;i<170;i++) { c.TrackVisual("moving",Vector2.zero,50,.025f,true); c.Advance(.025f); }
            Check(!c.Point.HasValue,"Motion tracking renewed attention indefinitely");
            var voice=new PetCuriosity(); voice.Notice("call",Vector2.right*200,.6f,75,"Caller","voice");
            for(int i=0;i<40;i++) voice.Advance(.025f);
            Check(!voice.Notice("moving",Vector2.zero,1,75,"Moving","sight",false,true),"Motion displaced vocal memory");
            voice.TrackVisual("call",Vector2.zero,50,.1f,false);
            Check(voice.GazePoint==Vector2.right*200,"Motion changed sound location");
            foreach(float dt in new[]{1f/30,1f/60,1f/120})
            {
                var root=new GameObject("Moving attention frame rate checks"); var pets=new List<DesktopPet>();
                try
                {
                    for(int i=0;i<3;i++) pets.Add(new DesktopPet(context,root.transform,DesktopPetConfiguration.From(new DesktopSettings {id="9320",scale=1,sound=false,roam=false,jumps=false},"motion-"+i),new Rect(-1000,-600,2000,1200),i));
                    for(int i=0;i<3;i++) pets[i].Motion.ShiftWithSurface(new Vector2(i*90,0)-pets[i].Motion.Position);
                    var listener=pets[0]; var dragged=pets[2]; dragged.Motion.BeginGrab(dragged.Motion.Position);
                    var sense=new PetSocialPerception(); sense.Advance(pets,false,dt);
                    Check(listener.Motion.Behavior.Social.Source==pets[1].Config.key,"Being held alone gained moving priority");
                    for(int i=0;i<Mathf.CeilToInt(1.3f/dt);i++)
                    {
                        dragged.Motion.ShiftWithSurface(Vector2.up*(i%2==0?100*dt:-100*dt));
                        foreach(var pet in pets) pet.Motion.Behavior.Social.Advance(dt);
                        sense.Advance(pets,false,dt);
                    }
                    Check(listener.Motion.Behavior.Social.Source==dragged.Config.key,"Moving held pet lost visual priority at dt="+dt);
                    Check(!listener.Motion.Behavior.Social.ApproachAllowed,"Watching dragged pet allowed pursuit");
                    var records=(System.Collections.IDictionary)Get(sense,"movement");
                    float movingSpeed=(float)Get(records[dragged.Config.key],"speed");
                    for(int i=0;i<Mathf.CeilToInt(1f/dt);i++) sense.Advance(pets,false,dt);
                    Check((float)Get(records[dragged.Config.key],"speed")<movingSpeed*.1f,"Motion salience failed to fade after stopping");
                    var gaze=listener.Motion.Behavior.Social.GazePoint;
                    dragged.Motion.ShiftWithSurface(new Vector2(800,0)-dragged.Motion.Position); sense.Advance(pets,false,dt);
                    Check(listener.Motion.Behavior.Social.GazePoint==gaze,"Gaze tracked source outside visual range");
                    var point=listener.Motion.Behavior.Social.Point;
                    dragged.Motion.ShiftWithSurface(Vector2.right*500); sense.Advance(pets,true,dt);
                    Check(listener.Motion.Behavior.Social.Point==point,"Pause changed remembered location");
                    pets.Remove(dragged); sense.Advance(pets,false,dt);
                    Check(listener.Motion.Behavior.Social.Source!=dragged.Config.key,"Removed moving target remained selected"); dragged.Dispose();
                }
                finally { foreach(var pet in pets) pet.Dispose(); UnityEngine.Object.DestroyImmediate(root); }
            }
            Debug.Log("MOVING_ATTENTION_HOLD_PRIORITY_FRAME_RATE_WARP_NO_CHASE_EXPIRY_PASSED");
        }
    }
}
