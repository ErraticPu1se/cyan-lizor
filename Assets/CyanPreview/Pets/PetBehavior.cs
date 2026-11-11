using System;
using UnityEngine;

namespace CyanPreview
{
    public enum PetIntent { Idle, Roaming, Investigating, Inspecting, Settling, FollowingOwner, Task }

    // Rain World-inspired weighted/smoothed utilities. Personality mappings and
    // desktop needs are adaptations, not the game's complete creature AI.
    public sealed class PetBehavior
    {
        readonly System.Random random;
        readonly float energy, sympathy;
        Vector2? destination, interest, owner, lastOwner, lastPosition;
        bool arrived, ownerInvited;
        int inspectionStepTicks, stableOwnerTicks, blockedTicks;
        float curiosity, restlessness = .1f, fatigue = .15f;
        float restScore = .35f, exploreScore, cueScore, friendScore;
        public Vector2? InterestPoint => interest;
        public Vector2? TaskTarget { get; set; }
        public Vector2? ExplorationTarget => TaskTarget ?? (interest.HasValue && !arrived ? interest : null);
        public bool Roam { get; set; } = true;
        public PetIntent Intent { get; private set; }
        public float RunSpeed { get; private set; }
        public float RestUtility => restScore;
        public float ExploreUtility => exploreScore;
        public float OwnerCueUtility => cueScore;
        public float FriendUtility => friendScore;
        public float Fatigue => fatigue;
        public float Restlessness => restlessness;
        public string DecisionSummary => $"Rest {restScore:0.00} / Explore {exploreScore:0.00} / Click {cueScore:0.00} / Owner {friendScore:0.00}\nExertion {fatigue:0.00} / Restlessness {restlessness:0.00}";
        public PetBehavior(int seed, PetPersonality personality = null)
        {
            random = new System.Random(seed);
            energy = personality?.Energy ?? .5f; sympathy = personality?.Sympathy ?? .5f;
            Rest();
        }
        public void Rest()
        {
            destination = interest = null; arrived = false; curiosity = cueScore = 0;
            blockedTicks = inspectionStepTicks = 0; Intent = PetIntent.Idle;
        }
        public void ObserveOwner(Vector2? point) { owner = point; }
        public void Target(Vector2 point)
        {
            if (float.IsNaN(point.x) || float.IsNaN(point.y) || float.IsInfinity(point.x) || float.IsInfinity(point.y)) throw new ArgumentOutOfRangeException(nameof(point));
            destination = interest = point; arrived = false; ownerInvited = true;
            curiosity = 1.2f; cueScore = curiosity * Mathf.Lerp(.9f, 1.2f, sympathy);
            blockedTicks = inspectionStepTicks = 0; Intent = PetIntent.Investigating;
        }
        public Vector2? Update(Vector2 position, IPetSurfaces surfaces, bool canArrive = true)
        {
            if(TaskTarget.HasValue)
            {
                Intent=PetIntent.Task; RunSpeed=Mathf.Lerp(RunSpeed,.35f,.08f);
                lastPosition=position; return TaskTarget;
            }
            float travel = lastPosition.HasValue ? Mathf.Min(4, Vector2.Distance(position, lastPosition.Value)) : 0;
            lastPosition = position;
            bool working = destination.HasValue || !canArrive;
            fatigue = Mathf.Clamp01(fatigue + (working ? .00045f + travel * .00065f : -.0012f));
            restlessness = Mathf.Clamp01(restlessness + (working ? -.0007f - travel * .0002f : Mathf.Lerp(.0006f, .0014f, energy)));
            if (owner.HasValue && lastOwner.HasValue && Vector2.Distance(owner.Value, lastOwner.Value) < 2) stableOwnerTicks++;
            else stableOwnerTicks = 0;
            lastOwner = owner;
            if (interest.HasValue)
            {
                if (!arrived && canArrive && Vector2.Distance(surfaces.Nearest(position, out _), interest.Value) < 18)
                { arrived = true; destination = null; inspectionStepTicks = 0; Intent = PetIntent.Inspecting; }
                blockedTicks = !arrived && canArrive && travel < .05f ? blockedTicks + 1 : 0;
                // Interest declines with inspection, and unattended unreachable cues expire.
                curiosity = Mathf.Max(0, curiosity - (arrived ? .006f : .00015f));
                if (blockedTicks > 240) curiosity = Mathf.Max(0, curiosity - .02f);
            }
            restScore = Mathf.Lerp(restScore, .25f + fatigue * .65f, .08f);
            exploreScore = Roam ? Mathf.Lerp(exploreScore, restlessness * Mathf.Lerp(.65f, 1, energy) * (1 - fatigue * .6f), .08f) : 0;
            cueScore = interest.HasValue ? Mathf.Lerp(cueScore, curiosity * Mathf.Lerp(.9f, 1.2f, sympathy), .08f) : 0;
            float friendWant = 0;
            if (Roam && ownerInvited && owner.HasValue && stableOwnerTicks > 80 && !interest.HasValue)
                friendWant = Mathf.InverseLerp(130, 400, Vector2.Distance(position, surfaces.Nearest(owner.Value, out _))) * .55f * (1 - fatigue);
            friendScore = Roam && stableOwnerTicks > 80 ? Mathf.Lerp(friendScore, friendWant, .08f) : 0;
            // Continuation hysteresis prevents near-equal choices flickering.
            float rest = restScore * (Intent == PetIntent.Idle || Intent == PetIntent.Settling ? 1.2f : 1);
            float explore = exploreScore * (Intent == PetIntent.Roaming ? 1.2f : 1);
            float cue = cueScore * (interest.HasValue ? 1.2f : 1);
            float friend = friendScore * (Intent == PetIntent.FollowingOwner ? 1.2f : 1);
            if (Intent == PetIntent.Settling)
            {
                RunSpeed = Mathf.Lerp(RunSpeed, 0, .08f);
                if (RunSpeed < .015f) { interest = null; Intent = PetIntent.Idle; }
                return null;
            }
            if (interest.HasValue && cue >= Mathf.Max(rest, explore, friend))
            {
                Intent = arrived ? PetIntent.Inspecting : PetIntent.Investigating;
                if (!arrived) destination = interest;
                else
                {
                    if (--inspectionStepTicks <= 0)
                    {
                        destination = surfaces.Advance(interest.Value, random.Next(24, 41) * (random.Next(2) == 0 ? -1 : 1));
                        inspectionStepTicks = random.Next(35, 56);
                    }
                    if (destination.HasValue && Vector2.Distance(surfaces.Nearest(position, out _), destination.Value) < 8) destination = null;
                }
            }
            else if (interest.HasValue)
            {
                destination = null; arrived = true; curiosity = cueScore = 0;
                Intent = PetIntent.Settling;
            }
            else if (friend > Mathf.Max(rest, explore) && owner.HasValue)
            {
                Intent = PetIntent.FollowingOwner;
                destination = surfaces.Nearest(owner.Value, out _);
            }
            else if (explore > rest)
            {
                if (Intent != PetIntent.Roaming) destination = null;
                Intent = PetIntent.Roaming;
                if (!destination.HasValue || Vector2.Distance(surfaces.Nearest(position, out _), destination.Value) < 18)
                {
                    destination = surfaces.Advance(position, random.Next(80, 201) * (random.Next(2) == 0 ? -1 : 1));
                    restlessness = Mathf.Max(0, restlessness - .025f);
                }
            }
            else { destination = null; Intent = PetIntent.Idle; }
            float approach = destination.HasValue ? Mathf.InverseLerp(18, 160, Vector2.Distance(position, destination.Value)) : 0;
            float requested = !destination.HasValue ? 0 : Intent == PetIntent.Inspecting ? .18f
                : Intent == PetIntent.Roaming ? .25f : Mathf.Lerp(.12f, .35f, approach);
            RunSpeed = Mathf.Lerp(RunSpeed, requested, .08f);
            return destination;
        }
    }
}
