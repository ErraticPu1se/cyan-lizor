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
        // Solve candidate launch velocities, then run the same discrete gravity/
        // drag steps used by the host. Reject paths that hit a different surface.
        public static bool TryPlanToTarget(Vector2 start, Vector2 target, IPetSurfaces surfaces,
            MovementCapabilities species, out JumpCandidate result)
        {
            result = default;
            if (species.LaunchMax <= 0 || !species.ClimbWalls) return false;
            var landing = surfaces.Nearest(target, out var normal) + normal * 8;
            for (int duration = 8; duration <= 60; duration += 2)
            {
                float dragSum = (1 - Mathf.Pow(.999f, duration)) / .001f;
                float gravitySum = .9f * (duration - dragSum) / .001f;
                var initial = (landing - start + Vector2.up * gravitySum) / dragSum;
                float speed = initial.magnitude;
                if (speed < species.LaunchMin || speed > species.LaunchMax) continue;
                var position = start; var velocity = initial;
                for (int tick = 1; tick <= duration + 4; tick++)
                {
                    var next = position + velocity;
                    var bounded = surfaces.Constrain(next, 8, out var hit);
                    bool arrived = tick >= duration - 1 && Vector2.Distance(next, landing) < 22
                        && (surfaces.SupportsFreeClimbing || Vector2.Distance(next, surfaces.Nearest(next, out _)) <= 18);
                    if (hit != Vector2.zero || arrived)
                    {
                        if (tick >= 5 && Vector2.Distance(bounded, landing) < 30)
                        {
                            result = new JumpCandidate { Velocity = initial, Landing = landing,
                                Power = Mathf.InverseLerp(species.LaunchMin, species.LaunchMax, speed) };
                            return true;
                        }
                        break;
                    }
                    position = next; velocity = velocity * .999f + Vector2.down * .9f;
                }
            }
            return false;
        }

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
