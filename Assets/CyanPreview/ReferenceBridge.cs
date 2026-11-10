using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using UnityEngine;

using static CyanPreview.ReferenceContext;

namespace CyanPreview
{
    // Per-pet state only. Disposing this pet never releases shared atlas textures.
    public sealed class ReferenceBridge : IDisposable
    {
        readonly ReferenceContext context;
        internal ReferenceContext Context => context;
        bool disposed;
        public Guid PetId { get; } = Guid.NewGuid();
        public LizardSpecies Species { get; }
        public ReferenceManifest Manifest => context.Manifest;
        public IReadOnlyDictionary<string, Texture2D> Textures => context.Textures;
        public object Graphics { get; private set; }
        public object Lizard { get; private set; }
        public object Camera { get; private set; }
        public object Leaser { get; private set; }
        public object Container { get; private set; }
        public int Seed { get; private set; }
        public string Identity { get; private set; }
        public Color EffectColor { get; private set; }
        public string Summary { get; private set; }
        public Vector2 HeadPosition => (Vector2)Get(Get(Graphics, "head"), "pos");

        internal ReferenceBridge(ReferenceContext context, LizardSpecies species)
        {
            this.context = context;
            Species = species;
        }
        void EnsureAlive()
        {
            if (disposed) throw new ObjectDisposedException(nameof(ReferenceBridge));
        }
        public static bool TryParseId(string text, out int spawner, out int seed)
        {
            spawner = -1;
            seed = 0;
            text = (text ?? "").Trim();
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out seed)) return true;
            var parts = text.Split('.');
            return parts.Length == 3 && parts[0] == "ID"
                && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out spawner)
                && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out seed);
        }

        public void Generate(string input)
        {
            EnsureAlive();
            if (!TryParseId(input, out var spawner, out var seed))
                throw new ArgumentException("Enter a whole-number seed or ID.spawner.number, for example ID.1.12345.");
            var savedRandom = UnityEngine.Random.state;
            try
            {
                Seed = seed;
                Identity = "ID." + spawner + "." + seed;
                Lizard = Species.CreateLizard(context, spawner, seed);
                EffectColor = (Color)Get(Lizard, "effectColor");
                Graphics = context.New("LizardGraphics", Lizard);
                Set(Lizard, "graphicsModule", Graphics);
                Set(Graphics, "culled", false);
                Set(Graphics, "lastCulled", false);
                SetupCamera();
                Leaser = context.New("RoomCamera+SpriteLeaser", Graphics, Camera);
                Pose(0, .7f);
                Draw();
                var iv = Get(Graphics, "iVars");
                var cosmetics = ((IEnumerable)Get(Graphics, "cosmetics")).Cast<object>().Select(c => c.GetType().Name);
                Summary = string.Join(" / ", cosmetics) + "\n" +
                    string.Join("   ", new[] { "headSize", "fatness", "tailLength", "tailFatness", "tailColor" }
                        .Select(n => n + " " + ((float)Get(iv, n)).ToString("0.000", CultureInfo.InvariantCulture)));
            }
            finally { UnityEngine.Random.state = savedRandom; }
        }

        void SetupCamera()
        {
            var room = context.Blank("Room");
            var tile = context.Blank("Room+Tile");
            Set(tile, "Terrain", Enum.Parse(context.Type("Room+Tile+TerrainType"), "Air"));
            Set(room, "DefaultTile", tile);
            var session = context.Blank("RainWorldGame");
            var rainWorld = context.Blank("RainWorld");
            Set(rainWorld, "setup", context.Blank("RainWorldGame+SetupValues"));
            Set(session, "rainWorld", rainWorld);
            Set(room, "game", session);
            Set(Lizard, "room", room);
            Camera = context.Blank("RoomCamera");
            Set(Camera, "room", room);
            Container = context.New("FContainer");
            Set(Camera, "SpriteLayers", context.TypedArray("FContainer", Container, context.New("FContainer"), context.New("FContainer")));
            Set(Camera, "SpriteLayerIndex", new Dictionary<string, int> { {"Midground",0}, {"Foreground",1}, {"HUD",2} });
            var palette = context.Blank("RoomPalette");
            Set(palette, "blackColor", new Color(.025f, .03f, .04f, 1));
            Set(Camera, "currentPalette", palette);
        }

        static void Position(object part, Vector2 point)
        { Set(part, "pos", point); Set(part, "lastPos", point); }

        public void Pose(float phase, float depth)
        {
            EnsureAlive();
            var positions = (Vector2[,])Get(Graphics, "drawPositions");
            var chunks = (Array)Get(Lizard, "bodyChunks");
            float profile = Mathf.Clamp01(Mathf.Abs(depth) / .7f);
            for (int i = 0; i < 3; i++)
            {
                var point = new Vector2(30 - i * 17, i * 5f * profile + Mathf.Sin(phase + i * .5f) * 1.5f);
                positions[i, 0] = positions[i, 1] = point;
                Position(chunks.GetValue(i), point);
            }
            for (int step = 0; step < 12; step++)
                foreach (var connection in (Array)Get(Lizard, "bodyChunkConnections")) Call(connection, "Update");
            for (int i = 0; i < 3; i++)
            {
                var point = (Vector2)Get(chunks.GetValue(i), "pos");
                positions[i, 0] = positions[i, 1] = point;
                Position(chunks.GetValue(i), point);
                Set(chunks.GetValue(i), "vel", Vector2.zero);
            }
            Position(Get(Graphics, "head"), positions[0, 0] + new Vector2(11, Mathf.Lerp(1, -3, profile)));
            Vector2 tailPoint = positions[2, 0];
            int segment = 0;
            foreach (var tail in (Array)Get(Graphics, "tail"))
            {
                float length = (float)Get(tail, "connectionRad");
                float angle = profile * (.3f - segment * .18f) + .15f * Mathf.Sin(phase + segment * .6f);
                tailPoint += new Vector2(-Mathf.Cos(angle), Mathf.Sin(angle)) * length;
                Position(tail, tailPoint);
                Set(tail, "vel", Vector2.zero);
                Set(tail, "stretched", 1f);
                segment++;
            }
            var tailParts = (Array)Get(Graphics, "tail");
            Set(tailParts.GetValue(0), "connectedPoint", (Vector2?)positions[2, 0]);
            Set(Graphics, "tailDirection", Vector2.zero);
            float stiffness = (float)Get(Get(Lizard, "lizardParams"), "tailStiffness");
            // Use the game's gravity, stiffness and segment constraints, without creature AI.
            for (int step = 0; step < 12; step++)
                for (int i = tailParts.Length - 1; i >= 0; i--)
                    Call(Graphics, "UpdateTailSegment", i, stiffness);
            var limbs = (Array)Get(Graphics, "limbs");
            for (int i = 0; i < limbs.Length; i++)
            {
                var limb = limbs.GetValue(i);
                float side = i % 2 == 0 ? 1 : -1;
                var shoulder = positions[i < 2 ? 0 : 2, 0];
                var spreadFoot = shoulder + new Vector2(-10, side * 19);
                var groundedFoot = new Vector2(shoulder.x - 7 + side * 4, -9 + side * 2);
                Position(limb, Vector2.Lerp(spreadFoot, groundedFoot, profile));
                var connection = Get(limb, "connection");
                float distance = (float)Call(context.Type("RWCustom.Custom"), "DistanceToLine",
                    Get(limb, "pos"), Get(connection, "pos"), Get(Get(connection, "rotationChunk"), "pos"));
                distance *= i > 1 ? 1 : -1;
                Set(limb, "flip", distance < 0 ? 1f : -1f);
            }
            foreach (string name in new[] { "depthRotation", "lastDepthRotation", "headDepthRotation", "lastHeadDepthRotation" }) Set(Graphics, name, depth);
            Set(Graphics, "headColorSetter", 1f);
            Set(Graphics, "breath", 0f);
            Set(Graphics, "lastBreath", 0f);
            // Settle only cosmetic springs. Never run room physics or creature AI.
            var random = UnityEngine.Random.state;
            try
            {
                UnityEngine.Random.InitState(Seed);
                foreach (var cosmetic in (IEnumerable)Get(Graphics, "cosmetics"))
                {
                    // Comparison poses must not retain spring history from earlier angles.
                    Call(cosmetic, "Reset");
                    if (context.Type("LizardCosmetics.LongBodyScales").IsInstanceOfType(cosmetic))
                    {
                        var scales = (Array)Get(cosmetic, "scaleObjects");
                        for (int i = 0; i < scales.Length; i++)
                        {
                            var back = Call(cosmetic, "GetBackPos", i, 1f, true);
                            Position(scales.GetValue(i), (Vector2)Get(back, "outerPos"));
                            Set(scales.GetValue(i), "vel", Vector2.zero);
                        }
                    }
                    for (int i = 0; i < 20; i++) Call(cosmetic, "Update");
                }
            }
            finally { UnityEngine.Random.state = random; }
        }
        public void Draw(float interpolation = 1f)
        {
            EnsureAlive();
            var state = UnityEngine.Random.state;
            try { Call(Graphics, "DrawSprites", Leaser, Camera, Mathf.Clamp01(interpolation), Vector2.zero); }
            finally { UnityEngine.Random.state = state; }
        }
        public IEnumerable<object> OrderedSprites => ((IEnumerable)Get(Container, "_childNodes")).Cast<object>()
            .Where(n => context.Type("FSprite").IsInstanceOfType(n));
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Graphics = Lizard = Camera = Leaser = Container = null;
            context.RemovePet(this);
        }
    }
}
