using System;
using UnityEngine;

namespace CyanPreview
{
    public enum PetIntent { Idle, Roaming, Pursuing }

    // Desktop counterpart to LizardAI's idle destination / prey destination choice.
    // No game prey, threat, hunger or relationship tracker is fabricated here.
    public sealed class PetBehavior
    {
        readonly System.Random random;
        Vector2? destination;
        bool explicitTarget;
        int restTicks, roamTicks;
        public const int IdleMinTicks = 320, IdleMaxTicks = 601;
        public const int RoamMinTicks = 600, RoamMaxTicks = 1001;
        public bool Roam { get; set; } = true;
        public PetIntent Intent { get; private set; }
        public float RunSpeed { get; private set; }
        public PetBehavior(int seed) { random = new System.Random(seed); Rest(); }
        public void Rest()
        {
            destination = null; explicitTarget = false;
            restTicks = random.Next(IdleMinTicks, IdleMaxTicks); roamTicks = 0; Intent = PetIntent.Idle;
        }
        public Vector2? ExplorationTarget => explicitTarget ? destination : null;
        public void Target(Vector2 point)
        {
            destination = point; explicitTarget = true; restTicks = roamTicks = 0;
            Intent = PetIntent.Pursuing;
        }
        public Vector2? Update(Vector2 position, IPetSurfaces surfaces)
        {
            if (roamTicks > 0 && --roamTicks == 0 && !explicitTarget) Rest();
            if (!Roam && !explicitTarget) destination = null;
            if (destination.HasValue && Vector2.Distance(surfaces.Nearest(position, out _), destination.Value) < 18)
            {
                destination = null;
                if (explicitTarget || roamTicks == 0) Rest();
            }
            if (restTicks > 0) restTicks--;
            if (!destination.HasValue && Roam && restTicks == 0)
            {
                if (roamTicks == 0) roamTicks = random.Next(RoamMinTicks, RoamMaxTicks);
                destination = surfaces.Advance(position, random.Next(80, 201) * (random.Next(2) == 0 ? -1 : 1));
            }
            Intent = !destination.HasValue ? PetIntent.Idle : explicitTarget ? PetIntent.Pursuing : PetIntent.Roaming;
            float requested = Intent == PetIntent.Idle ? 0 : Intent == PetIntent.Pursuing ? .35f : .25f;
            // LizardAI eases hunting speed; idle movement uses a slower destination speed.
            RunSpeed = Mathf.Lerp(RunSpeed, requested, Intent == PetIntent.Pursuing ? .1f : .5f);
            return destination;
        }
    }
}
