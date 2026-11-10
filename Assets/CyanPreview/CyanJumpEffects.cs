using System;
using System.Collections.Generic;
using UnityEngine;
using static CyanPreview.ReferenceContext;

namespace CyanPreview
{
    // Host rendering adaptation of CyanLizardSmoke, JumpLight and the launch
    // shockwave. Game room shaders/underwater collisions are not available here.
    public sealed class CyanJumpEffects : IDisposable
    {
        struct Particle
        {
            public Vector2 Position, Velocity;
            public float Age, Life, Radius;
            public bool Ring, Flash;
        }
        readonly PetMotion motion;
        readonly Color color;
        readonly System.Random random;
        readonly List<Particle> particles = new List<Particle>();
        readonly GameObject root;
        readonly Mesh mesh;
        readonly Material material;
        bool disposed;
        public int ParticleCount => particles.Count;
        public int BurstCount { get; private set; }
        public CyanJumpEffects(Transform parent, PetMotion motion, Color color, int seed)
        {
            this.motion = motion; this.color = color; random = new System.Random(seed ^ 68431);
            root = new GameObject("Cyan jump effects"); root.transform.SetParent(parent, false);
            mesh = new Mesh { name = "Cyan jump particles" }; mesh.MarkDynamic();
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            material = new Material(Shader.Find("CyanPreview/JumpParticle"));
            var renderer = root.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material; renderer.sortingOrder = 1000;
            motion.JumpLaunched += Burst; motion.JumpSmoke += Trail;
        }
        float Next() => (float)random.NextDouble();
        Vector2 Direction() { float a = Next() * Mathf.PI * 2; return new Vector2(Mathf.Cos(a), Mathf.Sin(a)); }
        public void SetScale(float scale) => root.transform.localScale = Vector3.one * scale;
        void Add(Vector2 p, Vector2 v, float life, float radius, bool ring = false, bool flash = false)
        {
            if (particles.Count >= 128) particles.RemoveAt(0);
            particles.Add(new Particle { Position = p, Velocity = v, Life = life, Radius = radius, Ring = ring, Flash = flash });
        }
        void Burst(Vector2 p, Vector2 velocity, float power)
        {
            BurstCount++;
            for (int i = 0; i < 7; i++)
                Add(p, -velocity * Next() * .5f + Direction() * Mathf.Lerp(6, 23, power),
                    Mathf.Lerp(30, 140, power) * Mathf.Lerp(.3f, 1, Next()) / 40, Mathf.Lerp(28, 46, Next()));
            for (int i = 0; i < 1 + (int)(power * 10); i++)
                Add(p, Direction() * (2 + power * 10), .3f + Next() * .6f, 2 + power * 5, true);
            Add(p - velocity.normalized * 10, Vector2.zero, Mathf.Lerp(4, 22, power * power) / 40, Mathf.Lerp(50, 90, power), false, true);
            Add(p - velocity.normalized * 15, Vector2.zero, (6 + power * 4) / 40, Mathf.Lerp(40, 120, power), true);
        }
        void Trail(Vector2 p, Vector2 velocity, float power)
        { Add(p, velocity + Direction() * 2, .25f + power, 12 + power * 20); }
        public void Advance(float seconds)
        {
            if (disposed) return;
            float dt = Mathf.Clamp(seconds, 0, .25f);
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                var p = particles[i]; p.Age += dt;
                if (p.Age >= p.Life) { particles.RemoveAt(i); continue; }
                p.Position += p.Velocity * (40 * dt);
                p.Velocity *= Mathf.Pow(Mathf.Clamp(.7f + .3f / Mathf.Sqrt(Mathf.Max(1, p.Velocity.magnitude)), .7f, 1), dt * 40);
                particles[i] = p;
            }
            var vertices = new List<Vector3>(); var uv = new List<Vector3>(); var colors = new List<Color>(); var triangles = new List<int>();
            foreach (var p in particles)
            {
                float age = p.Age / p.Life;
                float radius = p.Radius * (p.Flash ? 1 - age * .4f : Mathf.Lerp(.12f, 1, Mathf.Pow(age, .3f)));
                var tint = p.Flash ? Color.Lerp(color, Color.white, .5f) : color;
                tint.a = (1 - age) * (p.Ring ? .65f : p.Flash ? .7f : .3f);
                int first = vertices.Count;
                for (int corner = 0; corner < 4; corner++)
                {
                    float x = corner == 1 || corner == 2 ? 1 : -1, y = corner >= 2 ? 1 : -1;
                    vertices.Add(new Vector3(p.Position.x + x * radius, p.Position.y + y * radius));
                    uv.Add(new Vector3(x, y, p.Ring ? 1 : 0)); colors.Add(tint);
                }
                triangles.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
            }
            mesh.Clear(); mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0);
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            motion.JumpLaunched -= Burst; motion.JumpSmoke -= Trail;
            particles.Clear(); Release(root); Release(mesh); Release(material);
        }
    }
}
