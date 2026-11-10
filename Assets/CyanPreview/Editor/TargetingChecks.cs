using System;
using UnityEngine;
using static CyanPreview.ReferenceContext;

namespace CyanPreview.Editor
{
    public static class TargetingChecks
    {
        public static void Run(ReferenceContext context)
        {
            EdgeCases(context);
            var bounds = new Rect(-360, -180, 720, 360);
            using (var pet = context.CreatePet())
            {
                pet.Generate("12345"); pet.Pose(0, 0);
                var motion = new PetMotion(pet, new BackgroundSurfaces(bounds), 5) { Roam = false };
                for (int i = 0; i < 20; i++) motion.Advance(.025f);
                motion.BeginGrab(motion.Position);
                var limbs = (Array)Get(pet.Graphics, "limbs");
                var offsets = new Vector2[limbs.Length];
                for (int i = 0; i < limbs.Length; i++) offsets[i] = (Vector2)Get(limbs.GetValue(i), "pos") - motion.Position;
                motion.DragTo(new Vector2(220, 90));
                for (int i = 0; i < limbs.Length; i++)
                    Require(Vector2.Distance((Vector2)Get(limbs.GetValue(i), "pos") - motion.Position, offsets[i]) < .001f,
                        "Fast drag left a rendered foot behind before simulation tick");
                for (int step = 0; step < 20; step++)
                {
                    motion.DragTo(new Vector2(step % 2 == 0 ? -220 : 220, 80)); motion.Advance(.025f);
                    foreach (var limb in limbs)
                        Require(Vector2.Distance((Vector2)Get(limb, "pos"), (Vector2)Get(Get(limb, "connection"), "pos")) <= (float)Get(limb, "jointDist") + .01f,
                            "Fast drag stretched a foot beyond its reach");
                    pet.Draw();
                }
                Debug.Log("FAST_DRAG_POSE_AND_LIMB_REACH_CHECK_PASSED");
            }
            using (var pet = context.CreatePet())
            {
                pet.Generate("12345");
                var motion = new PetMotion(pet, new BackgroundSurfaces(bounds), 5) { Roam = false };
                motion.BeginGrab(motion.Position); motion.DragTo(new Vector2(0, 100));
                var anchor = motion.Position;
                var chunks = (Array)Get(pet.Lizard, "bodyChunks");
                for (int i = 0; i < 120; i++)
                {
                    motion.Advance(.025f);
                    Require(Vector2.Distance(anchor, motion.Position) < .001f, "Held point escaped the cursor");
                }
                var rear = (Vector2)Get(chunks.GetValue(2), "pos");
                Require(rear.y < anchor.y - 18, "Held body did not sag under gravity");
                motion.Advance(.025f);
                Require(Vector2.Distance(rear, (Vector2)Get(chunks.GetValue(2), "pos")) > .001f, "Held body stopped struggling");
                motion.EndGrab(); motion.Advance(.025f);
                Require(motion.Position.y < anchor.y, "Released body did not fall");
                Debug.Log("GRABBED_GRAVITY_ANCHOR_STRUGGLE_CHECK_PASSED");
            }
            foreach (IPetSurfaces surface in new IPetSurfaces[] { new FrameSurfaces(bounds), new BackgroundSurfaces(bounds), new PoleSurfaces(bounds, 0), new FrameSurfaces(new Rect(-360, -180, 380, 360)) })
            using (var pet = context.CreatePet())
            {
                pet.Generate("12345");
                var motion = new PetMotion(pet, surface, 5) { Roam = false };
                var chunks = (Array)Get(pet.Lizard, "bodyChunks");
                var initial = motion.Position;
                var forward = (initial - (Vector2)Get(chunks.GetValue(1), "pos")).normalized;
                motion.MoveTo(initial - forward * 90);
                bool turned = false;
                for (int i = 0; i < 180; i++)
                {
                    motion.Advance(.025f);
                    var facing = (motion.Position - (Vector2)Get(chunks.GetValue(1), "pos")).normalized;
                    turned |= Vector2.Dot(facing, -forward) > .5f;
                    if (!turned) Require(Vector2.Dot(motion.Position - initial, -forward) < 25, "Body travelled backwards before turning");
                }
                Require(turned, "Lizard failed to turn toward a target behind it: " + surface.GetType().Name);
                Debug.Log("REVERSE_TARGET_TURN_CHECK_PASSED " + surface.GetType().Name);
            }
            foreach (IPetSurfaces surfaces in new IPetSurfaces[] { new FrameSurfaces(bounds), new BackgroundSurfaces(bounds) })
            using (var pet = context.CreatePet())
            {
                pet.Generate("12345");
                var motion = new PetMotion(pet, surfaces, 5) { Roam = false, AutonomousJumps = false };
                var input = new PetInteraction(motion);
                var target = surfaces.SupportsFreeClimbing ? new Vector2(250, 80) : new Vector2(250, -180);
                var initial = motion.Position;
                input.Press(target);
                Require(motion.Behavior.Intent == PetIntent.Pursuing, "Click did not interrupt idle on press");
                Require(motion.Position == initial, "Click directly moved body instead of changing intent");
                input.Release(target, true);
                bool charge = false, flight = false, land = false;
                for (int step = 0; step < 900; step++)
                {
                    motion.Advance(.025f);
                    charge |= motion.State == PetMotionState.Charging;
                    flight |= motion.State == PetMotionState.Airborne;
                    land |= flight && motion.State == PetMotionState.Crawling;
                }
                Require(charge && flight && land, "Distant target did not use propulsion: " + surfaces.GetType().Name);
                Require(Vector2.Distance(motion.Position, target) < 55, "Exploration lost target after landing: " + motion.Position);
                Debug.Log("DISTANT_CLICK_PROPULSION_CHECK_PASSED " + surfaces.GetType().Name);
            }
            using (var pet = context.CreatePet())
            {
                pet.Generate("12345");
                var motion = new PetMotion(pet, new BackgroundSurfaces(bounds), 5) { Roam = false };
                var input = new PetInteraction(motion) { ClickToExplore = false };
                var target = motion.Position + Vector2.right * 65;
                input.Press(target); input.Release(target, true);
                Require(motion.Behavior.Intent == PetIntent.Idle, "Disabled click interrupted idle");
                input.ClickToExplore = true; input.Press(target); input.Release(target, true);
                var initial = motion.Position;
                for (int step = 0; step < 120; step++)
                {
                    var previous = motion.Position; motion.Advance(.025f);
                    Require(motion.State == PetMotionState.Crawling, "Nearby exploration unnecessarily jumped");
                    Require(Vector2.Distance(previous, motion.Position) < 8, "Exploration snapped the body");
                }
                Require(Vector2.Distance(initial, motion.Position) > 20, "Nearby exploration never started crawling");
                Debug.Log("NEAR_CLICK_CRAWL_INTERRUPT_CHECK_PASSED");
            }
        }
        static void EdgeCases(ReferenceContext context)
        {
            var bounds = new Rect(-360, -180, 720, 360);
            var surface = new BackgroundSurfaces(bounds);
            foreach (int seed in new[] { 0, 42, 12345 })
            foreach (int held in new[] { 0, 1, 2 })
            using (var pet = context.CreatePet())
            {
                pet.Generate(seed.ToString());
                var motion = new PetMotion(pet, surface, seed) { Roam = false, AutonomousJumps = true };
                var chunks = (Array)Get(pet.Lizard, "bodyChunks");
                var input = new PetInteraction(motion);
                foreach (var cursor in new[] { new Vector2(10000, 10000), new Vector2(-10000, 10000), new Vector2(-10000, -10000), new Vector2(10000, -10000), new Vector2(0, 100) })
                {
                    var selected = (Vector2)Get(chunks.GetValue(held), "pos");
                    input.Press(selected); input.Hold(selected, .1f);
                    Require(motion.State == PetMotionState.Grabbed, "Exact hold threshold failed");
                    input.Hold(cursor, .025f);
                    var anchor = (Vector2)Get(chunks.GetValue(held), "pos");
                    for (int step = 0; step < 30; step++)
                    {
                        motion.Advance(step % 2 == 0 ? .01f : .04f);
                        Require(Vector2.Distance(anchor, (Vector2)Get(chunks.GetValue(held), "pos")) < .001f, "Middle/rear grip drifted");
                        foreach (var chunk in chunks)
                        {
                            var position = (Vector2)Get(chunk, "pos");
                            Require(!float.IsNaN(position.x) && !float.IsNaN(position.y) && !float.IsInfinity(position.x) && !float.IsInfinity(position.y), "Non-finite held pose");
                            Require(Vector2.Distance(position, surface.Constrain(position, 8, out _)) < .01f, "Held body escaped boundary: " + position + " seed=" + seed + " held=" + held + " cursor=" + cursor);
                        }
                        Require(Vector2.Distance((Vector2)Get(chunks.GetValue(0), "pos"), (Vector2)Get(chunks.GetValue(2), "pos")) < 60, "Held body joints stretched");
                    }
                    input.Release(cursor, false); input.Cancel();
                    Require(motion.State == PetMotionState.Airborne, "Outside release or repeat cancel left a grip");
                    motion.Advance(.025f);
                }
            }
            Debug.Log("GRAB_BODY_POINTS_BOUNDARIES_REPEATED_RELEASE_CHECK_PASSED");
            foreach (bool duringFlight in new[] { false, true })
            using (var pet = context.CreatePet())
            {
                pet.Generate("42");
                var motion = new PetMotion(pet, new FrameSurfaces(bounds), 42) { Roam = false };
                for (int i = 0; i < 40; i++) motion.Advance(.025f);
                int launches = 0; motion.JumpLaunched += (p, v, power) => launches++;
                Require(motion.JumpTo(new Vector2(100, 80)), "Interrupt test could not start jump");
                if (duringFlight) for (int i = 0; i < 15; i++) motion.Advance(.025f);
                Require(motion.State == (duringFlight ? PetMotionState.Airborne : PetMotionState.Charging), "Wrong interruption state");
                motion.BeginGrab(motion.Position); motion.DragTo(new Vector2(0, 100));
                for (int i = 0; i < 80; i++) motion.Advance(.025f);
                Require(motion.State == PetMotionState.Grabbed && launches == (duringFlight ? 1 : 0), "Grab failed to cancel jump");
                motion.EndGrab();
                for (int i = 0; i < 240; i++) motion.Advance(.025f);
                Require(motion.State == PetMotionState.Crawling, "Interrupted jump did not recover after release");
            }
            Debug.Log("GRAB_CHARGE_FLIGHT_INTERRUPT_CHECK_PASSED");
            using (var first = context.CreatePet())
            using (var second = context.CreatePet())
            {
                first.Generate("42"); second.Generate("42");
                var a = new PetMotion(first, surface, 42) { Roam = false };
                var b = new PetMotion(second, surface, 42) { Roam = false };
                var untouched = b.Position;
                a.BeginGrab(a.Position); a.DragTo(new Vector2(0, 100));
                for (int i = 0; i < 120; i++) a.Advance(.025f);
                Require(b.Position == untouched && b.State == PetMotionState.Crawling, "Grabbing affected another pet");
                b.BeginGrab(b.Position); b.DragTo(new Vector2(0, 100));
                for (int i = 0; i < 60; i++) b.Advance(.05f);
                var ac = (Array)Get(first.Lizard, "bodyChunks"); var bc = (Array)Get(second.Lizard, "bodyChunks");
                for (int i = 0; i < 3; i++) Require(Vector2.Distance((Vector2)Get(ac.GetValue(i), "pos"), (Vector2)Get(bc.GetValue(i), "pos")) < .001f, "Hold depends on render frame rate");
            }
            Debug.Log("GRAB_PET_ISOLATION_FIXED_TIMESTEP_CHECK_PASSED");
        }
        static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    }
}
