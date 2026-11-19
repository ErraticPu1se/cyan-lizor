using System;
using System.Collections;
using UnityEngine;
using static CyanPreview.ReferenceContext;

namespace CyanPreview
{
    public sealed partial class PetMotion
    {
        void StepBody(out Vector2 tangent,out Vector2 normal)
        {
            bool airborne = State == PetMotionState.Airborne;
            poseTicks++;
            UpdateRestingAttention();
            if (airborne)
            {
                flightTicks++;
                if (propelled && flightTicks < 70 * jumpPower)
                    JumpSmoke?.Invoke((Vector2)Get(chunks[1], "pos"), (Vector2)Get(chunks[1], "vel") - (Position - (Vector2)Get(chunks[2], "pos")).normalized * 16, jumpPower);
            }
            UpdatePoleGrip();
            var edge = GroundSupport(Position,out normal);
            var ahead = hasDestination ? surfaces.Approach(edge, destination, 22) : surfaces.Advance(edge, 22);
            if(poleGrip.HasValue) ahead=PoleSupport(ahead,out _);
            if(windowFloor.HasValue) ahead=GroundSupport(ahead,out _);
            tangent = (ahead - edge).normalized;
            if (!capabilities.Supports(surfaces.SupportAt(Position).Kind)) tangent = Vector2.down;
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
                    if (propelled && i != 1) v += (pursuitJump ? ((Vector2)Get(chunks[1], "vel")).normalized : (jumpTarget - (Vector2)Get(chunks[1], "pos")).normalized)
                        * Mathf.Lerp(2, 0, Mathf.InverseLerp(10, 60, flightTicks)) * (i == 0 ? 1 : -1);
                    p += v; v = v * .999f + Vector2.down * .9f;
                }
                else
                {
                    var support = GroundSupport(p,out var inward);
                    float clearance = State == PetMotionState.Charging ? 9 : 14 - Recovery.Compression * (i == 1 ? 1 : .6f);
                    if(poleGrip.HasValue && State==PetMotionState.Crawling)
                        clearance=(i==0?8:i==1?10:12)-Recovery.Compression*.5f;
                    if (Recovery.Active && Recovery.Normal != Vector2.zero)
                    {
                        inward = Recovery.Normal;
                        support = p - inward * Vector2.Dot(p - Recovery.ContactPoint, inward);
                    }
                    var supportForce = Vector2.ClampMagnitude(support + inward * clearance - p, 6) * .18f;
                    v *= Recovery.VelocityRetention;
                    v += supportForce;
                    if(poleGrip.HasValue && !resting) v+=Vector2.down*(1-Mathf.Clamp01(GripCount/2f))*.3f;
                    if(motionSample!=null) { motionSample.normal[i]=inward; motionSample.support[i]=supportForce; }
                    if (State == PetMotionState.Crawling && hasDestination)
                    {
                        var beforeDrive = v;
                        // Head leads, the middle follows its route, and the rear is pulled
                        // by the original chunk connections instead of a rigid pose.
                        var forward = (Position - (Vector2)Get(chunks[1], "pos")).normalized;
                        float facing = Vector2.Dot(forward, tangent);
                        Vector2 targetNormal;
                        GroundSupport(ahead,out targetNormal);
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
                        if (i < 2) v += (target - p).normalized * capabilities.CrawlSpeed * (Behavior.RunSpeed / .5f) * (i == 0 ? .55f : .35f) * alignment * Mathf.InverseLerp(.3f, .9f, facing) * (poleGrip.HasValue?Mathf.Lerp(.3f,1,GripCount/4f):1);
                        v = beforeDrive + (v - beforeDrive) * Recovery.DriveScale * (Behavior.RoamStopping ? Mathf.Clamp01(Behavior.RunSpeed/.25f) : 1);
                        if(motionSample!=null) motionSample.drive[i]=v-beforeDrive;
                    }
                    if (resting)
                    {
                        // Desktop resting pose: bounded weight shifts around the
                        // arrival pose, rather than continuously driving the body.
                        float amplitude = Behavior.Intent == PetIntent.Inspecting ? 1.4f : Behavior.Intent == PetIntent.Settling ? .8f : .45f;
                        var offset = new Vector2(Mathf.Sin(poseTicks * .035f + i), Mathf.Sin(poseTicks * .06f + i * .7f)) * amplitude;
                        var restTarget = restingPose[i];
                        var pivot=socialBodyTurn?restingPose[2]:restingPose[1];
                        restTarget = pivot + (Vector2)(Quaternion.Euler(0,0,idleBodyTurn)*(restTarget-pivot));
                        if(inward!=Vector2.zero)
                        {
                            // Support owns the wall-normal position; idle sway must not pull against it.
                            restTarget+=inward*Vector2.Dot(p-restTarget,inward);
                            offset-=inward*Vector2.Dot(offset,inward);
                            restingPose[i]+=inward*Vector2.Dot(p-restingPose[i],inward);
                        }
                        v += Vector2.ClampMagnitude(restTarget + offset - p, 12) * .12f;
                        if(motionSample!=null) { motionSample.restTarget[i]=restTarget+offset; motionSample.idle[i]=Vector2.ClampMagnitude(restTarget+offset-p,12)*.12f; }
                    }
                    if(RivalStance && RivalTarget.HasValue)
                    {
                        // FightingStance braces the rear while the head leans toward its rival.
                        var direction=(RivalTarget.Value-Position).normalized;
                        v+=direction*(i==0?.4f:-.3f)*Mathf.Clamp01(GripCount/2f);
                    }
                    if(RivalPropelling && GripCount>0)
                        v+=RivalLungeDirection*Species.Biting.LungeSpeed/(i+1);
                    if(RivalRecovering && RivalTarget.HasValue && GripCount>0)
                        v+=(Position-RivalTarget.Value).normalized*.25f/(i+1);
                    if (State == PetMotionState.Charging)
                    {
                        if (i == 1) v *= .5f;
                        else v += launchVelocity.normalized * (i == 0 ? 1.5f : -2f);
                    }
                    p += v;
                }
                if(airborne && surfaces is IWindowFloors floors
                    && floors.TryLand((Vector2)Get(chunks[i],"lastPos"),p,8,out var platform))
                {
                    p.y=platform.Point.y+8;
                    float speed=Mathf.Max(0,-v.y);
                    if(speed>impactSpeed) { impactSpeed=speed; impactNormal=Vector2.up; impactPoint=platform.Point; }
                    v.y=Mathf.Max(0,v.y); landed=true; windowFloor=platform;
                }
                var beforeConstrain=p;
                p = surfaces.Constrain(p, 8, out var hit);
                if(motionSample!=null) motionSample.boundary[i]=p-beforeConstrain;
                if (hit != Vector2.zero)
                {
                    float speed = -Vector2.Dot(v, hit);
                    if (speed > impactSpeed) { impactSpeed = speed; impactNormal = hit; impactPoint = p - hit * 8; }
                    v -= hit * Mathf.Min(0, Vector2.Dot(v, hit));
                    if (cooldown < 15 && (hit.y > .5f || (propelled && capabilities.ClimbWalls))) landed = true;
                }
                Set(chunks[i], "pos", p); Set(chunks[i], "vel", v);
            }
            if(motionSample!=null) for(int i=0;i<3;i++) motionSample.beforeConnections[i]=BodyPosition(i);
            foreach (var connection in connections) Call(connection, "Update");
            if(motionSample!=null) for(int i=0;i<3;i++) motionSample.connection[i]=BodyPosition(i)-motionSample.beforeConnections[i];
            foreach (var chunk in chunks) Set(chunk, "pos", surfaces.Constrain((Vector2)Get(chunk, "pos"), 8, out _));
            if (airborne && !propelled)
                FallCatch.Advance(TickSeconds, ((Vector2)Get(chunks[1], "pos")).y, ((Vector2)Get(chunks[1], "vel")).y);
            if (airborne && propelled && capabilities.Supports(surfaces.SupportAt(jumpTarget).Kind) && flightTicks > 5 && Vector2.Distance(Position, jumpTarget) < 25
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
            if (Vector2.Distance(Position, surfaces.Nearest(Position, out var normal)) > 30 || !capabilities.Supports(surfaces.SupportAt(Position).Kind)) ReleaseSupport();
            else if (State == PetMotionState.Charging) { ChangeState(PetMotionState.Crawling); Behavior.Rest(); }
        }
    }
}
