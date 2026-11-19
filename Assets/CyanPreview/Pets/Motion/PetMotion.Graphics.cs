using System;
using System.Collections;
using UnityEngine;
using static CyanPreview.ReferenceContext;

namespace CyanPreview
{
    public sealed partial class PetMotion
    {
        void UpdateGraphics()
        {
            Set(pet.Lizard, "animation", pet.Context.Static("Lizard+Animation", State == PetMotionState.Charging ? "PrepareToJump" : RivalTarget.HasValue ? "FightingStance" : "Standard"));
            Set(pet.Lizard,"lastJawOpen",Get(pet.Lizard,"jawOpen"));
            Set(pet.Lizard,"jawOpen",TongueActive?1f:RivalTarget.HasValue?RivalJaw:0f);
            Set(pet.Lizard, "timeInAnimation", chargeDuration - chargeTicks);
            Set(pet.Lizard, "timeToRemainInAnimation", chargeDuration);
            Set(graphics, "lastVoiceVisualization", Get(graphics, "voiceVisualization"));
            Set(graphics, "lastVoiceVisualizationIntensity", Get(graphics, "voiceVisualizationIntensity"));
            if(pet.Species.CanCamouflage)
            {
                // Rain World LizardGraphics.Update blink and neutral override decay at 40 Hz.
                // Desktop activity supplies excitement instead of the room-dependent game AI.
                var ai=Get(pet.Lizard,"AI");
                float excitement=RivalTarget.HasValue?.25f:Behavior.Intent==PetIntent.Idle?0f:
                    Behavior.Intent==PetIntent.Investigating?.08f:.04f;
                excitement=Mathf.Lerp((float)Get(ai,"excitement"),excitement,.1f);
                Set(ai,"excitement",excitement);
                Set(graphics,"headColorSetter",Mathf.Lerp((float)Get(graphics,"headColorSetter"),0,.2f));
                float blink=(float)Get(graphics,"blink");
                Set(graphics,"lastBlink",blink);
                Set(graphics,"blink",blink+Mathf.Lerp(.0025f,.07f,excitement)+(float)headColorRandom.NextDouble()*.001f);
                // White markings share HeadColor: avoid Cyan's full-strength binary vocal flash.
                float blend=1-Mathf.Exp(-TickSeconds*8);
                Set(graphics,"voiceVisualization",Mathf.Lerp((float)Get(graphics,"voiceVisualization"),Mathf.Clamp01(VoiceIntensity),blend));
                Set(graphics,"voiceVisualizationIntensity",Mathf.Lerp((float)Get(graphics,"voiceVisualizationIntensity"),Mathf.Clamp01(VoiceIntensity*3)*.35f,blend));
            }
            else
            {
                Set(graphics, "voiceVisualization", Mathf.Clamp01(VoiceIntensity));
                Set(graphics, "voiceVisualizationIntensity", VoiceIntensity > 0 ? 1f : 0f);
            }
            Set(graphics, "lastBreath", Get(graphics, "breath"));
            Set(graphics, "breath", (float)Get(graphics, "breath") + .0125f);
            var draw = (Vector2[,])Get(graphics, "drawPositions");
            for (int i = 0; i < 3; i++) { draw[i, 1] = draw[i, 0]; draw[i, 0] = (Vector2)Get(chunks[i], "pos"); }
            var forward=UpdateHead(draw);
            var limbs = (Array)Get(graphics, "limbs");
            for (int i = 0; i < 4; i++)
            {
                var limb = limbs.GetValue(i);
                Set(limb, "lastPos", Get(limb, "pos")); Set(limb, "pos", Get(feet[i], "pos"));
                var connection = Get(limb, "connection");
                float side = (float)Call(pet.Context.Type("RWCustom.Custom"), "DistanceToLine", Get(limb, "pos"), Get(connection, "pos"), Get(Get(connection, "rotationChunk"), "pos"));
                float flip=side * (i < 2 ? -1 : 1) < 0 ? 1f : -1f;
                // Opposite knees wrap around the rail while both feet retain their real grip positions.
                flip=Mathf.Lerp(flip,i%2==0?1f:-1f,polePoseBlend);
                Set(limb,"flip",polePoseBlend>0?Mathf.Lerp((float)Get(limb,"flip"),flip,.3f):flip);
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
