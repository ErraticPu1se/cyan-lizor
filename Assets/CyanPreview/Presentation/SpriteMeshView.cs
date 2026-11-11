using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CyanPreview
{
    public sealed class SpriteMeshView : IDisposable
    {
        readonly Transform root;
        readonly List<Mesh> meshes = new List<Mesh>();
        readonly List<GameObject> objects = new List<GameObject>();
        readonly Dictionary<Texture2D, Material> materials = new Dictionary<Texture2D, Material>();
        readonly GameObject headGlow;
        readonly Mesh glowMesh;
        readonly Material glowMaterial;
        public SpriteMeshView(Transform parent, int sortingOrder = 0)
        {
            root = new GameObject("Reference lizard").transform;
            root.SetParent(parent, false);
            root.gameObject.AddComponent<UnityEngine.Rendering.SortingGroup>().sortingOrder = sortingOrder;
            headGlow = new GameObject("Head light");
            headGlow.transform.SetParent(root, false);
            glowMesh = new Mesh { vertices = new[] {
                new Vector3(-1, -1), new Vector3(1, -1), new Vector3(1, 1), new Vector3(-1, 1) },
                uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up },
                triangles = new[] { 0, 1, 2, 0, 2, 3 } };
            headGlow.AddComponent<MeshFilter>().sharedMesh = glowMesh;
            glowMaterial = new Material(Shader.Find("CyanPreview/HeadGlow"));
            var glowRenderer = headGlow.AddComponent<MeshRenderer>();
            glowRenderer.sharedMaterial = glowMaterial;
            glowRenderer.sortingOrder = -1;
            headGlow.SetActive(false);
        }
        public bool DesktopTransparency
        {
            set { glowMaterial.SetInt("_DstBlend", value ? 10 : 1); }
        }
        public bool Visible { set => root.gameObject.SetActive(value); }
        public bool HeadGlowEnabled { get; set; } = true;
        public void SetScale(float scale) => root.localScale = Vector3.one * scale;

        public void Refresh(ReferenceBridge reference)
        {
            // shortcut: local light spill only; match room lighting once a desktop surface model exists.
            headGlow.SetActive(HeadGlowEnabled && reference.Species.HeadLightRadius > 0);
            headGlow.transform.localPosition = reference.HeadPosition;
            headGlow.transform.localScale = Vector3.one * reference.Species.HeadLightRadius;
            var lightColor = reference.EffectColor;
            lightColor.a = .6f;
            glowMaterial.SetColor("_Tint", lightColor);
            var sprites = reference.OrderedSprites.ToArray();
            while (objects.Count < sprites.Length)
            {
                var go = new GameObject("Reference sprite " + objects.Count);
                go.transform.SetParent(root, false);
                var mesh = new Mesh();
                mesh.MarkDynamic();
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>();
                objects.Add(go);
                meshes.Add(mesh);
            }
            for (int i = 0; i < objects.Count; i++)
            {
                var go = objects[i];
                if (i >= sprites.Length) { go.SetActive(false); continue; }
                var sprite = sprites[i];
                go.SetActive((bool)ReferenceContext.Get(sprite, "isVisible"));
                if (!go.activeSelf) continue;
                var element = ReferenceContext.Get(sprite, "element");
                string name = (string)ReferenceContext.Get(element, "name");
                var texture = reference.Textures[name];
                if (!materials.TryGetValue(texture, out var material))
                {
                    material = new Material(Shader.Find("CyanPreview/Sprite"));
                    material.mainTexture = texture;
                    materials.Add(texture, material);
                }
                var renderer = go.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.sortingOrder = i;
                Vector2[] points;
                Vector2[] uv;
                int[] indices;
                Color[] colors;
                Color color = (Color)ReferenceContext.Get(sprite, "color");
                float alpha = (float)ReferenceContext.Get(sprite, "alpha");
                if (sprite.GetType().Name == "TriangleMesh")
                {
                    points = (Vector2[])ReferenceContext.Get(sprite, "vertices");
                    uv = (Vector2[])ReferenceContext.Get(sprite, "UVvertices");
                    var triangles = (Array)ReferenceContext.Get(sprite, "triangles");
                    indices = new int[triangles.Length * 3];
                    for (int t = 0; t < triangles.Length; t++)
                    {
                        var triangle = triangles.GetValue(t);
                        indices[t * 3] = Convert.ToInt32(ReferenceContext.Get(triangle, "a"));
                        indices[t * 3 + 1] = Convert.ToInt32(ReferenceContext.Get(triangle, "b"));
                        indices[t * 3 + 2] = Convert.ToInt32(ReferenceContext.Get(triangle, "c"));
                    }
                    colors = (bool)ReferenceContext.Get(sprite, "customColor")
                        ? ((Color[])ReferenceContext.Get(sprite, "verticeColors")).ToArray()
                        : Enumerable.Repeat(color, points.Length).ToArray();
                }
                else
                {
                    ReferenceContext.Call(sprite, "UpdateLocalVertices");
                    points = (Vector2[])ReferenceContext.Get(sprite, "_localVertices");
                    uv = new[] { new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), new Vector2(0, 0) };
                    indices = new[] { 0, 1, 2, 0, 2, 3 };
                    colors = Enumerable.Repeat(color, 4).ToArray();
                }
                for (int c = 0; c < colors.Length; c++) colors[c].a *= alpha;
                var mesh = meshes[i];
                mesh.Clear();
                mesh.vertices = points.Select(p => new Vector3(p.x, p.y, 0)).ToArray();
                mesh.uv = uv;
                mesh.colors = colors;
                mesh.triangles = indices;
                mesh.RecalculateBounds();
                go.transform.localPosition = new Vector3((float)ReferenceContext.Get(sprite, "x"), (float)ReferenceContext.Get(sprite, "y"), 0);
                go.transform.localRotation = Quaternion.Euler(0, 0, -(float)ReferenceContext.Get(sprite, "rotation"));
                go.transform.localScale = new Vector3((float)ReferenceContext.Get(sprite, "scaleX"), (float)ReferenceContext.Get(sprite, "scaleY"), 1);
            }
        }
        public void Dispose()
        {
            foreach (var mesh in meshes) ReferenceContext.Release(mesh);
            foreach (var material in materials.Values) ReferenceContext.Release(material);
            ReferenceContext.Release(glowMaterial);
            ReferenceContext.Release(glowMesh);
            ReferenceContext.Release(root.gameObject);
        }
    }
}
