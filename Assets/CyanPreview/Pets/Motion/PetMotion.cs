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
        readonly System.Random jumpRandom, headColorRandom;
        int jumpWait = 320, poseTicks;
        readonly Vector2[] restingPose = new Vector2[3];
        bool resting;
        float idleBodyTurn, idleNeckTurn, idleLookSide=1;
        bool socialBodyTurn;
        int cursorLookTicks;
        bool cursorOwnsLook, cursorMissing;
        float IdleAttention => lookTarget.HasValue ? 1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(150,210,Vector2.Distance(Position,lookTarget.Value))) : 0;
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
        public float BodyLength { get; private set; }
        public uint FootstepRevision { get; private set; }
        public Vector2? SocialGaze => (!Behavior.InterestPoint.HasValue || Behavior.FollowingSocial) && !Behavior.TaskTarget.HasValue && !RivalTarget.HasValue ? Behavior.Social.GazePoint : null;
        public float SmokeLifeTicks => Mathf.Max(1, (70 * jumpPower - flightTicks) * 1.5f);
        public Vector2 HeadPosition => pet.HeadPosition;
        public Vector2 GazeTarget => (Vector2)Get(graphics,"lookPos");
        public bool SocialHeadAligned => SocialGaze.HasValue && GazeTarget==SocialGaze.Value &&
            Vector2.Distance(Position,SocialGaze.Value)>12 &&
            Vector2.Angle(HeadPosition-Position,SocialGaze.Value-Position)<=25;
        // Rain World CarryObject places held objects ahead of the front body chunk.
        public Vector2 MouthPosition => Position+(Position-(Vector2)Get(chunks[1],"pos")).normalized*(25*.95f);
        public float VoiceIntensity { get; set; }
        public bool TongueActive {get;set;}
        public Vector2? TongueLookTarget {get;set;}
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
            Behavior = new PetBehavior(seed, pet.Personality,pet.Species.RestPreference);
            jumpRandom = new System.Random(seed ^ 21761);
            headColorRandom = new System.Random(seed ^ 19391);
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
            BodyLength=bodyLength; Behavior.BodyLength=bodyLength;
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
        public LizardSpecies Species => pet.Species;
        void ChangeState(PetMotionState state)
        {
            FallCatch.Reset(((Vector2)Get(chunks[1], "pos")).y);
            if (state != PetMotionState.Crawling) Recovery.Cancel();
            if(state!=PetMotionState.Crawling) { poleGrip=null; windowFloor=null; }
            State = state; StateChanged?.Invoke(state);
        }
        public void Advance(float seconds)
        {
            if (pet.Graphics != graphics) throw new InvalidOperationException("Create a new movement controller after generating a pet.");
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            accumulator += Mathf.Min(seconds, .25f);
            while (accumulator + .000001f >= TickSeconds) { accumulator -= TickSeconds; BeginMotionTrace(); Step(); EndMotionTrace(); }
        }
        void Step()
        {
            if(surfaces is IWindowFloors floorStep) floorStep.BeginFloorStep();
            UpdateWindowFloor();
            // Brief cursor loss must not alternate attention and neutral pose every tick.
            if(cursorOwnsLook && cursorMissing && cursorLookTicks>0 && --cursorLookTicks==0) LookAt(null);
            if (State == PetMotionState.Grabbed)
            {
                StepGrabbed();
                UpdateGraphics(); return;
            }
            UpdateRecoveryAndJumpPlanning();
            var goal = Behavior.Update(Position, surfaces, State == PetMotionState.Crawling);
            hasDestination = goal.HasValue;
            if (goal.HasValue) destination = goal.Value;
            Set(Get(pet.Lizard, "AI"), "runSpeed", Behavior.RunSpeed);
            StepBody(out var tangent,out var normal);
            UpdateLimbs(tangent,normal);
            UpdateGraphics();
        }
    }
}
