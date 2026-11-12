using System;
using UnityEngine;
using static CyanPreview.ReferenceContext;

namespace CyanPreview.Editor
{
    public static class CompanionChecks
    {
        static void Require(bool value, string message) { if (!value) throw new Exception(message); }
        public static void VerifyIdleStability()
        { using(var context=new ReferenceContext(System.IO.Path.GetFullPath("local-data"))) IdleStability(context); }
        public static void IdleStability(ReferenceContext context)
        {
            float worst=0;
            foreach(var scenario in new[]{"far","near","behind","rear-crossing","range-crossing","observe-boundary","cursor-disappears","centre"})
            using(var pet=context.CreatePet())
            {
                pet.Generate("9320");
                var motion=new PetMotion(pet,new BackgroundSurfaces(new Rect(-700,-400,1400,800)),42) {Roam=false,AutonomousJumps=false};
                float maxBodyStep=0,maxHeadStep=0,maxJerk=0,bodyTravel=0,tailTravel=0;
                Vector2 previousDelta=Vector2.zero;
                var tail=((Array)Get(pet.Graphics,"tail")).GetValue(0);
                for(int i=0;i<800;i++)
                {
                    Vector2 cursor=scenario=="far"?new Vector2(600,200):scenario=="near"?new Vector2(80,80):
                        scenario=="behind"?new Vector2(-130,0):scenario=="rear-crossing"?new Vector2(-130,i%2==0?.1f:-.1f):
                        scenario=="centre"?motion.Position:motion.HeadPosition+new Vector2(0,i%2==0?179.9f:180.1f);
                    if(scenario=="observe-boundary") motion.ObserveCursor(motion.Position+new Vector2(0,i%2==0?209.9f:210.1f));
                    else if(scenario=="cursor-disappears") motion.ObserveCursor(i%2==0?(Vector2?)new Vector2(100,50):null);
                    else motion.LookAt(cursor);
                    var before=motion.Position; var headBefore=motion.HeadPosition; var tailBefore=(Vector2)Get(tail,"pos");
                    motion.Advance(.025f);
                    if(i<200) continue;
                    var delta=motion.Position-before;
                    maxBodyStep=Mathf.Max(maxBodyStep,delta.magnitude); maxHeadStep=Mathf.Max(maxHeadStep,Vector2.Distance(headBefore,motion.HeadPosition));
                    maxJerk=Mathf.Max(maxJerk,(delta-previousDelta).magnitude); previousDelta=delta;
                    bodyTravel+=delta.magnitude; tailTravel+=Vector2.Distance(tailBefore,(Vector2)Get(tail,"pos"));
                    Require(!float.IsNaN(motion.Position.x) && motion.Position.magnitude<60,"Idle body drifted or became non-finite");
                }
                Debug.Log($"IDLE_STABILITY {scenario} bodyStep={maxBodyStep:F3} headStep={maxHeadStep:F3} jerk={maxJerk:F3} bodyTravel={bodyTravel:F3} tailTravel={tailTravel:F3}");
                worst=Mathf.Max(worst,maxBodyStep);
                Require(maxHeadStep<.5f && maxJerk<.25f,"Idle gaze boundary produced abrupt head/body motion");
                Require(bodyTravel>.1f && tailTravel>.1f,"Idle body/tail animation froze");
            }
            Require(worst<.15f,"Flat-background idle correction exceeded .15 units per tick");
            Debug.Log("FLAT_BACKGROUND_IDLE_STABILITY_CHECK_PASSED");
        }
        public static void VerifyEdgeFootholds()
        { using(var context=new ReferenceContext(System.IO.Path.GetFullPath("local-data"))) EdgeFootholds(context); }
        public static void EdgeFootholds(ReferenceContext context)
        {
            int worstUnreachable=0,worstResets=0;
            foreach(var scenario in new[]{"floor","wall","corner","window","window-raised","window-background","floor-raised"})
            using(var pet=context.CreatePet())
            {
                pet.Generate("9320");
                var surfaces=new DesktopSurfaces(new Rect(-700,-400,1400,800)) {Mode=scenario=="window-background"?ClimbingMode.EdgesAndBackgrounds:ClimbingMode.EdgesOnly};
                if(scenario.StartsWith("window")) surfaces.UpdateWindows(new[]{new DesktopWindow(1,new Rect(-200,-100,400,200))});
                var motion=new PetMotion(pet,surfaces,42) {Roam=false,AutonomousJumps=false};
                var origin=scenario=="wall"?new Vector2(686,0):scenario=="corner"?new Vector2(680,-380):
                    scenario.StartsWith("window")?new Vector2(0,scenario=="window"?86:72):new Vector2(0,scenario=="floor-raised"?-372:-386);
                var forward=scenario=="wall"||scenario=="corner"?Vector2.up:Vector2.right;
                var chunks=(Array)Get(pet.Lizard,"bodyChunks");
                for(int i=0;i<3;i++) { Set(chunks.GetValue(i),"pos",origin-forward*17*i); Set(chunks.GetValue(i),"lastPos",origin-forward*17*i); Set(chunks.GetValue(i),"vel",Vector2.zero); }
                var anchors=(Vector2[])Get(motion,"anchors"); var ages=(int[])Get(motion,"gripAge"); var seeking=(bool[])Get(motion,"seeking"); var planted=(bool[])Get(motion,"planted");
                var limbs=(Array)Get(pet.Graphics,"limbs"); var oldAges=new int[4];
                int resets=0,unreachable=0; float travel=0,maxStep=0;
                for(int t=0;t<800;t++)
                {
                    motion.ObserveCursor(null); var before=motion.Position; motion.Advance(.025f);
                    if(t>=200)
                    {
                        travel+=Vector2.Distance(before,motion.Position); maxStep=Mathf.Max(maxStep,Vector2.Distance(before,motion.Position));
                        for(int i=0;i<4;i++)
                        {
                            if(oldAges[i]>0 && ages[i]<=1) resets++;
                            if((seeking[i] || planted[i]) && Vector2.Distance(anchors[i],motion.BodyPosition(i<2?0:2))>(float)Get(limbs.GetValue(i),"jointDist")+.1f) unreachable++;
                        }
                    }
                    Array.Copy(ages,oldAges,4);
                }
                Debug.Log($"EDGE_FOOTHOLDS {scenario} resets={resets} unreachable={unreachable} grips={motion.GripCount} travel={travel:F3} maxStep={maxStep:F3}");
                worstUnreachable=Mathf.Max(worstUnreachable,unreachable); worstResets=Mathf.Max(worstResets,resets);
                Require(travel>.1f,"Edge idle movement froze");
                Require(motion.GripCount==4,"Reachable idle edge failed to retain four grips");
                if(scenario=="floor")
                {
                    var start=motion.Position; int steps=0; float progress=0;
                    motion.MoveTo(surfaces.Advance(start,140));
                    for(int tick=0;tick<160;tick++)
                    {
                        var before=(Vector2[])anchors.Clone(); motion.Advance(.025f);
                        progress=Mathf.Max(progress,Vector2.Distance(start,motion.Position));
                        for(int i=0;i<4;i++) if(Vector2.Distance(before[i],anchors[i])>.5f) steps++;
                    }
                    Require(progress>20 && steps>0,"Stable resting feet prevented normal crawling/stepping");
                }
                if(scenario=="window")
                {
                    var oldAnchors=(Vector2[])anchors.Clone(); var shift=new Vector2(50,30);
                    var delta=surfaces.UpdateWindows(new[]{new DesktopWindow(1,new Rect(-150,-70,400,200))},motion.Position,true);
                    Require(Vector2.Distance(delta,shift)<.01f,"Window-move fixture lost support");
                    motion.ShiftWithSurface(delta);
                    for(int i=0;i<4;i++) Require(Vector2.Distance(anchors[i],oldAnchors[i]+shift)<.01f,"Moving window detached stationary foot anchor");
                    surfaces.UpdateWindows(new DesktopWindow[0]); motion.RefreshSurfaceSupport(); motion.Advance(.025f);
                    Require(motion.State==PetMotionState.Airborne && motion.GripCount==0,"Closed window retained false foot support");
                }
            }
            Require(worstUnreachable==0 && worstResets<8,"Resting edge feet repeatedly seek unreachable footholds or reset grips");
            Debug.Log("EDGE_IDLE_FOOTHOLD_STABILITY_CHECK_PASSED");
        }
        public static void VerifyRestingForces()
        { using(var context=new ReferenceContext(System.IO.Path.GetFullPath("local-data"))) RestingForces(context); }
        public static void RestingForces(ReferenceContext context)
        {
            float straightError=0;
            foreach(var scenario in new[]{"raised-floor","raised-window","tilted-floor","corner","window-threshold"})
            using(var pet=context.CreatePet())
            {
                pet.Generate("9320");
                var surfaces=new DesktopSurfaces(new Rect(-700,-400,1400,800)) {Mode=scenario=="window-threshold"?ClimbingMode.EdgesAndBackgrounds:ClimbingMode.EdgesOnly};
                if(scenario.Contains("window")) surfaces.UpdateWindows(new[]{new DesktopWindow(1,new Rect(-200,-100,400,200))});
                var motion=new PetMotion(pet,surfaces,42) {Roam=false,AutonomousJumps=false};
                var origin=scenario.Contains("window")?new Vector2(0,scenario=="window-threshold"?70:72):scenario=="corner"?new Vector2(680,-380):new Vector2(0,-372);
                var forward=scenario=="tilted-floor"?new Vector2(.8f,.6f):scenario=="corner"?new Vector2(.6f,.8f):Vector2.right;
                var chunks=(Array)Get(pet.Lizard,"bodyChunks");
                for(int i=0;i<3;i++) { Set(chunks.GetValue(i),"pos",origin-forward*17*i); Set(chunks.GetValue(i),"lastPos",origin-forward*17*i); Set(chunks.GetValue(i),"vel",Vector2.zero); }
                float error=0,opposed=0,travel=0,maxStep=0; int samples=0,driveTicks=0;
                var tail=((Array)Get(pet.Graphics,"tail")).GetValue(0); float tailTravel=0;
                for(int tick=0;tick<800;tick++)
                {
                    motion.ObserveCursor(null); var before=motion.Position; var tailBefore=(Vector2)Get(tail,"pos"); motion.Advance(.025f);
                    if(tick<200) continue;
                    if(motion.DiagnosticDriveTarget.HasValue || motion.Behavior.TaskTarget.HasValue) driveTicks++;
                    travel+=Vector2.Distance(before,motion.Position); tailTravel+=Vector2.Distance(tailBefore,(Vector2)Get(tail,"pos")); maxStep=Mathf.Max(maxStep,Vector2.Distance(before,motion.Position));
                    var rest=(Vector2[])Get(motion,"restingPose");
                    for(int i=0;i<3;i++)
                    {
                        var pos=motion.BodyPosition(i); var support=surfaces.Nearest(pos,out var normal); if(normal==Vector2.zero) continue;
                        float gap=Vector2.Dot(support+normal*14-pos,normal);
                        float restPull=Vector2.Dot(rest[i]-pos,normal);
                        error+=Mathf.Abs(gap); if(gap*restPull<0) opposed+=Mathf.Min(Mathf.Abs(gap)*.18f,Mathf.Abs(restPull)*.12f); samples++;
                    }
                }
                float mean=error/Mathf.Max(1,samples);
                Debug.Log($"REST_FORCES {scenario} supportError={mean:F3} opposedEstimate={opposed/Mathf.Max(1,samples):F3} driveTicks={driveTicks} travel={travel:F3} maxStep={maxStep:F3} tailTravel={tailTravel:F3}");
                Require(driveTicks==0,"Resting pet still had a movement destination");
                Require(travel>.1f && tailTravel>.1f && maxStep<1,"Resting edge motion froze or remained unstable");
                if(scenario=="raised-floor" || scenario=="raised-window") straightError=Mathf.Max(straightError,mean);
            }
            Require(straightError<1,"Saved resting pose keeps pulling body away from edge support");
            Debug.Log("SUPPORTED_EDGE_RESTING_FORCES_CHECK_PASSED");
        }
        public static void Run(ReferenceContext context)
        {
            var surface = new BackgroundSurfaces(new Rect(-360, -180, 720, 360));
            foreach (int seed in new[] { 0, 42, 12345 })
            {
                var behavior = new PetBehavior(seed) { Roam = false };
                behavior.Target(Vector2.zero);
                behavior.Update(Vector2.zero, surface, false);
                Require(behavior.Intent == PetIntent.Investigating, "Arrival began before landing");
                behavior.Update(Vector2.zero, surface);
                Require(behavior.Intent == PetIntent.Inspecting, "Arrival skipped inspection");
                for (int i = 0; i < 79; i++) behavior.Update(Vector2.zero, surface);
                Require(behavior.Intent == PetIntent.Inspecting, "Inspection ended prematurely");
                bool settled = false;
                for (int i = 0; i < 300; i++) { behavior.Update(Vector2.zero, surface); settled |= behavior.Intent == PetIntent.Settling; }
                Require(settled && behavior.Intent == PetIntent.Idle && !behavior.InterestPoint.HasValue, "Inspection failed to settle and clear interest");
                behavior.Target(Vector2.zero); behavior.Update(Vector2.zero, surface);
                behavior.Target(new Vector2(100, 20));
                Require(behavior.Intent == PetIntent.Investigating && behavior.ExplorationTarget == new Vector2(100, 20), "Repeat click did not replace inspection");
                behavior.Update(new Vector2(100, 20), surface);
                for (int i = 0; i < 400 && behavior.Intent == PetIntent.Inspecting; i++) behavior.Update(new Vector2(100, 20), surface);
                Require(behavior.Intent == PetIntent.Settling, "Missing settling phase");
                behavior.Target(new Vector2(-100, 20));
                Require(behavior.Intent == PetIntent.Investigating, "Click did not interrupt settling");
                behavior.Rest(); Require(!behavior.InterestPoint.HasValue, "Grab/rest retained investigation");
                behavior.Roam = true; behavior.Target(Vector2.zero); behavior.Update(Vector2.zero, surface);
                for (int i = 0; i < 700; i++) behavior.Update(Vector2.zero, surface);
                Require(behavior.Intent != PetIntent.Inspecting && behavior.Intent != PetIntent.Settling, "Autonomy stuck in inspection");
            }
            foreach (IPetSurfaces geometry in new IPetSurfaces[] { surface, new FrameSurfaces(new Rect(-360, -180, 720, 360)), new PoleSurfaces(new Rect(-360, -180, 720, 360), 0) })
            foreach (Vector2 point in new[] { Vector2.zero, new Vector2(352, 172), new Vector2(-352, -172) })
            {
                var clicked = geometry.Nearest(point, out _);
                var behavior = new PetBehavior(42) { Roam = false };
                behavior.Target(clicked); behavior.Update(clicked, geometry);
                int goals = 0;
                for (int i = 0; i < 160; i++)
                {
                    var goal = behavior.Update(clicked, geometry);
                    if (!goal.HasValue) continue;
                    goals++;
                    Require(Vector2.Distance(clicked, goal.Value) <= 41, "Inspection wandered outside clicked neighbourhood");
                    Require(Vector2.Distance(goal.Value, geometry.Nearest(goal.Value, out _)) < .01f, "Inspection selected an unsupported position");
                    Require(!behavior.ExplorationTarget.HasValue, "Inspection became a propulsion target");
                }
                Require(goals > 0, "Inspection never explored around click");
            }
            using (var pet = context.CreatePet())
            {
                pet.Generate("42");
                var motion = new PetMotion(pet, surface, 42) { Roam = false };
                var clicked = motion.Position;
                motion.MoveTo(clicked); motion.Advance(.025f);
                float moved = 0;
                for (int i = 0; i < 75; i++)
                {
                    var cursor = new Vector2(i % 2 == 0 ? -200 : 200, 100);
                    motion.LookAt(cursor);
                    var before = motion.Position; motion.Advance(.025f);
                    moved += Vector2.Distance(before, motion.Position);
                    Require((Vector2)Get(pet.Graphics, "lookPos") == cursor, "Inspection ignored live cursor attention");
                    Require(motion.Behavior.InterestPoint == clicked, "Cursor motion moved the clicked exploration centre");
                    Require(motion.State == PetMotionState.Crawling, "Local exploration launched unnecessarily");
                }
                Require(moved > 5, "Local inspection did not move body");
            }
            Debug.Log("CURSOR_ATTENTION_LOCAL_EXPLORATION_CHECK_PASSED");
            using (var first = context.CreatePet())
            using (var second = context.CreatePet())
            {
                var random = UnityEngine.Random.state;
                first.Generate("42"); second.Generate("42");
                Require(first.Personality.ToString() == second.Personality.ToString(), "Duplicate IDs differ in personality");
                var personality = first.Personality.ToString(); second.Generate("12345");
                Require(first.Personality.ToString() == personality && second.Personality.ToString() != personality, "Personality ownership or generation failed");
                var original = Get(Get(first.Lizard, "abstractPhysicalObject"), "personality");
                Require(first.Personality.Energy == (float)Get(original, "energy") && first.Personality.Dominance == (float)Get(original, "dominance"), "Personality differs from original game values");
                Require(UnityEngine.Random.state.Equals(random), "Personality leaked global randomness");
                var motion = new PetMotion(first, surface, 42) { Roam = false };
                motion.MoveTo(motion.Position + Vector2.right * 65);
                bool inspect = false, settle = false;
                for (int i = 0; i < 800; i++)
                {
                    motion.Advance(.025f); inspect |= motion.Behavior.Intent == PetIntent.Inspecting;
                    settle |= motion.Behavior.Intent == PetIntent.Settling;
                }
                Require(inspect && settle && motion.Behavior.Intent == PetIntent.Idle, "Movement did not inspect then settle");
                var start = motion.Position; float movement = 0;
                for (int i = 0; i < 160; i++)
                {
                    var before = motion.Position; motion.Advance(.025f);
                    movement += Vector2.Distance(before, motion.Position);
                    Require(Vector2.Distance(start, motion.Position) < 20, "Idle pose drifted away");
                }
                Require(movement > .1f, "Idle body froze");
                motion.MoveTo(motion.Position); motion.Advance(.025f);
                motion.BeginGrab(motion.Position);
                Require(!motion.Behavior.InterestPoint.HasValue, "Grab did not cancel inspection");
            }
            Debug.Log("COMPANION_PERSONALITY_INSPECT_SETTLE_IDLE_CHECK_PASSED");
        }
    }
}
