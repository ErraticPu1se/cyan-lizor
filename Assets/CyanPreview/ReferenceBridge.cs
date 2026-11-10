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

namespace CyanPreview
{
    [Serializable] public class AtlasRecord
    {
        public string name;
        public int width, height, x, y, w, h;
    }
    [Serializable] public class ReferenceManifest
    {
        public int schema;
        public string gamePath, gameVersion, assemblySha256;
        public AtlasRecord[] elements;
    }

    // The reference assembly stays in the user's game installation. This adapter
    // invokes its real generation and drawing routines without launching game AI.
    // No copied game implementation or game binaries belong in this repository.
    public sealed class ReferenceBridge : IDisposable
    {
        public const string SupportedHash = "B6BE1D4E18CE219D21091B51564CB6A11C1E4106B41DE903EB8E58849CB16FDB";
        const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        readonly Assembly game;
        readonly string managedPath;
        public readonly ReferenceManifest Manifest;
        public readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();
        public object Graphics { get; private set; }
        public object Lizard { get; private set; }
        public object Camera { get; private set; }
        public object Leaser { get; private set; }
        public object Container { get; private set; }
        public int Seed { get; private set; }
        public string Identity { get; private set; }
        public Color EffectColor { get; private set; }
        public string Summary { get; private set; }

        public ReferenceBridge(string cachePath)
        {
            var path = Path.Combine(cachePath, "reference.json");
            if (!File.Exists(path)) throw new FileNotFoundException("Prepare your local Rain World reference first. See README.md.", path);
            Manifest = JsonUtility.FromJson<ReferenceManifest>(File.ReadAllText(path));
            if (Manifest.schema != 1 || Manifest.elements == null) throw new InvalidDataException("Unsupported reference manifest.");
            managedPath = Path.Combine(Manifest.gamePath, "RainWorld_Data", "Managed");
            var assemblyPath = Path.Combine(managedPath, "Assembly-CSharp.dll");
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(assemblyPath))
            {
                var hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
                if (hash != SupportedHash || hash != Manifest.assemblySha256)
                    throw new InvalidDataException("This reference build supports Rain World v1.11.8 only. The game DLL changed; re-check compatibility before loading.");
            }
            AppDomain.CurrentDomain.AssemblyResolve += Resolve;
            game = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp")
                ?? Assembly.LoadFrom(assemblyPath);
            if (!string.Equals(Path.GetFullPath(game.Location), Path.GetFullPath(assemblyPath), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Another Assembly-CSharp is already loaded. Restart the preview with the correct game path.");
            try { SetupAtlas(cachePath); }
            catch { Dispose(); throw; }
        }

        Assembly Resolve(object sender, ResolveEventArgs args)
        {
            var name = new AssemblyName(args.Name).Name;
            var loaded = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == name);
            if (loaded != null) return loaded;
            // Unity's own engine modules must always come from the host runtime.
            if (name.StartsWith("UnityEngine") || name.StartsWith("UnityEditor") || name == "mscorlib") return null;
            var file = Path.Combine(managedPath, name + ".dll");
            return File.Exists(file) ? Assembly.LoadFrom(file) : null;
        }
        public Type Type(string name) => game.GetType(name, true);
        public object New(string name, params object[] args) => Activator.CreateInstance(Type(name), Flags, null, args, CultureInfo.InvariantCulture);
        public object Blank(string name) => FormatterServices.GetUninitializedObject(Type(name));
        public object Static(string type, string member) => Get(Type(type), member);
        public static object Get(object target, string name)
        {
            var type = target as Type ?? target.GetType();
            for (var t = type; t != null; t = t.BaseType)
            {
                var field = t.GetField(name, Flags | BindingFlags.DeclaredOnly);
                if (field != null) return field.GetValue(target is Type ? null : target);
                var property = t.GetProperty(name, Flags | BindingFlags.DeclaredOnly);
                if (property != null) return property.GetValue(target is Type ? null : target, null);
            }
            throw new MissingMemberException(type.FullName, name);
        }
        public static void Set(object target, string name, object value)
        {
            var type = target as Type ?? target.GetType();
            for (var t = type; t != null; t = t.BaseType)
            {
                var field = t.GetField(name, Flags | BindingFlags.DeclaredOnly);
                if (field != null) { field.SetValue(target is Type ? null : target, value); return; }
                var property = t.GetProperty(name, Flags | BindingFlags.DeclaredOnly);
                if (property != null && property.GetSetMethod(true) != null)
                { property.SetValue(target is Type ? null : target, value, null); return; }
            }
            throw new MissingMemberException(type.FullName, name);
        }
        public static object Call(object target, string name, params object[] args)
        {
            var type = target as Type ?? target.GetType();
            var methods = type.GetMethods(Flags).Where(m => m.Name == name && m.GetParameters().Length == args.Length);
            foreach (var method in methods)
            {
                var p = method.GetParameters();
                if (!p.Select((item, i) => args[i] == null || item.ParameterType.IsInstanceOfType(args[i])).All(v => v)) continue;
                try { return method.Invoke(target is Type ? null : target, args); }
                catch (TargetInvocationException e) { throw new InvalidOperationException(type.Name + "." + name + ": " + e.InnerException, e.InnerException); }
            }
            throw new MissingMethodException(type.FullName, name);
        }
        Array TypedArray(string type, params object[] values)
        {
            var result = Array.CreateInstance(Type(type), values.Length);
            for (int i = 0; i < values.Length; i++) result.SetValue(values[i], i);
            return result;
        }

        void SetupAtlas(string cache)
        {
            var manager = New("FAtlasManager");
            Set(Type("Futile"), "atlasManager", manager);
            Call(Type("FFacetType"), "Init");
            // Individual textures use normalized UVs; retain original trim/source
            // dimensions so the game's sprite positioning remains intact.
            int index = 0;
            foreach (var record in Manifest.elements)
            {
                if (Path.GetFileName(record.name) != record.name) throw new InvalidDataException("Invalid atlas name.");
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!texture.LoadImage(File.ReadAllBytes(Path.Combine(cache, record.name + ".png"))))
                    throw new InvalidDataException("Could not decode " + record.name);
                texture.name = record.name;
                texture.filterMode = FilterMode.Point;
                texture.wrapMode = TextureWrapMode.Clamp;
                Textures.Add(record.name, texture);
                var atlas = Blank("FAtlas");
                Set(atlas, "_index", index++);
                Set(atlas, "_texture", texture);
                var element = New("FAtlasElement");
                Set(element, "name", record.name);
                Set(element, "atlas", atlas);
                Set(element, "sourcePixelSize", new Vector2(record.width, record.height));
                Set(element, "sourceSize", new Vector2(record.width, record.height));
                Set(element, "sourceRect", new Rect(record.x, record.y, record.w, record.h));
                Set(element, "uvRect", new Rect(0, 0, 1, 1));
                Set(element, "uvBottomLeft", new Vector2(0, 0));
                Set(element, "uvBottomRight", new Vector2(1, 0));
                Set(element, "uvTopLeft", new Vector2(0, 1));
                Set(element, "uvTopRight", new Vector2(1, 1));
                Call(manager, "AddElement", element);
            }
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
            if (!TryParseId(input, out var spawner, out var seed))
                throw new ArgumentException("Enter a whole-number seed or ID.spawner.number, for example ID.1.12345.");
            var savedRandom = UnityEngine.Random.state;
            try
            {
                Seed = seed;
                Identity = "ID." + spawner + "." + seed;
                var cyan = Static("CreatureTemplate+Type", "CyanLizard");
                var template = Blank("CreatureTemplate");
                Set(template, "type", cyan);
                var abstractCreature = Blank("AbstractCreature");
                Set(abstractCreature, "creatureTemplate", template);
                Set(abstractCreature, "ID", New("EntityID", spawner, seed));
                Lizard = Blank("Lizard");
                Set(Lizard, "abstractPhysicalObject", abstractCreature);
                Set(Lizard, "grasps", Array.CreateInstance(Type("Creature+Grasp"), 1));
                var breed = New("LizardBreedParams", cyan);
                // Static cyan geometry parameters, independently mapped from the
                // inspected build. Generation itself executes the reference ctor.
                var values = new Dictionary<string, float> {
                    {"bodySizeFac",1}, {"bodyRadFac",1}, {"bodyLengthFac",1},
                    {"headSize",.95f}, {"tailLengthFactor",1.8f}, {"limbSpeed",6},
                    {"limbQuickness",.6f}, {"limbSize",1}, {"limbThickness",1},
                    {"tailColorationStart",.3f}, {"tailColorationExponent",2},
                    {"jawOpenAngle",110}, {"jawOpenLowerJawFac",.55f}, {"jawOpenMoveJawsApart",22}
                };
                foreach (var pair in values) Set(breed, pair.Key, pair.Value);
                Set(breed, "tailSegments", 6);
                Set(breed, "tongueSegments", 7);
                Set(breed, "headGraphics", new int[5]);
                Set(Lizard, "lizardParams", breed);
                Set(Lizard, "animation", Static("Lizard+Animation", "Standard"));
                Set(Lizard, "AI", Blank("LizardAI"));
                var chunks = new object[3];
                for (int i = 0; i < 3; i++) chunks[i] = New("BodyChunk", Lizard, i, new Vector2(-17 * i, 0), 8f, 2f / 3f);
                Set(Lizard, "bodyChunks", TypedArray("BodyChunk", chunks));
                var connections = new object[3];
                for (int i = 0; i < 3; i++)
                {
                    connections[i] = Blank("PhysicalObject+BodyChunkConnection");
                    Set(connections[i], "distance", 17f);
                }
                Set(Lizard, "bodyChunkConnections", TypedArray("PhysicalObject+BodyChunkConnection", connections));
                // A retracted tongue still participates in the graphics constructor
                // and consumes its own random draws. Omitting it changes cosmetics!
                Set(Lizard, "tongue", Blank("LizardTongue"));
                Set(Get(Lizard, "tongue"), "state", Static("LizardTongue+State", "Hidden"));
                UnityEngine.Random.InitState(seed);
                var custom = Type("RWCustom.Custom");
                float hue = (float)Call(custom, "WrappedRandomVariation", .49f, .04f, .6f);
                float lightness = (float)Call(custom, "ClampedRandomVariation", .5f, .15f, .1f);
                EffectColor = (Color)Call(custom, "HSL2RGB", hue, 1f, lightness);
                Set(Lizard, "effectColor", EffectColor);
                Graphics = New("LizardGraphics", Lizard);
                Set(Lizard, "graphicsModule", Graphics);
                Set(Graphics, "culled", false);
                Set(Graphics, "lastCulled", false);
                SetupCamera();
                Leaser = New("RoomCamera+SpriteLeaser", Graphics, Camera);
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
            var room = Blank("Room");
            var tile = Blank("Room+Tile");
            Set(tile, "Terrain", Enum.Parse(Type("Room+Tile+TerrainType"), "Air"));
            Set(room, "DefaultTile", tile);
            var session = Blank("RainWorldGame");
            var rainWorld = Blank("RainWorld");
            Set(rainWorld, "setup", Blank("RainWorldGame+SetupValues"));
            Set(session, "rainWorld", rainWorld);
            Set(room, "game", session);
            Set(Lizard, "room", room);
            Camera = Blank("RoomCamera");
            Set(Camera, "room", room);
            Container = New("FContainer");
            Set(Camera, "SpriteLayers", TypedArray("FContainer", Container, New("FContainer"), New("FContainer")));
            Set(Camera, "SpriteLayerIndex", new Dictionary<string, int> { {"Midground",0}, {"Foreground",1}, {"HUD",2} });
            var palette = Blank("RoomPalette");
            Set(palette, "blackColor", new Color(.025f, .03f, .04f, 1));
            Set(Camera, "currentPalette", palette);
        }

        static void Position(object part, Vector2 point)
        { Set(part, "pos", point); Set(part, "lastPos", point); }

        public void Pose(float phase, float depth)
        {
            var positions = (Vector2[,])Get(Graphics, "drawPositions");
            var chunks = (Array)Get(Lizard, "bodyChunks");
            for (int i = 0; i < 3; i++)
            {
                var point = new Vector2(30 - i * 17, Mathf.Sin(phase + i * .5f) * 1.5f);
                positions[i, 0] = positions[i, 1] = point;
                Position(chunks.GetValue(i), point);
            }
            Position(Get(Graphics, "head"), positions[0, 0] + new Vector2(11, 1));
            Vector2 tailPoint = positions[2, 0];
            int segment = 0;
            foreach (var tail in (Array)Get(Graphics, "tail"))
            {
                float length = (float)Get(tail, "connectionRad");
                float angle = .15f * Mathf.Sin(phase + segment * .6f);
                tailPoint += new Vector2(-Mathf.Cos(angle), Mathf.Sin(angle)) * length;
                Position(tail, tailPoint);
                Set(tail, "stretched", 1f);
                segment++;
            }
            var limbs = (Array)Get(Graphics, "limbs");
            for (int i = 0; i < limbs.Length; i++)
            {
                var limb = limbs.GetValue(i);
                float side = i % 2 == 0 ? 1 : -1;
                Position(limb, positions[i < 2 ? 0 : 2, 0] + new Vector2(-10, side * 19));
                Set(limb, "flip", side);
            }
            foreach (string name in new[] { "depthRotation", "lastDepthRotation", "headDepthRotation", "lastHeadDepthRotation" }) Set(Graphics, name, depth);
            Set(Graphics, "headColorSetter", 1f);
            Set(Graphics, "breath", 0f);
            Set(Graphics, "lastBreath", 0f);
            // Settle only cosmetic springs. Never run room physics or creature AI.
            var random = UnityEngine.Random.state;
            try
            {
                foreach (var cosmetic in (IEnumerable)Get(Graphics, "cosmetics"))
                    for (int i = 0; i < 20; i++) Call(cosmetic, "Update");
            }
            finally { UnityEngine.Random.state = random; }
        }
        public void Draw()
        {
            var state = UnityEngine.Random.state;
            try { Call(Graphics, "DrawSprites", Leaser, Camera, 1f, Vector2.zero); }
            finally { UnityEngine.Random.state = state; }
        }
        public IEnumerable<object> OrderedSprites => ((IEnumerable)Get(Container, "_childNodes")).Cast<object>()
            .Where(n => Type("FSprite").IsInstanceOfType(n));
        public void Dispose()
        {
            AppDomain.CurrentDomain.AssemblyResolve -= Resolve;
            foreach (var texture in Textures.Values) Release(texture);
            Textures.Clear();
        }
        public static void Release(UnityEngine.Object value)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
