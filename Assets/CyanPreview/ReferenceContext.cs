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

    // Owns the game's process-wide graphics setup; pets borrow these resources.
    public sealed class ReferenceContext : IDisposable
    {
        static ReferenceContext active;
        readonly HashSet<ReferenceBridge> pets = new HashSet<ReferenceBridge>();
        bool disposed;
        public const string SupportedHash = "B6BE1D4E18CE219D21091B51564CB6A11C1E4106B41DE903EB8E58849CB16FDB";
        const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        readonly Assembly game;
        readonly string managedPath;
        public readonly ReferenceManifest Manifest;
        public readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();
        public ReferenceContext(string cachePath)
        {
            if (active != null) throw new InvalidOperationException("Only one shared reference context may be active.");
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
            try
            {
                AppDomain.CurrentDomain.AssemblyResolve += Resolve;
                game = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp")
                    ?? Assembly.LoadFrom(assemblyPath);
                if (!string.Equals(Path.GetFullPath(game.Location), Path.GetFullPath(assemblyPath), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Another Assembly-CSharp is already loaded. Restart with the correct game path.");
                SetupAtlas(cachePath);
                active = this;
            }
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
        public Array TypedArray(string type, params object[] values)
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
                Textures.Add(record.name, texture);
                if (!texture.LoadImage(File.ReadAllBytes(Path.Combine(cache, record.name + ".png"))))
                    throw new InvalidDataException("Could not decode " + record.name);
                texture.name = record.name;
                texture.filterMode = FilterMode.Point;
                texture.wrapMode = TextureWrapMode.Clamp;
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

        AssetBundle soundBundle;
        public AudioClip LoadSound(string name)
        {
            if (disposed) throw new ObjectDisposedException(nameof(ReferenceContext));
            if (soundBundle == null)
            {
                soundBundle = AssetBundle.LoadFromFile(Path.Combine(Manifest.gamePath, "RainWorld_Data", "StreamingAssets", "AssetBundles", "loadedsoundeffects"));
                if (soundBundle == null) throw new IOException("Cannot load this Rain World installation's sound bundle.");
            }
            var clip = soundBundle.LoadAsset<AudioClip>("Assets/LoadedSoundEffects/" + name + ".wav");
            if (clip == null) throw new IOException("Missing local game sound: " + name);
            return clip;
        }

        public ReferenceBridge CreatePet(string speciesId = "cyan")
        {
            if (disposed) throw new ObjectDisposedException(nameof(ReferenceContext));
            var pet = new ReferenceBridge(this, LizardSpecies.Find(speciesId));
            pets.Add(pet);
            return pet;
        }
        internal void RemovePet(ReferenceBridge pet) => pets.Remove(pet);
        public void Dispose()
        {
            if (disposed) return;
            if (pets.Count != 0) throw new InvalidOperationException("Dispose pet views and pets before their shared reference context.");
            disposed = true;
            AppDomain.CurrentDomain.AssemblyResolve -= Resolve;
            foreach (var texture in Textures.Values) Release(texture);
            Textures.Clear();
            if (soundBundle != null) soundBundle.Unload(true);
            if (active == this) active = null;
        }
        public static void Release(UnityEngine.Object value)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
