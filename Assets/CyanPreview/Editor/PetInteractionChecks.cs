using System;
using UnityEngine;

namespace CyanPreview.Editor
{
    public static class PetInteractionChecks
    {
        public static void Run(ReferenceContext context)
        {
            var bounds = new Rect(-130, -42, 180, 84);
            using (var pet = context.CreatePet())
            {
                pet.Generate("12345");
                var motion = new PetMotion(pet, new FrameSurfaces(bounds), 73) { Roam = false };
                var input = new PetInteraction(motion) { ClickToExplore = false };
                var target = new Vector2(40, 30);
                input.Press(target); input.Release(target, true); motion.Advance(.025f);
                Require(motion.Behavior.Intent == PetIntent.Idle, "Disabled exploration accepted a click");
                input.ClickToExplore = true;
                input.Press(target); input.Release(target, true); motion.Advance(.025f);
                Require(motion.Behavior.Intent == PetIntent.Investigating, "Enabled exploration ignored a click");
                var origin = motion.Position;
                input.Press(origin); input.Hold(origin, .05f);
                Require(motion.State != PetMotionState.Grabbed, "Short press grabbed the pet");
                input.Hold(origin, .06f);
                Require(motion.State == PetMotionState.Grabbed, "Long press did not grab");
                var dragged = origin + new Vector2(-20, 20);
                input.Hold(dragged, .025f);
                Require(Vector2.Distance(motion.Position, dragged) < .01f, "Grab did not follow cursor");
                motion.AutonomousJumps = true;
                for (int i = 0; i < 500; i++) motion.Advance(.025f);
                Require(motion.State == PetMotionState.Grabbed && motion.Position == dragged, "Autonomy interrupted grab");
                input.Release(dragged, true);
                Require(motion.State == PetMotionState.Airborne, "Release did not drop the pet");
                Require(motion.Behavior.Intent == PetIntent.Idle, "Drag release became exploration");
                for (int i = 0; i < 240; i++) motion.Advance(.025f);
                Require(motion.State == PetMotionState.Crawling, "Dropped pet did not regain footing");
                input.Press(motion.Position); input.Hold(motion.Position, .4f); input.Cancel();
                Require(motion.State != PetMotionState.Grabbed, "Focus loss left pet grabbed");
                input.Press(motion.Position); input.Hold(motion.Position, .05f); input.Release(motion.Position, true);
                Require(!input.CapturesPointer && motion.State != PetMotionState.Grabbed, "Short click retained a grab");
                input.Press(motion.Position); input.Cancel(); input.Hold(motion.Position, .2f);
                Require(!input.CapturesPointer && motion.State != PetMotionState.Grabbed, "Cancelled pending hold became a grab");
                var beforeFastGrab = motion.Position;
                input.Press(beforeFastGrab);
                Require(input.CapturesPointer, "Pending hold did not retain pointer ownership");
                var fastTarget = beforeFastGrab + new Vector2(-20, 20);
                input.Hold(fastTarget, .11f);
                Require(motion.State == PetMotionState.Grabbed && Vector2.Distance(motion.Position, fastTarget) < .01f,
                    "Moving cursor before hold threshold lost the grab");
                input.Release(fastTarget, false);
                Require(!input.CapturesPointer && motion.State != PetMotionState.Grabbed, "Release outside work area left grab active");
                input.Press(new Vector2(1000, 1000)); input.Hold(motion.Position, .2f);
                Require(!input.CapturesPointer && motion.State != PetMotionState.Grabbed, "Application drag crossing pet stole pointer");
                input.Cancel();
                Debug.Log("HOLD_GRAB_DROP_EXPLORE_TOGGLE_CHECK_PASSED");
            }
            using (var pet = context.CreatePet())
            {
                pet.Generate("12345");
                var motion = new PetMotion(pet, new FrameSurfaces(bounds), 42) { AutonomousJumps = true };
                Require(motion.Behavior.Intent == PetIntent.Idle, "New pet did not start idle");
                bool charged = false, flew = false, landed = false;
                for (int i = 0; i < 2400 && !landed; i++)
                {
                    motion.Advance(.025f);
                    charged |= motion.State == PetMotionState.Charging;
                    flew |= motion.State == PetMotionState.Airborne;
                    landed |= flew && motion.State == PetMotionState.Crawling;
                }
                Require(charged && flew && landed, "Autonomous jump did not charge, launch and land");
                Debug.Log("AUTONOMOUS_JUMP_LANDING_CHECK_PASSED");
            }
            foreach (IPetSurfaces surfaces in new IPetSurfaces[] { new BackgroundSurfaces(bounds), new PoleSurfaces(bounds, -40) })
            using (var pet = context.CreatePet())
            {
                pet.Generate("12345");
                var motion = new PetMotion(pet, surfaces, 1) { Roam = false };
                var initial = motion.Position;
                motion.MoveTo(new Vector2(-40, 35));
                bool climbedWithGrip = false;
                for (int i = 0; i < 300; i++)
                {
                    motion.Advance(.025f);
                    climbedWithGrip |= motion.Position.y > initial.y + 8 && motion.GripCount > 0;
                }
                // Inspection may walk back down after reaching the destination.
                Require(climbedWithGrip, surfaces.GetType().Name + " did not climb and grip");
                pet.Draw();
                Debug.Log("CLIMB_SURFACE_CHECK_PASSED " + surfaces.GetType().Name);
            }
        }
        static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    }
}
