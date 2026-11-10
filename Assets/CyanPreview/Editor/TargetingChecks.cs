using System;
using UnityEngine;
using static CyanPreview.ReferenceContext;

namespace CyanPreview.Editor
{
    public static class TargetingChecks
    {
        public static void Run(ReferenceContext context)
        {
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
        static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    }
}
