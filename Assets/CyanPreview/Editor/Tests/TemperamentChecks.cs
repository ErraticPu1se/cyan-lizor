using System;
using System.Collections.Generic;
using UnityEngine;
using static CyanPreview.ReferenceContext;
namespace CyanPreview.Editor
{
    public static class TemperamentChecks
    {
        static void Check(bool ok,string why) { if(!ok) throw new Exception(why); }
        public static void Verify() { using(var c=new ReferenceContext(System.IO.Path.GetFullPath("local-data"))) Run(c); }
        public static void Run(ReferenceContext context)
        {
            var calm=new PetCuriosity(.5f,0,0); var nervous=new PetCuriosity(.5f,0,1);
            foreach(var s in new[]{calm,nervous}) s.Notice("other",Vector2.right*50,.5f,75,"Other","sight",true,false,1);
            Check(nervous.Utility>calm.Utility,"Nervous pet did not notice more strongly");
            for(int i=0;i<30;i++) { calm.Advance(.025f); nervous.Advance(.025f); }
            Check(!calm.HoldingGlance && nervous.HoldingGlance,"Nervous pet did not watch longer");
            calm.TrackVisual("other",Vector2.right*50,50,.025f,false);
            var yield=calm.YieldFrom(Vector2.zero); Check(yield.HasValue && yield.Value.x<0,"Lower dominance did not yield away");
            calm.TrackVisual("other",Vector2.right*51,50,.025f,false);
            Check(calm.YieldFrom(Vector2.zero)==yield,"Yield destination jittered with small source motion");
            calm.TrackVisual("other",Vector2.right*51,50,.025f,true); Check(!calm.YieldFrom(Vector2.zero).HasValue,"Held pet caused yielding chase");
            var watcher=new PetBehavior(42) {Roam=false};
            Set(watcher.Social,"dominance",0f); watcher.Social.Notice("other",Vector2.right*20,1,75,"Other","sight",true,false,1);
            for(int i=0;i<80;i++) { watcher.Social.TrackVisual("other",Vector2.right*20,50,.025f,false); Check(!watcher.Update(Vector2.zero,new BackgroundSurfaces(new Rect(-700,-400,1400,800))).HasValue,"Yielding bypassed disabled roaming"); }
            var dominant=new PetCuriosity(.5f,1,0); dominant.Notice("other",Vector2.right*50,1,75,"Other","sight",true,false,0);
            for(int i=0;i<30;i++) dominant.Advance(.025f);
            dominant.TrackVisual("other",Vector2.right*50,50,.025f,false);
            Check(!dominant.YieldFrom(Vector2.zero).HasValue,"Dominant pet yielded to lower dominance without threat");
            foreach(float dt in new[]{.0125f,.025f,.1f})
            {
                var timid=new PetConflictResponse(0,1); var bold=new PetConflictResponse(1,0);
                for(int i=0;i<Mathf.RoundToInt(2/dt);i++)
                { timid.Advance(dt); bold.Advance(dt); timid.ObserveThreat(dt,1,true,true); bold.ObserveThreat(dt,0,true,true); }
                Check(timid.Retreating && !bold.HasYielded,"Threat response ignored personality or frame rate");
                timid.Cancel(); timid.NoticeHit(1); timid.ObserveThreat(.025f,1,true,true);
                Check(!timid.HasYielded,"Retreat restarted during cooldown");
                for(int i=0;i<120;i++) timid.Advance(.1f);
                timid.NoticeHit(1); timid.ObserveThreat(.025f,1,false,false); Check(!timid.HasYielded,"Retreat started without a surface");
                timid.ObserveThreat(.025f,1,false,true); Check(timid.Retreating,"Grounded threatened pet did not retreat");
                for(int i=0;i<40;i++) timid.Advance(.1f);
                Check(!timid.Retreating && timid.HasYielded,"Retreat did not finish in bounded time");
                for(int i=0;i<4;i++) bold.NoticeHit(0);
                bold.ObserveThreat(.025f,0,false,true); Check(bold.Retreating,"Dominance made pet immune to repeated intimidation");
            }
            var invalid=new PetConflictResponse(float.NaN,float.PositiveInfinity);
            invalid.NoticeHit(1); invalid.ObserveThreat(float.NaN,1,true,true); invalid.ObserveThreat(0,1,true,true); invalid.Advance(-1);
            Check(!invalid.HasYielded,"Invalid or paused time caused retreat");
            var root=new GameObject("Temperament integration"); var host=root.AddComponent<DesktopApp>();
            var pets=(List<DesktopPet>)Get(host,"pets"); var native=new WindowsDesktop(System.Diagnostics.Process.GetCurrentProcess().Id);
            var settings=new DesktopSettings {allowFighting=true,scuffleRequest=1,scuffleFirst="a",scuffleSecond="b"};
            Set(host,"native",native); Set(host,"settings",settings);
            try
            {
                foreach(string key in new[]{"a","b"})
                    pets.Add(new DesktopPet(context,root.transform,DesktopPetConfiguration.From(new DesktopSettings {id="9320",sound=false,roam=false,jumps=false},key),new Rect(-700,-400,1400,800),pets.Count));
                var a=pets[0]; var b=pets[1];
                Check(a.Motion.AutonomousJumps && !a.Config.jumps,"Legacy jump preference overrode cyan species trait or was overwritten");
                a.Configure(a.Config,false); Check(a.Motion.AutonomousJumps,"Configuration disabled species propulsion");
                Check(!new MovementCapabilities(true,2).CanPropel,"Non-propelling species gained cyan ability");
                foreach(var pet in pets) { pet.Motion.AutonomousJumps=false; for(int i=0;i<80;i++) pet.Motion.Advance(.025f); }
                Set(a,"Conflict",new PetConflictResponse(0,1)); Set(b,"Conflict",new PetConflictResponse(1,0));
                Check(!ReferenceEquals(a.Conflict,b.Conflict),"Duplicate appearance IDs share conflict state");
                Call(host,"ProcessScuffleRequest"); a.Conflict.NoticeHit(1); Call(host,"AdvanceScuffles",.025f);
                Check(a.Conflict.Retreating && !b.Conflict.Retreating,"Independent retreat was not applied to active scuffle");
                Check(a.Motion.Behavior.TaskTarget.HasValue && !a.Motion.Behavior.HoldPosition && b.Motion.Behavior.HoldPosition,"Retreat did not move one pet and stop opponent pursuing");
                Check(!a.Motion.RivalLunging && !a.Motion.RivalPropelling,"Retreat retained attack propulsion");
                var destination=a.Motion.Behavior.TaskTarget.Value;
                Check(destination.x>=a.Surfaces.Bounds.xMin && destination.x<=a.Surfaces.Bounds.xMax && destination.y>=a.Surfaces.Bounds.yMin && destination.y<=a.Surfaces.Bounds.yMax,"Retreat escaped surface bounds");
                Check(!(bool)Get(a.Pet.Lizard,"dead") && !(bool)Get(b.Pet.Lizard,"dead"),"Retreat damaged a pet");
                settings.allowFighting=false; Call(host,"AdvanceScuffles",.025f);
                Check(!a.Conflict.HasYielded && !a.Motion.Behavior.TaskTarget.HasValue && !b.Motion.Behavior.HoldPosition,"Disabling fighting left retreat or watching locked");
            }
            finally { foreach(var pet in pets) pet.Dispose(); pets.Clear(); native.Dispose(); UnityEngine.Object.DestroyImmediate(root); }
            Debug.Log("DOMINANCE_YIELD_NERVOUS_WATCH_RETREAT_SPECIES_PROPULSION_CHECK_PASSED");
        }
    }
}
