using System;
using System.Collections;
using UnityEngine;
using static CyanPreview.ReferenceContext;

namespace CyanPreview
{
    public sealed partial class PetMotion
    {
        void UpdateRecoveryAndJumpPlanning()
        {
            float bodySpeed = 0;
            foreach (var chunk in chunks) bodySpeed = Mathf.Max(bodySpeed, ((Vector2)Get(chunk, "vel")).magnitude);
            Recovery.Advance(TickSeconds, GripCount, bodySpeed);
            if (cooldown > 0) cooldown--;
            var exploration = Behavior.ExplorationTarget;
            if ((Behavior.TaskTarget.HasValue ? AutonomousJumps : (!clickInvestigation || AutonomousJumps)) && State == PetMotionState.Crawling && !Recovery.Active && cooldown == 0 && GripCount > 0 && exploration.HasValue
                && Vector2.Distance(Position, exploration.Value) > (clickInvestigation ? 35 : Mathf.Lerp(190, 120, pet.Personality.Bravery))
                && (!RivalPursuingCarrier || CanPlanPursuitJump(exploration.Value))
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
            RefreshPursuitCharge();
            if (State == PetMotionState.Charging && --chargeTicks == 0)
            {
                foreach (var chunk in chunks) Set(chunk, "vel", launchVelocity);
                for (int i = 0; i < 4; i++) { planted[i] = seeking[i] = false; gripAge[i] = 0; }
                cooldown = 20;
                ChangeState(PetMotionState.Airborne);
                JumpLaunched?.Invoke((Vector2)Get(chunks[1], "pos"), launchVelocity, jumpPower);
            }
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
        // Explicit host task; ordinary roam/click jump preferences remain unchanged.
        public bool TryTaskJump(Vector2 point)
        {
            if(State!=PetMotionState.Crawling || Recovery.Active || cooldown>0 || GripCount==0) return false;
            if(!PetJumpPlanner.TryPlanToTarget(Position,point,surfaces,capabilities,out var candidate)) return false;
            launchVelocity=candidate.Velocity; BeginJump(candidate.Landing,candidate.Power); return true;
        }
        void BeginJump(Vector2 point, float power)
        {
            pursuitJump = RivalPursuingCarrier;
            jumpPower = Mathf.Clamp01(power);
            jumpTarget = point;
            propelled = true;
            flightTicks = 0;
            chargeTicks = chargeDuration = power > .75f ? 45 : power > .25f ? 30 : 15;
            ChangeState(PetMotionState.Charging);
        }
    }
}
