using System;
using System.Collections.Generic;
using UnityEngine;
using static CyanPreview.ReferenceContext;

namespace CyanPreview
{
    static class LizardBodyFactory
    {
        internal static object Create(ReferenceContext context,int spawner,int seed,bool white)
        {
            var speciesType = context.Static("CreatureTemplate+Type", white?"WhiteLizard":"CyanLizard");
            var template = context.Blank("CreatureTemplate");
            Set(template, "type", speciesType);
            var abstractCreature = context.Blank("AbstractCreature");
            Set(abstractCreature, "creatureTemplate", template);
            Set(abstractCreature, "ID", context.New("EntityID", spawner, seed));
            Set(abstractCreature, "personality", context.New("AbstractCreature+Personality", Get(abstractCreature, "ID")));
            var lizard = context.Blank("Lizard");
            Set(lizard, "abstractPhysicalObject", abstractCreature);
            Set(lizard, "grasps", Array.CreateInstance(context.Type("Creature+Grasp"), 1));
            var breed = context.New("LizardBreedParams", speciesType);
            // Geometry mapped from the installed breeds; cosmetic generation executes the reference constructor.
            var values = new Dictionary<string, float> {
                {"bodySizeFac",1}, {"bodyRadFac",1}, {"bodyLengthFac",1},
                {"headSize",.95f}, {"tailLengthFactor",1.8f}, {"limbSpeed",6},
                {"limbQuickness",.6f}, {"limbSize",1}, {"limbThickness",1},
                {"tailColorationStart",.3f}, {"tailColorationExponent",2},
                {"tailStiffness",250}, {"tailStiffnessDecline",.2f},
                {"jawOpenAngle",110}, {"jawOpenLowerJawFac",.55f}, {"jawOpenMoveJawsApart",22}
            };
            if(white)
            {
                values["headSize"]=1; values["tailLengthFactor"]=1.2f; values["limbSpeed"]=8; values["limbQuickness"]=.8f;
                values["tailColorationStart"]=.1f; values["tailColorationExponent"]=1.2f;
                values["tailStiffness"]=800; values["tailStiffnessDecline"]=.1f;
                values["jawOpenLowerJawFac"]=.5f; values["jawOpenMoveJawsApart"]=23;
                Set(breed,"standardColor",Color.white);
            }
            foreach (var pair in values) Set(breed, pair.Key, pair.Value);
            Set(breed, "tailSegments", white?5:6);
            Set(breed, "tongueSegments", white?10:7);
            Set(breed, "headGraphics", white?new[]{0,0,0,0,3}:new int[5]);
            Set(lizard, "lizardParams", breed);
            Set(lizard, "animation", context.Static("Lizard+Animation", "Standard"));
            Set(lizard, "AI", context.Blank("LizardAI"));
            var chunks = new object[3];
            for (int i = 0; i < 3; i++) chunks[i] = context.New("BodyChunk", lizard, i, new Vector2(-17 * i, 0), 8f, (white?2.1f:2f) / 3f);
            Set(lizard, "bodyChunks", context.TypedArray("BodyChunk", chunks));
            var normal = context.Static("PhysicalObject+BodyChunkConnection+Type", "Normal");
            var push = context.Static("PhysicalObject+BodyChunkConnection+Type", "Push");
            var connections = new[] {
                context.New("PhysicalObject+BodyChunkConnection", chunks[0], chunks[1], 17f, normal, .95f, .5f),
                context.New("PhysicalObject+BodyChunkConnection", chunks[1], chunks[2], 17f, normal, .95f, .5f),
                context.New("PhysicalObject+BodyChunkConnection", chunks[0], chunks[2], 17f, push, .1f, .5f)
            };
            Set(lizard, "bodyChunkConnections", context.TypedArray("PhysicalObject+BodyChunkConnection", connections));
            // A retracted tongue still participates in the graphics constructor
            // and consumes its own random draws. Omitting it changes cosmetics!
            Set(lizard, "tongue", context.Blank("LizardTongue"));
            Set(Get(lizard, "tongue"), "state", context.Static("LizardTongue+State", "Hidden"));
            UnityEngine.Random.InitState(seed);
            if(white) { Set(lizard,"effectColor",Color.white); return lizard; }
            var custom = context.Type("RWCustom.Custom");
            float hue = (float)Call(custom, "WrappedRandomVariation", .49f, .04f, .6f);
            float lightness = (float)Call(custom, "ClampedRandomVariation", .5f, .15f, .1f);
            var effectColor = (Color)Call(custom, "HSL2RGB", hue, 1f, lightness);
            Set(lizard, "effectColor", effectColor);
            return lizard;
        }
    }
}
