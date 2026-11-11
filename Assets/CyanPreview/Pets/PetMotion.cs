using System;
using System.Collections;
using UnityEngine;
using static CyanPreview.ReferenceContext;

namespace CyanPreview
{
    public enum PetMotionState { Crawling, Charging, Airborne, Grabbed }

    // One controller per pet. Rain World integration is confined to this adapter.
    // Room AI is replaced by host surface selection; original constraints and Limb
    // reach interpolation are executed against the locally installed game assembly.
    public sealed partial class PetMotion
    {
        const float TickSeconds = 1f / 40f;
        readonly ReferenceBridge pet;
        readonly IPetSurfaces surfaces;
        readonly object[] feet = new object[4];
        readonly Vector2[] anchors = new Vector2[4];
        readonly bool[] planted = new bool[4];
        readonly bool[] seeking = new bool[4];
        readonly int[] gripAge = new int[4];
        readonly object[] chunks = new object[3];
        readonly Array connections;
        readonly object graphics;
        readonly MovementCapabilities capabilities;
        readonly System.Random jumpRandom;
        int jumpWait = 320, poseTicks;
        readonly Vector2[] restingPose = new Vector2[3];
        bool resting;
        Vector2 grabOffset, grabAnchor, grabVelocity;
        int grabbedChunk, heldTicks;
        float turnDirection = 1;
        bool propelled;
        float jumpPower;
        public event Action<Vector2, Vector2, float> JumpLaunched;
        public event Action<float> Landed;
        public event Action<Vector2, Vector2, float> JumpSmoke;
        public bool AutonomousJumps { get; set; }
        float accumulator;
        int chargeTicks, chargeDuration, cooldown, flightTicks;
        public float JumpPower => jumpPower;
        public float SmokeLifeTicks => Mathf.Max(1, (70 * jumpPower - flightTicks) * 1.5f);
        public Vector2 HeadPosition => pet.HeadPosition;
        // Rain World CarryObject places held objects ahead of the front body chunk.
        public Vector2 MouthPosition => Position+(Position-(Vector2)Get(chunks[1],"pos")).normalized*(25*.95f);
        public float VoiceIntensity { get; set; }
        Vector2 destination, launchVelocity, jumpTarget;
        Vector2? lookTarget;
        bool hasDestination, clickInvestigation;
        public PetBehavior Behavior { get; }
        public LandingRecovery Recovery { get; }
        public FallCatchProgress FallCatch { get; }
        public bool Roam { get => Behavior.Roam; set => Behavior.Roam = value; }
        public PetMotionState State { get; private set; }
        public float Interpolation => Mathf.Clamp01(accumulator / TickSeconds);
        public int GripCount { get { int n = 0; foreach (bool p in planted) if (p) n++; return n; } }
        public Vector2 Position => (Vector2)Get(chunks[0], "pos");
        public event Action<PetMotionState> StateChanged;

        public PetMotion(ReferenceBridge pet, IPetSurfaces surfaces, int? behaviorSeed = null)
        {
            this.pet = pet ?? throw new ArgumentNullException(nameof(pet));
            this.surfaces = surfaces ?? throw new ArgumentNullException(nameof(surfaces));
            graphics = pet.Graphics;
            int seed = behaviorSeed ?? pet.PetId.GetHashCode();
            Behavior = new PetBehavior(seed, pet.Personality);
            jumpRandom = new System.Random(seed ^ 21761);
            capabilities = pet.Species.Movement;
            Recovery = new LandingRecovery(pet.Species.Landing);
            var body = (Array)Get(pet.Lizard, "bodyChunks");
            for (int i = 0; i < 3; i++) chunks[i] = body.GetValue(i);
            connections = (Array)Get(pet.Lizard, "bodyChunkConnections");
            var origin = surfaces.Nearest(Vector2.zero, out var normal) + normal * 14;
            var tangent = normal == Vector2.zero ? Vector2.right : new Vector2(normal.y, -normal.x);
            for (int i = 0; i < 3; i++)
            {
                Set(chunks[i], "pos", origin - tangent * i * 17);
                Set(chunks[i], "lastPos", Get(chunks[i], "pos"));
                Set(chunks[i], "vel", Vector2.zero);
            }
            float bodyLength = Vector2.Distance((Vector2)Get(chunks[0], "pos"),(Vector2)Get(chunks[1], "pos"))
                + Vector2.Distance((Vector2)Get(chunks[1], "pos"),(Vector2)Get(chunks[2], "pos"))
                + (float)Get(chunks[0], "rad") + (float)Get(chunks[2], "rad");
            FallCatch = new FallCatchProgress(bodyLength);
            for (int i = 0; i < 4; i++)
            {
                feet[i] = CreateFoot(i);
                Set(feet[i], "pushOutOfTerrain", false);
                Set(feet[i], "mode", pet.Context.Static("Limb+Mode", "HuntAbsolutePosition"));
            }
            // Initialize springs once; subsequent frames retain their momentum.
            var tail = (Array)Get(graphics, "tail");
            for (int i = 0; i < tail.Length; i++)
            {
                var p = origin - tangent * (34 + (i + 1) * 8);
                Set(tail.GetValue(i), "pos", p); Set(tail.GetValue(i), "lastPos", p); Set(tail.GetValue(i), "vel", Vector2.zero);
            }
            UpdateGraphics();
        }
        object CreateFoot(int index)
        {
            var state = UnityEngine.Random.state;
            try { return pet.Context.New("Limb", graphics, chunks[index < 2 ? 0 : 2], index, 2.5f, .7f, .99f, 6f, .6f); }
            finally { UnityEngine.Random.state = state; }
        }
        public void LookAt(Vector2? point) { lookTarget = point; }
        public LizardSpecies Species => pet.Species;
        public void ObserveCursor(Vector2? point)
        {
            // Nearby pointer attention changes the pose without creating a movement goal.
            LookAt(point.HasValue && (Vector2.Distance(HeadPosition, point.Value) <= 180 || poseTicks % 200 < 20) ? point : null);
        }
        public void MoveTo(Vector2 point) { clickInvestigation = false; Behavior.Target(surfaces.Nearest(point, out _)); }
        public void ExploreClick(Vector2 point)
        {
            if (float.IsNaN(point.x) || float.IsNaN(point.y) || float.IsInfinity(point.x) || float.IsInfinity(point.y))
                throw new ArgumentOutOfRangeException(nameof(point));
            // World-space reach scales with the rendered pet, not the monitor resolution.
            const float nearbyReach = 150; // Roughly three cyan body lengths, excluding the tail.
            var origin = surfaces.Nearest(Position, out _);
            var target = surfaces.Nearest(point, out _);
            float distance = Vector2.Distance(origin, target);
            if (distance > nearbyReach) target = surfaces.Nearest(Vector2.MoveTowards(origin, target, nearbyReach + (distance - nearbyReach) * .65f), out _);
            Behavior.Target(target);
            clickInvestigation = true;
        }
        public bool Contains(Vector2 point)
        {
            foreach (var chunk in chunks)
                if (Vector2.Distance(point, (Vector2)Get(chunk, "pos")) <= 14) return true;
            foreach (var tail in (Array)Get(graphics, "tail"))
                if (Vector2.Distance(point, (Vector2)Get(tail, "pos")) <= 7) return true;
            return Vector2.Distance(point, pet.HeadPosition) <= 12;
        }
        public void BeginGrab(Vector2 point)
        {
            grabbedChunk = 0;
            for (int i = 1; i < chunks.Length; i++)
                if (Vector2.Distance(point, (Vector2)Get(chunks[i], "pos")) < Vector2.Distance(point, (Vector2)Get(chunks[grabbedChunk], "pos"))) grabbedChunk = i;
            grabAnchor = (Vector2)Get(chunks[grabbedChunk], "pos");
            grabOffset = grabAnchor - point; grabVelocity = Vector2.zero; heldTicks = 0; propelled = false;
            Behavior.Rest(); ChangeState(PetMotionState.Grabbed);
            for (int i = 0; i < 4; i++) { planted[i] = seeking[i] = false; gripAge[i] = 0; }
        }
        public void DragTo(Vector2 point)
        {
            if (State != PetMotionState.Grabbed) return;
            // Cursor updates set a target; physics owns body and tail momentum.
            if (float.IsNaN(point.x) || float.IsNaN(point.y) || float.IsInfinity(point.x) || float.IsInfinity(point.y))
                throw new ArgumentOutOfRangeException(nameof(point));
            grabAnchor = surfaces.Constrain(point + grabOffset, 8, out _);
        }
        public void ShiftWithSurface(Vector2 delta, bool resetVelocity = true)
        {
            if (float.IsNaN(delta.x) || float.IsNaN(delta.y) || float.IsInfinity(delta.x) || float.IsInfinity(delta.y)) throw new ArgumentOutOfRangeException(nameof(delta));
            Recovery.Shift(delta);
            for (int i = 0; i < restingPose.Length; i++) restingPose[i] += delta;
            foreach (var chunk in chunks) { TranslatePart(chunk, delta); if (resetVelocity) Set(chunk, "vel", Vector2.zero); }
            // Carry the complete pose, including interpolation history. Feet must
            // not chase an externally moved body at their normal crawling speed.
            foreach (var part in (Array)Get(graphics, "bodyParts")) TranslatePart(part, delta);
            foreach (var foot in feet)
            {
                TranslatePart(foot, delta);
                Set(foot, "absoluteHuntPos", (Vector2)Get(foot, "absoluteHuntPos") + delta);
            }
            for (int i = 0; i < anchors.Length; i++) anchors[i] += delta;
            var draw = (Vector2[,])Get(graphics, "drawPositions");
            for (int i = 0; i < 3; i++) for (int history = 0; history < 2; history++) draw[i, history] += delta;
            var tail = (Array)Get(graphics, "tail");
            Set(tail.GetValue(0), "connectedPoint", (Vector2?)Get(chunks[2], "pos"));
            foreach (var cosmetic in (IEnumerable)Get(graphics, "cosmetics"))
                if (pet.Context.Type("LizardCosmetics.LongBodyScales").IsInstanceOfType(cosmetic))
                    foreach (var scale in (Array)Get(cosmetic, "scaleObjects")) TranslatePart(scale, delta);
            // Input can arrive between physics ticks; keep dragged chunks inside
            // the arena immediately, before the joint solver gets its next step.
            foreach (var chunk in chunks)
            {
                var position = (Vector2)Get(chunk, "pos");
                TranslatePart(chunk, surfaces.Constrain(position, 8, out _) - position);
            }
        }
        static void TranslatePart(object part, Vector2 delta)
        {
            Set(part, "pos", (Vector2)Get(part, "pos") + delta);
            Set(part, "lastPos", (Vector2)Get(part, "lastPos") + delta);
        }
        public void EndGrab()
        {
            if (State != PetMotionState.Grabbed) return;
            Set(chunks[grabbedChunk], "vel", grabVelocity);
            foreach (var chunk in chunks) Set(chunk, "vel", Vector2.ClampMagnitude((Vector2)Get(chunk, "vel"), 18));
            cooldown = 0; flightTicks = 0; Behavior.Rest();
            ChangeState(PetMotionState.Airborne);
        }
        public void ReleaseSupport()
        {
            if (State != PetMotionState.Crawling && State != PetMotionState.Charging) return;
            for (int i = 0; i < planted.Length; i++) planted[i] = seeking[i] = false;
            flightTicks = 0; propelled = false; Behavior.Rest(); ChangeState(PetMotionState.Airborne);
        }
        public void RefreshSurfaceSupport()
        {
            Recovery.Cancel();
            for (int i = 0; i < planted.Length; i++) { planted[i] = seeking[i] = false; gripAge[i] = 0; }
            if (Vector2.Distance(Position, surfaces.Nearest(Position, out var normal)) > 30 || (!capabilities.ClimbWalls && normal.y < .5f)) ReleaseSupport();
            else if (State == PetMotionState.Charging) { ChangeState(PetMotionState.Crawling); Behavior.Rest(); }
        }
        public bool JumpTo(Vector2 point, float power = .25f)
        {
            if (State != PetMotionState.Crawling || Recovery.Active || cooldown > 0 || GripCount == 0 || capabilities.LaunchMax <= 0) return false;
            var direction = point - Position;
            if (direction.sqrMagnitude < 1) return false;
            surfaces.Nearest(Position, out var normal);
            // Avoid firing directly into the supporting surface.
            direction = (direction.normalized + normal * .35f).normalized;
            if (Vector2.Dot(direction, normal) < .15f) direction = (direction + normal).normalized;
            launchVelocity = direction * Mathf.Lerp(capabilities.LaunchMin, capabilities.LaunchMax, Mathf.Clamp01(power));
            BeginJump(point, power);
            return true;
        }
        public void BeginIdleHold()
        {
            Behavior.TaskTarget=null; Behavior.Rest(); Behavior.HoldPosition=true;
            if(State==PetMotionState.Charging) ChangeState(PetMotionState.Crawling);
        }
        // Explicit host task; ordinary roam/click jump preferences remain unchanged.
        public bool TryTaskJump(Vector2 point)
        {
            if(State!=PetMotionState.Crawling || Recovery.Active || cooldown>0 || GripCount==0) return false;
            if(!PetJumpPlanner.TryPlanToTarget(Position,point,surfaces,capabilities,out var candidate)) return false;
            launchVelocity=candidate.Velocity; BeginJump(candidate.Landing,candidate.Power); return true;
        }
        void BeginJump(Vector2 point, float power)
        {
            jumpPower = Mathf.Clamp01(power);
            jumpTarget = point;
            propelled = true;
            flightTicks = 0;
            chargeTicks = chargeDuration = power > .75f ? 45 : power > .25f ? 30 : 15;
            ChangeState(PetMotionState.Charging);
        }
        void ChangeState(PetMotionState state)
        {
            FallCatch.Reset(((Vector2)Get(chunks[1], "pos")).y);
            if (state != PetMotionState.Crawling) Recovery.Cancel();
            State = state; StateChanged?.Invoke(state);
        }
        public void Advance(float seconds)
        {
            if (pet.Graphics != graphics) throw new InvalidOperationException("Create a new movement controller after generating a pet.");
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            accumulator += Mathf.Min(seconds, .25f);
            while (accumulator + .000001f >= TickSeconds) { accumulator -= TickSeconds; Step(); }
        }
        void Step()
        {
            if (State == PetMotionState.Grabbed)
            {
                heldTicks++;
                var dragDelta = grabAnchor - (Vector2)Get(chunks[grabbedChunk], "pos");
                // Carry only excess displacement on extreme cursor warps to bound joint stretch.
                var impulse = Vector2.ClampMagnitude(dragDelta, 24);
                grabVelocity = impulse;
                if (dragDelta.magnitude > 24) ShiftWithSurface(dragDelta - impulse, false);
                Set(chunks[grabbedChunk], "vel", impulse);
                for (int i = 0; i < chunks.Length; i++)
                {
                    var p = (Vector2)Get(chunks[i], "pos");
                    Set(chunks[i], "lastPos", p);
                    // Only the held point is fixed. Gravity and small muscular
                    // impulses act on the rest through the existing body joints.
                    var struggle = new Vector2(Mathf.Sin(heldTicks * .23f + i * 1.7f) * .35f,
                        Mathf.Cos(heldTicks * .17f + i) * .15f);
                    var velocity = (Vector2)Get(chunks[i], "vel") * .94f + Vector2.down * .9f + struggle;
                    Set(chunks[i], "pos", i == grabbedChunk ? grabAnchor : surfaces.Constrain(p + velocity, 8, out _));
                    Set(chunks[i], "vel", i == grabbedChunk ? Vector2.zero : velocity);
                }
                for (int pass = 0; pass < 6; pass++)
                {
                    foreach (var connection in connections) Call(connection, "Update");
                    foreach (var chunk in chunks)
                    {
                        Set(chunk, "pos", surfaces.Constrain((Vector2)Get(chunk, "pos"), 8, out var hit));
                        var velocity = (Vector2)Get(chunk, "vel");
                        Set(chunk, "vel", velocity - hit * Mathf.Min(0, Vector2.Dot(velocity, hit)));
                    }
                    Set(chunks[grabbedChunk], "pos", grabAnchor); Set(chunks[grabbedChunk], "vel", Vector2.zero);
                }
                for (int i = 0; i < 4; i++)
                {
                    var shoulder = (Vector2)Get(chunks[i < 2 ? 0 : 2], "pos");
                    Set(feet[i], "absoluteHuntPos", shoulder + new Vector2(Mathf.Sin(heldTicks * .28f + i * 2) * 12, -8 + Mathf.Cos(heldTicks * .28f + i * 2) * 9));
                    Call(feet[i], "Update");
                    float reach = (float)Get(((Array)Get(graphics, "limbs")).GetValue(i), "jointDist");
                    Call(feet[i], "ConnectToPoint", shoulder, reach, false, 0f, Vector2.zero, 0f, 0f);
                }
                UpdateGraphics(); return;
            }
            float bodySpeed = 0;
            foreach (var chunk in chunks) bodySpeed = Mathf.Max(bodySpeed, ((Vector2)Get(chunk, "vel")).magnitude);
            Recovery.Advance(TickSeconds, GripCount, bodySpeed);
            if (cooldown > 0) cooldown--;
            var exploration = Behavior.ExplorationTarget;
            if ((Behavior.TaskTarget.HasValue ? AutonomousJumps : (!clickInvestigation || AutonomousJumps)) && State == PetMotionState.Crawling && !Recovery.Active && cooldown == 0 && GripCount > 0 && exploration.HasValue
                && Vector2.Distance(Position, exploration.Value) > (clickInvestigation ? 35 : Mathf.Lerp(190, 120, pet.Personality.Bravery))
                && PetJumpPlanner.TryPlanToTarget(Position, exploration.Value, surfaces, capabilities, out var targetedJump))
            {
                launchVelocity = targetedJump.Velocity;
                BeginJump(targetedJump.Landing, targetedJump.Power);
            }
            if (!Behavior.HoldPosition && AutonomousJumps && Roam && Behavior.Intent == PetIntent.Roaming && State == PetMotionState.Crawling && !Recovery.Active && --jumpWait <= 0 && GripCount > 0)
            {
                jumpWait = jumpRandom.Next(240, 481) + Mathf.RoundToInt((1 - pet.Personality.Bravery) * 160);
                if (PetJumpPlanner.TryPlan(Position, surfaces, capabilities, jumpRandom, out var candidate))
                {
                    launchVelocity = candidate.Velocity;
                    BeginJump(candidate.Landing, candidate.Power);
                }
            }
            if (State == PetMotionState.Charging && --chargeTicks == 0)
            {
                foreach (var chunk in chunks) Set(chunk, "vel", launchVelocity);
                for (int i = 0; i < 4; i++) { planted[i] = seeking[i] = false; gripAge[i] = 0; }
                cooldown = 20;
                ChangeState(PetMotionState.Airborne);
                JumpLaunched?.Invoke((Vector2)Get(chunks[1], "pos"), launchVelocity, jumpPower);
            }
            var goal = Behavior.Update(Position, surfaces, State == PetMotionState.Crawling);
            hasDestination = goal.HasValue;
            if (goal.HasValue) destination = goal.Value;
            Set(Get(pet.Lizard, "AI"), "runSpeed", Behavior.RunSpeed);
            bool airborne = State == PetMotionState.Airborne;
            poseTicks++;
            bool atRest = State == PetMotionState.Crawling && !hasDestination && !Recovery.Active;
            if (atRest && !resting) for (int i = 0; i < 3; i++) restingPose[i] = (Vector2)Get(chunks[i], "pos");
            resting = atRest;
            if (airborne)
            {
                flightTicks++;
                if (propelled && flightTicks < 70 * jumpPower)
                    JumpSmoke?.Invoke((Vector2)Get(chunks[1], "pos"), (Vector2)Get(chunks[1], "vel") - (Position - (Vector2)Get(chunks[2], "pos")).normalized * 16, jumpPower);
            }
            var edge = surfaces.Nearest(Position, out var normal);
            var ahead = hasDestination ? surfaces.Approach(edge, destination, 22) : surfaces.Advance(edge, 22);
            var tangent = (ahead - edge).normalized;
            if (!capabilities.ClimbWalls && normal.y < .5f) tangent = Vector2.down;
            bool landed = false;
            float impactSpeed = 0;
            Vector2 impactNormal = Vector2.zero, impactPoint = Position;
            for (int i = 0; i < 3; i++)
            {
                var p = (Vector2)Get(chunks[i], "pos");
                var v = (Vector2)Get(chunks[i], "vel");
                Set(chunks[i], "lastPos", p);
                if (airborne)
                {
                    // Cyan JumpingUpdate applies opposite front/rear forces toward
                    // the landing target, allowing the body to turn during flight.
                    if (propelled && i != 1) v += (jumpTarget - (Vector2)Get(chunks[1], "pos")).normalized
                        * Mathf.Lerp(2, 0, Mathf.InverseLerp(10, 60, flightTicks)) * (i == 0 ? 1 : -1);
                    p += v; v = v * .999f + Vector2.down * .9f;
                }
                else
                {
                    var support = surfaces.Nearest(p, out var inward);
                    float clearance = State == PetMotionState.Charging ? 9 : 14 - Recovery.Compression * (i == 1 ? 1 : .6f);
                    if (Recovery.Active && Recovery.Normal != Vector2.zero)
                    {
                        inward = Recovery.Normal;
                        support = p - inward * Vector2.Dot(p - Recovery.ContactPoint, inward);
                    }
                    var supportForce = Vector2.ClampMagnitude(support + inward * clearance - p, 6) * .18f;
                    v *= Recovery.VelocityRetention;
                    v += supportForce;
                    if (State == PetMotionState.Crawling && hasDestination)
                    {
                        var beforeDrive = v;
                        // Head leads, the middle follows its route, and the rear is pulled
                        // by the original chunk connections instead of a rigid pose.
                        var forward = (Position - (Vector2)Get(chunks[1], "pos")).normalized;
                        float facing = Vector2.Dot(forward, tangent);
                        surfaces.Nearest(ahead, out var targetNormal);
                        var target = ahead + targetNormal * clearance;
                        if (facing < .95f)
                        {
                            float angle = Vector2.SignedAngle(forward, tangent);
                            if (Mathf.Abs(angle) > 170)
                            {
                                if (normal != Vector2.zero) turnDirection = Vector2.SignedAngle(forward, normal) >= 0 ? 1 : -1;
                                angle = turnDirection * 180;
                            }
                            else turnDirection = Mathf.Sign(angle);
                            var bending = (Vector2)(Quaternion.Euler(0, 0, Mathf.Clamp(angle, -18, 18)) * forward);
                            var middle = (Vector2)Get(chunks[1], "pos");
                            if (i == 0) v += (middle + bending * 17 - p) * .65f;
                            if (i == 2) v += (middle - bending * 17 - p) * .35f;
                        }
                        float alignment = 1;
                        if (i == 1)
                        {
                            var leadTarget = target;
                            target += (Position - target).normalized * 17;
                            if (Behavior.Intent == PetIntent.Investigating)
                                alignment = Mathf.Lerp(.5f, 1, (Vector2.Dot((leadTarget - p).normalized, (target - p).normalized) + 1) * .5f);
                        }
                        if (i < 2) v += (target - p).normalized * capabilities.CrawlSpeed * (Behavior.RunSpeed / .5f) * (i == 0 ? .55f : .35f) * alignment * Mathf.InverseLerp(.3f, .9f, facing);
                        v = beforeDrive + (v - beforeDrive) * Recovery.DriveScale;
                    }
                    if (resting)
                    {
                        // Desktop resting pose: bounded weight shifts around the
                        // arrival pose, rather than continuously driving the body.
                        float amplitude = Behavior.Intent == PetIntent.Inspecting ? 1.4f : Behavior.Intent == PetIntent.Settling ? .8f : .45f;
                        var offset = new Vector2(Mathf.Sin(poseTicks * .035f + i), Mathf.Sin(poseTicks * .06f + i * .7f)) * amplitude;
                        var restTarget = restingPose[i];
                        if (lookTarget.HasValue && Vector2.Distance(HeadPosition, lookTarget.Value) <= 180)
                        {
                            var forward = restingPose[0] - restingPose[1];
                            float turn = Mathf.Clamp(Vector2.SignedAngle(forward, lookTarget.Value - restingPose[1]), -25, 25);
                            restTarget = restingPose[1] + (Vector2)(Quaternion.Euler(0, 0, turn) * (restTarget - restingPose[1]));
                        }
                        v += Vector2.ClampMagnitude(restTarget + offset - p, 12) * .12f;
                    }
                    if (State == PetMotionState.Charging)
                    {
                        if (i == 1) v *= .5f;
                        else v += launchVelocity.normalized * (i == 0 ? 1.5f : -2f);
                    }
                    p += v;
                }
                p = surfaces.Constrain(p, 8, out var hit);
                if (hit != Vector2.zero)
                {
                    float speed = -Vector2.Dot(v, hit);
                    if (speed > impactSpeed) { impactSpeed = speed; impactNormal = hit; impactPoint = p - hit * 8; }
                    v -= hit * Mathf.Min(0, Vector2.Dot(v, hit));
                    if (cooldown < 15 && (hit.y > .5f || (propelled && capabilities.ClimbWalls))) landed = true;
                }
                Set(chunks[i], "pos", p); Set(chunks[i], "vel", v);
            }
            foreach (var connection in connections) Call(connection, "Update");
            foreach (var chunk in chunks) Set(chunk, "pos", surfaces.Constrain((Vector2)Get(chunk, "pos"), 8, out _));
            if (airborne && !propelled)
                FallCatch.Advance(TickSeconds, ((Vector2)Get(chunks[1], "pos")).y, ((Vector2)Get(chunks[1], "vel")).y);
            if (airborne && propelled && capabilities.ClimbWalls && flightTicks > 5 && Vector2.Distance(Position, jumpTarget) < 25
                && (surfaces.SupportsFreeClimbing || (Vector2.Distance(jumpTarget, surfaces.Nearest(jumpTarget, out _)) <= 10
                    && Vector2.Distance(Position, surfaces.Nearest(Position, out _)) <= 18)))
                landed = true;
            if (airborne && landed)
            {
                if (impactSpeed > 1.5f) Landed?.Invoke(impactSpeed);
                ChangeState(PetMotionState.Crawling);
                // Terrain impacts use the contact plane; a background catch has no solid normal.
                float recoverySpeed = impactSpeed > 0 ? impactSpeed : ((Vector2)Get(chunks[1], "vel")).magnitude;
                Recovery.Begin(recoverySpeed, impactNormal, impactPoint);
                if (!Behavior.ExplorationTarget.HasValue) Behavior.Rest();
                foreach (var chunk in chunks) Set(chunk, "vel", (Vector2)Get(chunk, "vel") * .5f);
                for (int i = 0; i < feet.Length; i++) { planted[i] = seeking[i] = false; gripAge[i] = 0; }
            }
            for (int i = 0; i < 4; i++)
            {
                var shoulder = (Vector2)Get(chunks[i < 2 ? 0 : 2], "pos");
                bool flying = State == PetMotionState.Airborne;
                float reach = (float)Get(((Array)Get(graphics, "limbs")).GetValue(i), "jointDist");
                // LizardLimb releases a grip when the shoulder passes it and the
                // limb reaches its limit; its partner's grip delays the next step.
                var localForward = i < 2 ? tangent : ((Vector2)Get(chunks[1], "pos") - shoulder).normalized;
                if (localForward.sqrMagnitude < .01f) localForward = tangent;
                float extension = Vector2.Distance(anchors[i], shoulder);
                bool trailing = Vector2.Dot(shoulder - anchors[i], localForward) > 0;
                bool release = !Recovery.Active && planted[i] && trailing && extension > reach - 2 && (gripAge[i ^ 1] > 2 || extension > reach + 3);
                if (release || flying || extension > reach + 3) { planted[i] = seeking[i] = false; gripAge[i] = 0; }
                if (!planted[i] && !seeking[i])
                {
                    var spread = normal == Vector2.zero ? new Vector2(-localForward.y, localForward.x) * (i % 2 == 0 ? 14 : -14) : Vector2.zero;
                    anchors[i] = surfaces.Nearest(shoulder + localForward * (Recovery.Active ? 0 : i % 2 == 0 ? 16 : 10) + spread, out _);
                    seeking[i] = true;
                }
                Vector2 hunt = anchors[i];
                bool reachingToCatch = false;
                Vector2 catchNormal = Vector2.zero;
                if (flying)
                {
                    hunt = shoulder - localForward * 8 + normal * (i % 2 == 0 ? 10 : -10);
                    if (!propelled && capabilities.ClimbWalls && FallCatch.Ready && surfaces is IEdgeCatchSurfaces catchSurfaces
                        && catchSurfaces.TryCatchEdge(shoulder, reach, out var catchPoint, out catchNormal))
                    { hunt = anchors[i] = catchPoint; reachingToCatch = true; }
                }
                // LizardLimb increases hunt speed by its connection velocity when reaching.
                Set(feet[i], "huntSpeed", 6f + (reachingToCatch ? ((Vector2)Get(chunks[i < 2 ? 0 : 2], "vel")).magnitude : 0));
                Set(feet[i], "absoluteHuntPos", hunt);
                Call(feet[i], "Update");
                Call(feet[i], "ConnectToPoint", shoulder, reach, false, 0f, Get(chunks[i < 2 ? 0 : 2], "vel"), 0f, 0f);
                if (reachingToCatch && Vector2.Distance((Vector2)Get(feet[i], "pos"), hunt) < 2
                    && Vector2.Distance(shoulder, hunt) <= reach)
                {
                    float speed = ((Vector2)Get(chunks[1], "vel")).magnitude;
                    ChangeState(PetMotionState.Crawling);
                    Recovery.Begin(speed,catchNormal,hunt);
                    foreach (var chunk in chunks) Set(chunk,"vel",(Vector2)Get(chunk,"vel") * .5f);
                    Behavior.Rest(); resting = false;
                    planted[i] = true; seeking[i] = false;
                }
                if (!flying && Vector2.Distance(shoulder, anchors[i]) <= reach && Vector2.Distance((Vector2)Get(feet[i], "pos"), anchors[i]) < 2) planted[i] = true;
                if (planted[i]) gripAge[i]++;
            }
            UpdateGraphics();
        }
        void UpdateGraphics()
        {
            Set(pet.Lizard, "animation", pet.Context.Static("Lizard+Animation", State == PetMotionState.Charging ? "PrepareToJump" : "Standard"));
            Set(pet.Lizard, "timeInAnimation", chargeDuration - chargeTicks);
            Set(pet.Lizard, "timeToRemainInAnimation", chargeDuration);
            Set(graphics, "lastVoiceVisualization", Get(graphics, "voiceVisualization"));
            Set(graphics, "lastVoiceVisualizationIntensity", Get(graphics, "voiceVisualizationIntensity"));
            Set(graphics, "voiceVisualization", Mathf.Clamp01(VoiceIntensity));
            Set(graphics, "voiceVisualizationIntensity", VoiceIntensity > 0 ? 1f : 0f);
            Set(graphics, "lastBreath", Get(graphics, "breath"));
            Set(graphics, "breath", (float)Get(graphics, "breath") + .0125f);
            var draw = (Vector2[,])Get(graphics, "drawPositions");
            for (int i = 0; i < 3; i++) { draw[i, 1] = draw[i, 0]; draw[i, 0] = (Vector2)Get(chunks[i], "pos"); }
            var head = Get(graphics, "head");
            var forward = (draw[0, 0] - draw[1, 0]).normalized;
            var target = draw[0, 0] + forward * 100;
            if (State == PetMotionState.Charging || (State == PetMotionState.Airborne && propelled)) target = jumpTarget;
            else if (lookTarget.HasValue && Vector2.Distance(HeadPosition, lookTarget.Value) <= 180) target = lookTarget.Value;
            else if (Behavior.InterestPoint.HasValue && lookTarget.HasValue) target = lookTarget.Value;
            else if (Behavior.ExplorationTarget.HasValue) target = Behavior.ExplorationTarget.Value;
            else if (Behavior.InterestPoint.HasValue) target = Behavior.InterestPoint.Value + new Vector2(Mathf.Sin(poseTicks * .035f) * 35, 18 + Mathf.Sin(poseTicks * .06f) * 12);
            else if (Behavior.Intent == PetIntent.Idle && State == PetMotionState.Crawling && lookTarget.HasValue && poseTicks % 200 < 150) target = lookTarget.Value;
            Set(graphics, "lookPos", target);
            var aim = (target - draw[0, 0]).normalized;
            float bodyAngle = Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg;
            float targetAngle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            float neckAngle = bodyAngle + Mathf.Clamp(Mathf.DeltaAngle(bodyAngle, targetAngle), -80, 80);
            aim = new Vector2(Mathf.Cos(neckAngle * Mathf.Deg2Rad), Mathf.Sin(neckAngle * Mathf.Deg2Rad));
            // Use the original head spring and neck constraint. DrawSprites derives
            // the head sprite angle from head.pos relative to the leading chunk.
            Call(head, "Update");
            Set(head, "vel", (Vector2)Get(head, "vel") + aim * 3f);
            Call(head, "ConnectToPoint", draw[0, 0] + forward * 11.4f,
                (float)Get(graphics, "headConnectionRad"), false, .2f, Get(chunks[0], "vel"), .5f, .1f);
            Set(graphics, "lastHeadDepthRotation", Get(graphics, "headDepthRotation"));
            Set(graphics, "headDepthRotation", Mathf.Lerp((float)Get(graphics, "headDepthRotation"), (float)Get(graphics, "depthRotation") * Vector2.Dot(aim, forward), .2f));
            var limbs = (Array)Get(graphics, "limbs");
            for (int i = 0; i < 4; i++)
            {
                var limb = limbs.GetValue(i);
                Set(limb, "lastPos", Get(limb, "pos")); Set(limb, "pos", Get(feet[i], "pos"));
                var connection = Get(limb, "connection");
                float side = (float)Call(pet.Context.Type("RWCustom.Custom"), "DistanceToLine", Get(limb, "pos"), Get(connection, "pos"), Get(Get(connection, "rotationChunk"), "pos"));
                Set(limb, "flip", side * (i < 2 ? -1 : 1) < 0 ? 1f : -1f);
                Set(limb, "gripCounter", gripAge[i]);
            }
            var tails = (Array)Get(graphics, "tail");
            Set(tails.GetValue(0), "connectedPoint", (Vector2?)draw[2, 0]);
            float stiffness = (float)Get(Get(pet.Lizard, "lizardParams"), "tailStiffness");
            for (int i = tails.Length - 1; i >= 0; i--)
            {
                if (State == PetMotionState.Charging)
                {
                    var segment = tails.GetValue(i);
                    var sideways = new Vector2(-forward.y, forward.x);
                    Set(segment, "vel", (Vector2)Get(segment, "vel") - launchVelocity.normalized * i
                        + sideways * ((chargeDuration - chargeTicks) % 6 < 3 ? -5 : 5));
                }
                if (State == PetMotionState.Grabbed)
                {
                    var segment = tails.GetValue(i);
                    Set(segment, "vel", (Vector2)Get(segment, "vel") * .95f + Vector2.down * .9f);
                }
                Call(graphics, "UpdateTailSegment", i, stiffness);
                var tail = tails.GetValue(i);
                Set(tail, "pos", surfaces.Constrain((Vector2)Get(tail, "pos"), 1, out _));
            }
            var random = UnityEngine.Random.state;
            try { foreach (var cosmetic in (IEnumerable)Get(graphics, "cosmetics")) Call(cosmetic, "Update"); }
            finally { UnityEngine.Random.state = random; }
        }
    }
}
