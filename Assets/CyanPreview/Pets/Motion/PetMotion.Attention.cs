using System;
using System.Collections;
using UnityEngine;
using static CyanPreview.ReferenceContext;

namespace CyanPreview
{
    public sealed partial class PetMotion
    {
        AttentionTuning Attention => pet.Species.Motion.Attention;
        void UpdateRestingAttention()
        {
            bool atRest = State == PetMotionState.Crawling && !hasDestination && !Recovery.Active && !RivalTarget.HasValue;
            if (atRest && !resting)
            {
                for (int i = 0; i < 3; i++) restingPose[i] = (Vector2)Get(chunks[i], "pos");
                idleBodyTurn=0;
            }
            resting = atRest;
            if(resting)
            {
                var socialLook=SocialGaze;
                var attention=TongueLookTarget ?? (Behavior.Social.HoldingGlance && socialLook.HasValue ? socialLook : lookTarget ?? socialLook);
                bool turnSocial=TongueLookTarget.HasValue || (socialLook.HasValue && attention==socialLook);
                if(socialBodyTurn && !turnSocial)
                {
                    for(int i=0;i<3;i++) restingPose[i]=BodyPosition(i);
                    idleBodyTurn=0;
                }
                socialBodyTurn=turnSocial;
                float requested=attention.HasValue ? Mathf.Clamp(StableIdleAngle(restingPose[0]-restingPose[1],attention.Value-restingPose[1]),socialBodyTurn?-180:-25,socialBodyTurn?180:25)*(attention==lookTarget && !TongueLookTarget.HasValue?IdleAttention:1) : 0;
                idleBodyTurn=Mathf.MoveTowards(idleBodyTurn,Mathf.Lerp(idleBodyTurn,requested,Attention.BodySmoothing),socialBodyTurn?Attention.DeliberateBodyDegreesPerTick:Attention.IdleBodyDegreesPerTick);
            }
        }
        float StableIdleAngle(Vector2 forward,Vector2 direction)
        {
            float angle=Vector2.SignedAngle(forward,direction);
            // A cursor crossing directly behind must not flip the neck between its two limits.
            if(Mathf.Abs(angle)>150) return Mathf.Abs(angle)*idleLookSide;
            if(Mathf.Abs(angle)>5) idleLookSide=Mathf.Sign(angle);
            return angle;
        }
        public void LookAt(Vector2? point) { lookTarget = point; cursorOwnsLook=cursorMissing=false; cursorLookTicks=0; }
        public void ObserveCursor(Vector2? point)
        {
            // Nearby pointer attention changes the pose without creating a movement goal.
            bool visible=point.HasValue && (resting ? Vector2.Distance(Position,point.Value)<=210 : Vector2.Distance(HeadPosition,point.Value)<=180);
            if(pet.Species.RestPreference>0) Behavior.ObserveOwner(visible && State==PetMotionState.Crawling?point:null);
            if(visible) { lookTarget=point; cursorOwnsLook=true; cursorMissing=false; cursorLookTicks=6; }
            else if(cursorOwnsLook && cursorLookTicks>0) cursorMissing=true;
            else LookAt(null);
        }
        Vector2 UpdateHead(Vector2[,] draw)
        {
            var head = Get(graphics, "head");
            var forward = (draw[0, 0] - draw[1, 0]).normalized;
            var target = draw[0, 0] + forward * 100;
            if (pursuitJump && RivalTarget.HasValue) target=RivalTarget.Value;
            else if (State == PetMotionState.Charging || (State == PetMotionState.Airborne && propelled)) target = jumpTarget;
            else if (RivalTarget.HasValue) target=RivalTarget.Value;
            else if (TongueLookTarget.HasValue) target=TongueLookTarget.Value;
            else if (SocialGaze.HasValue && Behavior.Social.HoldingGlance) target=SocialGaze.Value;
            else if (lookTarget.HasValue && (resting ? Vector2.Distance(Position,lookTarget.Value)<=210 : Vector2.Distance(HeadPosition,lookTarget.Value)<=180)) target = lookTarget.Value;
            else if (Behavior.InterestPoint.HasValue && lookTarget.HasValue) target = lookTarget.Value;
            else if (Behavior.ExplorationTarget.HasValue) target = Behavior.ExplorationTarget.Value;
            else if (SocialGaze.HasValue) target = SocialGaze.Value;
            else if (Behavior.InterestPoint.HasValue) target = Behavior.InterestPoint.Value + new Vector2(Mathf.Sin(poseTicks * .035f) * 35, 18 + Mathf.Sin(poseTicks * .06f) * 12);
            Set(graphics, "lookPos", target);
            var aim = (target - draw[0, 0]).normalized;
            // A cursor on the head has no stable direction; keep the neck facing forward.
            if(resting && (target-draw[0,0]).sqrMagnitude<144) aim=forward;
            float bodyAngle = Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg;
            float targetAngle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            float neckTurn=Mathf.Clamp(Mathf.DeltaAngle(bodyAngle,targetAngle),-80,80);
            bool cursorAttention=lookTarget.HasValue && target==lookTarget.Value;
            bool socialAttention=SocialGaze.HasValue && target==SocialGaze.Value;
            if(resting)
            {
                float requested=Mathf.Clamp(StableIdleAngle(forward,aim),-80,80);
                bool attending=cursorAttention || socialAttention || TongueLookTarget.HasValue;
                // Keep the neck responsive through the useful range; soften only its outer boundary.
                if(cursorAttention && !socialAttention && !TongueLookTarget.HasValue && !Behavior.InterestPoint.HasValue)
                    requested*=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(195,210,Vector2.Distance(Position,lookTarget.Value)));
                bool deliberateTurn=attending && Mathf.Abs(requested-idleNeckTurn)>12;
                idleNeckTurn=Mathf.MoveTowards(idleNeckTurn,Mathf.Lerp(idleNeckTurn,requested,deliberateTurn?Attention.DeliberateSmoothing:Attention.SmallAdjustmentSmoothing),deliberateTurn?Attention.DeliberateDegreesPerTick:Attention.SmallAdjustmentDegreesPerTick);
                neckTurn=idleNeckTurn;
            }
            else idleNeckTurn=neckTurn;
            float neckAngle = bodyAngle + neckTurn;
            aim = new Vector2(Mathf.Cos(neckAngle * Mathf.Deg2Rad), Mathf.Sin(neckAngle * Mathf.Deg2Rad));
            // Use the original head spring and neck constraint. DrawSprites derives
            // the head sprite angle from head.pos relative to the leading chunk.
            // Aim the neck support too: a forward-only anchor fights an intentional glance.
            Call(head, "Update");
            Set(head, "vel", (Vector2)Get(head, "vel") + aim * 3f);
            Call(head, "ConnectToPoint", draw[0, 0] + (resting || State==PetMotionState.Grabbed || cursorAttention || socialAttention ? aim : forward) * 11.4f,
                (float)Get(graphics, "headConnectionRad"), false, .2f, Get(chunks[0], "vel"), .5f, .1f);
            UpdatePoleRotation();
            Set(graphics, "lastHeadDepthRotation", Get(graphics, "headDepthRotation"));
            float headAlignment=Mathf.InverseLerp(0,.6f,Mathf.Abs(Vector2.Dot((target-draw[0,0]).normalized,(HeadPosition-draw[0,0]).normalized)));
            float headDepth=(float)Get(graphics,"depthRotation")*Mathf.Lerp(Vector2.Dot(aim,forward),headAlignment,polePoseBlend);
            Set(graphics,"headDepthRotation",Mathf.Lerp((float)Get(graphics,"headDepthRotation"),headDepth,Mathf.Lerp(.2f,.5f,polePoseBlend)));
            return forward;
        }
    }
}
