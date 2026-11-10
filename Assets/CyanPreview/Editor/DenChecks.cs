using System;
using UnityEngine;
using static CyanPreview.ReferenceContext;

namespace CyanPreview.Editor
{
    public static class DenChecks
    {
        static void Require(bool value, string message) { if (!value) throw new Exception(message); }
        public static void Run(ReferenceContext context)
        {
            var randomState = UnityEngine.Random.state;
            var bounds = new Rect(-360, -180, 720, 360);
            foreach (IPetSurfaces surface in new IPetSurfaces[] { new FrameSurfaces(bounds), new BackgroundSurfaces(bounds), new PoleSurfaces(bounds, 0) })
            using (var pet = context.CreatePet())
            {
                pet.Generate("12345");
                var den = new PetDen(surface, bounds, new Vector2(-180, -120));
                var motion = new PetMotion(pet, surface, 42) { Roam = false };
                var journey = new PetDenJourney(motion, den);
                var input = new PetInteraction(motion, journey);
                journey.Spawn(); Require(!journey.Visible, "Spawn should begin inside");
                journey.Spawn();
                for (int i = 0; i < 100; i++) { journey.Advance(.025f); pet.Draw(); }
                Require(journey.Phase == DenPhase.Outside && journey.Visible, "Spawn failed to emerge " + surface.GetType().Name);
                for (int i = 0; i < 400; i++) journey.Advance(.025f);
                Require(journey.Phase == DenPhase.Outside, "Ordinary rest automatically returned to den");
                input.Press(den.Position); input.Hold(new Vector2(10000, 10000), .2f);
                Require(den.Dragging && motion.State != PetMotionState.Grabbed, "Den drag grabbed pet");
                Require(den.Position.x <= bounds.xMax - PetDen.Size / 2 && den.Position.y <= bounds.yMax - PetDen.Size / 2, "Den escaped viewport");
                input.Cancel(); Require(!den.Dragging, "Focus loss left den dragging");
                den.Move(new Vector2(-120, -110));
                Require(journey.RequestFoodReturn() && !journey.RequestFoodReturn(), "Return request not idempotent");
                bool entered = false;
                for (int i = 0; i < 2400; i++)
                {
                    if (i == 20) den.Move(new Vector2(-60, -100));
                    journey.Advance(.025f); pet.Draw();
                    if (journey.Phase == DenPhase.Entering) { entered = true; break; }
                }
                Require(entered, "Food return did not reach relocated den " + surface.GetType().Name);
                den.Move(new Vector2(60, 100)); journey.Advance(.025f);
                Require(journey.Phase == DenPhase.Returning, "Moving entrance did not abort entry");
                bool inside = false;
                for (int i = 0; i < 2400; i++)
                {
                    journey.Advance(.025f); pet.Draw();
                    if (journey.Phase == DenPhase.Inside) { inside = true; break; }
                }
                Require(inside && !journey.Visible, "Food return did not enter den");
                input.Press(new Vector2(250, -50)); input.Hold(new Vector2(250, -50), .2f); input.Release(new Vector2(250, -50), true);
                Require(!motion.Behavior.InterestPoint.HasValue && motion.State != PetMotionState.Grabbed, "Hidden pet accepted a click or grab");
                input.Press(den.Position); input.Hold(new Vector2(-100, 40), .2f);
                for (int i = 0; i < 250; i++) journey.Advance(.025f);
                Require(journey.Phase == DenPhase.Inside, "Occupied den emerged during dragging");
                input.Release(den.Position, false);
                for (int i = 0; i < 400; i++) { journey.Advance(.025f); pet.Draw(); }
                Require(journey.Phase == DenPhase.Outside && journey.Visible, "Meal rest did not emerge automatically");
                Require(Vector2.Distance(motion.Position, den.Entrance) < 120, "Emerged from stale den location");
                journey.RequestFoodReturn(); motion.BeginGrab(motion.Position); journey.Advance(.025f);
                Require(journey.Phase == DenPhase.Outside && motion.State == PetMotionState.Grabbed, "Grab did not interrupt return");
                Require(!journey.RequestFoodReturn(), "Food return interrupted a grab");
                motion.EndGrab();
                journey.Spawn(); journey.ComeOut(); journey.Advance(.025f);
                Require(journey.Phase == DenPhase.Emerging, "Come out failed");
                den.Move(new Vector2(80, -70)); journey.Advance(.025f);
                Require(journey.Phase == DenPhase.Inside, "Moving den during exit left detached emergence");
                for (int i = 0; i < 100; i++) journey.Advance(.025f);
                Require(journey.Phase == DenPhase.Outside, "Relocated emergence failed to complete");
                var validPosition = den.Position; bool rejected = false;
                try { den.Move(new Vector2(float.NaN, 0)); } catch (ArgumentOutOfRangeException) { rejected = true; }
                Require(rejected && den.Position == validPosition, "Invalid den location damaged placement");
                Debug.Log("DEN_DRAG_SPAWN_RETURN_MOVE_ENTER_REST_EMERGE_CHECK_PASSED " + surface.GetType().Name);
            }
            using (var a = context.CreatePet())
            using (var b = context.CreatePet())
            {
                a.Generate("42"); b.Generate("42");
                var surface = new BackgroundSurfaces(bounds);
                var denA = new PetDen(surface, bounds, Vector2.zero);
                var denB = new PetDen(surface, bounds, Vector2.right * 100);
                var ma = new PetMotion(a, surface, 42); var mb = new PetMotion(b, surface, 42);
                var ja = new PetDenJourney(ma, denA); var jb = new PetDenJourney(mb, denB);
                var untouched = mb.Position; ja.Spawn(); denA.Move(new Vector2(40, 40));
                for (int i = 0; i < 100; i++) ja.Advance(.025f);
                Require(jb.Phase == DenPhase.Outside && mb.Position == untouched && denB.Position == Vector2.right * 100, "Den state leaked across pets");
            }
            Require(UnityEngine.Random.state.Equals(randomState), "Den animation leaked global randomness");
            Debug.Log("DEN_PET_OWNERSHIP_CHECK_PASSED");
        }
    }
}
