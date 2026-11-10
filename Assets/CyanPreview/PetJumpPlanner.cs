using UnityEngine;

namespace CyanPreview
{
    public struct JumpCandidate
    {
        public Vector2 Velocity, Landing;
        public float Power;
    }

    // Samples a trajectory against host surfaces. This is the desktop equivalent
    // of JumpFinder candidate evaluation, not the game's tile/pathfinder search.
    public static class PetJumpPlanner
    {
        public static bool TryPlan(Vector2 start, IPetSurfaces surfaces, MovementCapabilities species,
            System.Random random, out JumpCandidate result)
        {
            result = default;
            if (species.LaunchMax <= 0) return false;
            surfaces.Nearest(start, out var inward);
            float best = 30;
            bool found = false;
            for (int sample = 0; sample < 20; sample++)
            {
                float power = (float)random.NextDouble() * .45f;
                float angle = (float)random.NextDouble() * Mathf.PI * 2;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                if (inward != Vector2.zero && Vector2.Dot(direction, inward) < .25f) continue;
                var initial = direction * Mathf.Lerp(species.LaunchMin, species.LaunchMax, power);
                var p = start;
                var velocity = initial;
                for (int tick = 0; tick < 100; tick++)
                {
                    var next = p + velocity;
                    var clamped = surfaces.Constrain(next, 8, out var contact);
                    if (contact != Vector2.zero)
                    {
                        float separation = Vector2.Distance(start, clamped);
                        if (tick >= 5 && separation > best && (species.ClimbWalls || contact.y > .5f))
                        {
                            best = separation; found = true;
                            result = new JumpCandidate { Velocity = initial, Landing = clamped, Power = power };
                        }
                        break;
                    }
                    p = next; velocity = velocity * .999f + Vector2.down * .9f;
                }
            }
            return found;
        }
    }
}
