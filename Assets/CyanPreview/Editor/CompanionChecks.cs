using System;
using UnityEngine;
using static CyanPreview.ReferenceContext;

namespace CyanPreview.Editor
{
    public static class CompanionChecks
    {
        static void Require(bool value, string message) { if (!value) throw new Exception(message); }
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
                for (int i = 0; i < 160 && behavior.Intent == PetIntent.Inspecting; i++) behavior.Update(new Vector2(100, 20), surface);
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
