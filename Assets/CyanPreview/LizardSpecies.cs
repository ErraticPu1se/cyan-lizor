using System;
using System.Collections.Generic;
using UnityEngine;
using static CyanPreview.ReferenceContext;

namespace CyanPreview
{
    public abstract class LizardSpecies
    {
        public abstract string Id { get; }
        public abstract string DisplayName { get; }
        public virtual float HeadLightRadius => 0;
        public abstract MovementCapabilities Movement { get; }
        internal abstract object CreateLizard(ReferenceContext context, int spawner, int seed);
        static readonly LizardSpecies cyan = new CyanSpecies();
        public static LizardSpecies Find(string id)
        {
            if (id == cyan.Id) return cyan;
            throw new NotSupportedException("Unsupported lizard species: " + id);
        }
    }

    sealed class CyanSpecies : LizardSpecies
    {
        public override string Id => "cyan";
        public override string DisplayName => "Cyan lizard";
        public override float HeadLightRadius => 100f;
        public override MovementCapabilities Movement { get; } = new MovementCapabilities(true, 2.4f, 14f, 50f);
        internal override object CreateLizard(ReferenceContext context, int spawner, int seed)
        {
            var cyan = context.Static("CreatureTemplate+Type", "CyanLizard");
            var template = context.Blank("CreatureTemplate");
            Set(template, "type", cyan);
            var abstractCreature = context.Blank("AbstractCreature");
            Set(abstractCreature, "creatureTemplate", template);
            Set(abstractCreature, "ID", context.New("EntityID", spawner, seed));
            Set(abstractCreature, "personality", context.New("AbstractCreature+Personality", Get(abstractCreature, "ID")));
            var lizard = context.Blank("Lizard");
            Set(lizard, "abstractPhysicalObject", abstractCreature);
            Set(lizard, "grasps", Array.CreateInstance(context.Type("Creature+Grasp"), 1));
            var breed = context.New("LizardBreedParams", cyan);
            // Static cyan geometry parameters, independently mapped from the
            // inspected build. Generation itself executes the reference ctor.
            var values = new Dictionary<string, float> {
                {"bodySizeFac",1}, {"bodyRadFac",1}, {"bodyLengthFac",1},
                {"headSize",.95f}, {"tailLengthFactor",1.8f}, {"limbSpeed",6},
                {"limbQuickness",.6f}, {"limbSize",1}, {"limbThickness",1},
                {"tailColorationStart",.3f}, {"tailColorationExponent",2},
                {"tailStiffness",250}, {"tailStiffnessDecline",.2f},
                {"jawOpenAngle",110}, {"jawOpenLowerJawFac",.55f}, {"jawOpenMoveJawsApart",22}
            };
            foreach (var pair in values) Set(breed, pair.Key, pair.Value);
            Set(breed, "tailSegments", 6);
            Set(breed, "tongueSegments", 7);
            Set(breed, "headGraphics", new int[5]);
            Set(lizard, "lizardParams", breed);
            Set(lizard, "animation", context.Static("Lizard+Animation", "Standard"));
            Set(lizard, "AI", context.Blank("LizardAI"));
            var chunks = new object[3];
            for (int i = 0; i < 3; i++) chunks[i] = context.New("BodyChunk", lizard, i, new Vector2(-17 * i, 0), 8f, 2f / 3f);
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
            var custom = context.Type("RWCustom.Custom");
            float hue = (float)Call(custom, "WrappedRandomVariation", .49f, .04f, .6f);
            float lightness = (float)Call(custom, "ClampedRandomVariation", .5f, .15f, .1f);
            var effectColor = (Color)Call(custom, "HSL2RGB", hue, 1f, lightness);
            Set(lizard, "effectColor", effectColor);
            return lizard;
        }
    }
}
