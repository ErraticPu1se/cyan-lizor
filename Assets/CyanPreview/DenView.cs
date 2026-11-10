using System;
using UnityEngine;
using static CyanPreview.ReferenceContext;

namespace CyanPreview
{
    public sealed class DenView : IDisposable
    {
        readonly GameObject root;
        readonly Texture2D texture;
        readonly Sprite sprite;
        public DenView(Transform parent)
        {
            texture = new Texture2D(32, 32, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var pixels = new Color[32 * 32];
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
            {
                bool border = x < 2 || y < 2 || x > 29 || y > 29;
                bool glyph = (x >= 8 && x <= 23 && y >= 6 && y <= 9)
                    || ((x >= 8 && x <= 11 || x >= 20 && x <= 23) && y >= 6 && y <= 23)
                    || (x >= 14 && x <= 17 && y >= 19 && y <= 27);
                pixels[y * 32 + x] = glyph ? new Color(.9f, .88f, .8f) : border ? new Color(.4f, .38f, .35f) : new Color(.045f, .035f, .055f);
            }
            texture.SetPixels(pixels); texture.Apply();
            sprite = Sprite.Create(texture, new Rect(0, 0, 32, 32), Vector2.one * .5f, 32 / PetDen.Size);
            root = new GameObject("Movable den"); root.transform.SetParent(parent, false);
            var renderer = root.AddComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.sortingOrder = -10;
        }
        public void Refresh(PetDen den, float scale)
        { root.transform.localPosition = den.Position * scale; root.transform.localScale = Vector3.one * scale; }
        public void Dispose() { Release(root); Release(sprite); Release(texture); }
    }
}
