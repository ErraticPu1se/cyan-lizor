using System;
using UnityEngine;

namespace CyanPreview
{
    public enum PetIntent { Idle, Roaming, Investigating, Inspecting, Settling }

    // Friendly desktop investigation, informed by LizardAI idle and FriendTracker.
    // No game prey, threat, hunger or relationship tracker is fabricated here.
    public sealed class PetBehavior
    {
        readonly System.Random random;
        Vector2? destination;
        bool explicitTarget;
        int restTicks, roamTicks, interestTicks, inspectionStepTicks;
        Vector2? interest;
        public Vector2? InterestPoint => interest;
        public const int IdleMinTicks = 320, IdleMaxTicks = 601;
        public const int RoamMinTicks = 600, RoamMaxTicks = 1001;
        public bool Roam { get; set; } = true;
        public PetIntent Intent { get; private set; }
        public float RunSpeed { get; private set; }
        public PetBehavior(int seed) { random = new System.Random(seed); Rest(); }
        public void Rest()
        {
            destination = null; explicitTarget = false; interest = null; interestTicks = 0;
            restTicks = random.Next(IdleMinTicks, IdleMaxTicks); roamTicks = 0; Intent = PetIntent.Idle;
        }
        public Vector2? ExplorationTarget => explicitTarget ? destination : null;
        public void Target(Vector2 point)
        {
            destination = point; interest = point; interestTicks = 0; explicitTarget = true; restTicks = roamTicks = 0;
            Intent = PetIntent.Investigating;
        }
        public Vector2? Update(Vector2 position, IPetSurfaces surfaces, bool canArrive = true)
        {
            if (Intent == PetIntent.Inspecting || Intent == PetIntent.Settling)
            {
                bool inspecting = Intent == PetIntent.Inspecting;
                if (--interestTicks <= 0)
                {
                    destination = null;
                    if (inspecting) { Intent = PetIntent.Settling; interestTicks = random.Next(60, 101); }
                    else Rest();
                    RunSpeed = Mathf.Lerp(RunSpeed, 0, .08f);
                    return null;
                }
                if (inspecting && interest.HasValue)
                {
                    if (--inspectionStepTicks <= 0)
                    {
                        // Explore a small, surface-valid neighbourhood of the click.
                        // Live cursor motion changes attention, not this body's goal.
                        destination = surfaces.Advance(interest.Value, random.Next(24, 41) * (random.Next(2) == 0 ? -1 : 1));
                        inspectionStepTicks = random.Next(35, 56);
                    }
                    if (destination.HasValue && Vector2.Distance(surfaces.Nearest(position, out _), destination.Value) < 8) destination = null;
                }
                RunSpeed = Mathf.Lerp(RunSpeed, inspecting && destination.HasValue ? .18f : 0, .08f);
                return inspecting ? destination : null;
            }
            if (roamTicks > 0 && --roamTicks == 0 && !explicitTarget) Rest();
            if (!Roam && !explicitTarget) destination = null;
            if (canArrive && destination.HasValue && Vector2.Distance(surfaces.Nearest(position, out _), destination.Value) < 18)
            {
                destination = null;
                if (explicitTarget)
                {
                    explicitTarget = false; inspectionStepTicks = 0; Intent = PetIntent.Inspecting; interestTicks = random.Next(80, 161);
                    RunSpeed = Mathf.Lerp(RunSpeed, 0, .08f); return null;
                }
                if (roamTicks == 0) Rest();
            }
            if (restTicks > 0) restTicks--;
            if (!destination.HasValue && Roam && restTicks == 0)
            {
                if (roamTicks == 0) roamTicks = random.Next(RoamMinTicks, RoamMaxTicks);
                destination = surfaces.Advance(position, random.Next(80, 201) * (random.Next(2) == 0 ? -1 : 1));
            }
            Intent = !destination.HasValue ? PetIntent.Idle : explicitTarget ? PetIntent.Investigating : PetIntent.Roaming;
            // FriendTracker.RunSpeed uses distance and a stopping range, not prey urgency.
            // Desktop units/ranges are scaled for this smaller movement arena.
            float approach = destination.HasValue ? Mathf.InverseLerp(18, 160, Vector2.Distance(position, destination.Value)) : 0;
            float requested = Intent == PetIntent.Idle ? 0 : Intent == PetIntent.Investigating ? Mathf.Lerp(.12f, .35f, approach) : .25f;
            // LizardAI eases hunting speed; idle movement uses a slower destination speed.
            RunSpeed = Mathf.Lerp(RunSpeed, requested, Intent == PetIntent.Investigating ? .1f : .08f);
            return destination;
        }
    }
}
