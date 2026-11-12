using System;
using System.ComponentModel;
using System.Collections.Generic;
using Unity.Collections;
using System.Runtime.InteropServices;
using UnityEngine;
using static CyanPreview.ReferenceContext;

namespace CyanPreview
{
    // Render only the pet/effects bounds, then let Windows composite their premultiplied alpha.
    public sealed class DesktopAlphaRenderer : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)] struct Point { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] struct Blend { public byte Operation, Flags, Alpha, Format; }
        [StructLayout(LayoutKind.Sequential)] struct BitmapInfo
        {
            public uint HeaderSize; public int Width, Height; public ushort Planes, Bits;
            public uint Compression, ImageSize; public int XPixels, YPixels; public uint Colors, Important;
        }
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] static extern IntPtr CreateDIBSection(IntPtr dc, ref BitmapInfo info, uint usage, out IntPtr pixels, IntPtr section, uint offset);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc, IntPtr item);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr item);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
        [DllImport("user32.dll", SetLastError = true)] static extern bool UpdateLayeredWindow(IntPtr window, IntPtr destinationDC,
            ref Point destination, ref Point size, IntPtr sourceDC, ref Point source, uint key, ref Blend blend, uint flags);
        readonly IntPtr window;
        readonly Camera camera;
        readonly Transform root;
        IntPtr dc, bitmap, previousBitmap, pixels;
        RenderTexture target;
        Texture2D capture;
        byte[] bytes;
        readonly List<Renderer> renderers = new List<Renderer>();
        bool disposed;
        int smallFrames;
        public DesktopAlphaRenderer(IntPtr window, Camera camera, Transform root)
        {
            if (window == IntPtr.Zero) throw new ArgumentException("An owned overlay window is required.", nameof(window));
            this.window = window; this.camera = camera; this.root = root;
            dc = CreateCompatibleDC(IntPtr.Zero);
            if (dc == IntPtr.Zero) throw new InvalidOperationException("Cannot create the desktop alpha surface.");
            camera.enabled = false; camera.allowHDR = false; camera.allowMSAA = false;
        }
        public static RectInt CaptureRect(Bounds bounds, Rect workArea, float scale)
        {
            int left = Mathf.Max(Mathf.CeilToInt(workArea.xMin), Mathf.FloorToInt(workArea.center.x + bounds.min.x * scale) - 2);
            int top = Mathf.Max(Mathf.CeilToInt(workArea.yMin), Mathf.FloorToInt(workArea.center.y - bounds.max.y * scale) - 2);
            int right = Mathf.Min(Mathf.FloorToInt(workArea.xMax), Mathf.CeilToInt(workArea.center.x + bounds.max.x * scale) + 2);
            int bottom = Mathf.Min(Mathf.FloorToInt(workArea.yMax), Mathf.CeilToInt(workArea.center.y - bounds.min.y * scale) + 2);
            if (right <= left || bottom <= top) return new RectInt((int)workArea.x, (int)workArea.y, 1, 1);
            int width = Mathf.Min((int)workArea.width, Mathf.CeilToInt((right - left) / 32f) * 32);
            int height = Mathf.Min((int)workArea.height, Mathf.CeilToInt((bottom - top) / 32f) * 32);
            return new RectInt(Mathf.Min(left, (int)workArea.xMax - width), Mathf.Min(top, (int)workArea.yMax - height), width, height);
        }
        public static RectInt ReuseCaptureRect(RectInt requested, Rect workArea, Vector2Int capacity, ref int smallFrames)
        {
            int maxWidth=(int)workArea.width, maxHeight=(int)workArea.height;
            bool fits=capacity.x>=requested.width && capacity.y>=requested.height && capacity.x<=maxWidth && capacity.y<=maxHeight;
            bool muchSmaller=fits && (long)requested.width*requested.height*2<(long)capacity.x*capacity.y;
            smallFrames=muchSmaller?smallFrames+1:0;
            int width, height;
            if(fits && smallFrames<120) { width=capacity.x; height=capacity.y; }
            else
            {
                // Grow in bounded buckets; shrink only after two seconds of substantially smaller bounds.
                width=Mathf.Min(maxWidth,Mathf.CeilToInt(requested.width/128f)*128);
                height=Mathf.Min(maxHeight,Mathf.CeilToInt(requested.height/128f)*128);
                if(!fits && capacity.x<=maxWidth && capacity.y<=maxHeight)
                { width=Mathf.Max(width,capacity.x); height=Mathf.Max(height,capacity.y); }
                smallFrames=0;
            }
            return new RectInt(Mathf.Clamp(requested.x,(int)workArea.xMin,(int)workArea.xMax-width),
                Mathf.Clamp(requested.y,(int)workArea.yMin,(int)workArea.yMax-height),width,height);
        }
        public static void PackPixels(NativeArray<Color32> source, byte[] destination, int width, int height)
        {
            if (!source.IsCreated || destination == null || width <= 0 || height <= 0 || source.Length != checked(width * height) || destination.Length != checked(source.Length * 4))
                throw new ArgumentException("Invalid desktop pixel buffer dimensions.");
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                var p = source[(height - 1 - y) * width + x]; int offset = (y * width + x) * 4;
                destination[offset] = Math.Min(p.b, p.a); destination[offset + 1] = Math.Min(p.g, p.a);
                destination[offset + 2] = Math.Min(p.r, p.a); destination[offset + 3] = p.a;
            }
        }
        public static bool VisibleBounds(Transform root, List<Renderer> renderers, out Bounds bounds, Rect? viewport = null)
        {
            bounds = default; bool found = false;
            root.GetComponentsInChildren(false, renderers);
            foreach (var renderer in renderers)
            {
                if (!renderer.enabled) continue;
                if (renderer is MeshRenderer && renderer.TryGetComponent<MeshFilter>(out var filter)
                    && (filter.sharedMesh == null || filter.sharedMesh.vertexCount == 0)) continue;
                var current = renderer.bounds;
                // Empty particle meshes have zero bounds at the scene origin, not at the pet.
                if (current.size.x <= 0 || current.size.y <= 0) continue;
                if (viewport.HasValue)
                {
                    // Rain World also hides some sprites by moving them far off-screen.
                    // Clip each renderer before union, otherwise that hidden sprite inflates the whole crop.
                    var area = viewport.Value;
                    float left=Mathf.Max(current.min.x,area.xMin), right=Mathf.Min(current.max.x,area.xMax);
                    float bottom=Mathf.Max(current.min.y,area.yMin), top=Mathf.Min(current.max.y,area.yMax);
                    if (right<=left || top<=bottom) continue;
                    current.SetMinMax(new Vector3(left,bottom,current.min.z),new Vector3(right,top,current.max.z));
                }
                if (!found) { bounds = current; found = true; } else bounds.Encapsulate(current);
            }
            return found;
        }
        public void Render(Rect workArea, float scale)
        {
            if (disposed) return;
            Bounds bounds; bool found;
            using (DesktopFrameProfile.Measure(DesktopFrameProfile.Stage.Bounds)) found = VisibleBounds(root, renderers, out bounds, DesktopCoordinates.ToWorld(workArea, workArea, scale));
            var rect = found ? CaptureRect(bounds, workArea, scale) : new RectInt((int)workArea.x, (int)workArea.y, 1, 1);
            rect=ReuseCaptureRect(rect,workArea,target==null?Vector2Int.zero:new Vector2Int(target.width,target.height),ref smallFrames);
            DesktopFrameProfile.Pixels(rect.width * rect.height);
            using (DesktopFrameProfile.Measure(DesktopFrameProfile.Stage.Buffers)) EnsureSize(rect.width, rect.height);
            var center = DesktopCoordinates.ToWorld(rect.center, workArea, scale);
            camera.transform.position = new Vector3(center.x, center.y, -10);
            camera.orthographicSize = rect.height / (2f * scale); camera.aspect = rect.width / (float)rect.height;
            camera.backgroundColor = Color.clear; camera.targetTexture = target;
            var active = RenderTexture.active;
            try
            {
                using (DesktopFrameProfile.Measure(DesktopFrameProfile.Stage.Camera)) camera.Render();
                RenderTexture.active = target;
                using (DesktopFrameProfile.Measure(DesktopFrameProfile.Stage.Readback)) capture.ReadPixels(new Rect(0, 0, rect.width, rect.height), 0, 0, false);
                using (DesktopFrameProfile.Measure(DesktopFrameProfile.Stage.Pack)) PackPixels(capture.GetRawTextureData<Color32>(), bytes, rect.width, rect.height);
            }
            finally { RenderTexture.active = active; }
            using (DesktopFrameProfile.Measure(DesktopFrameProfile.Stage.Present))
            {
                Marshal.Copy(bytes, 0, pixels, bytes.Length);
                var destination = new Point { X = rect.x, Y = rect.y }; var size = new Point { X = rect.width, Y = rect.height };
                var source = new Point(); var blend = new Blend { Alpha = 255, Format = 1 };
                if (!UpdateLayeredWindow(window, IntPtr.Zero, ref destination, ref size, dc, ref source, 0, ref blend, 2))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot present the desktop alpha surface.");
            }
        }
        void EnsureSize(int width, int height)
        {
            if (target != null && target.width == width && target.height == height) return;
            DesktopFrameProfile.Resize();
            ReleaseBuffers();
            target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear) { antiAliasing = 1 };
            capture = new Texture2D(width, height, TextureFormat.RGBA32, false, true); bytes = new byte[checked(width * height * 4)];
            var info = new BitmapInfo { HeaderSize = 40, Width = width, Height = -height, Planes = 1, Bits = 32 };
            bitmap = CreateDIBSection(dc, ref info, 0, out pixels, IntPtr.Zero, 0);
            if (bitmap == IntPtr.Zero) throw new InvalidOperationException("Cannot allocate desktop alpha pixels.");
            previousBitmap = SelectObject(dc, bitmap);
        }
        void ReleaseBuffers()
        {
            if (camera != null) camera.targetTexture = null;
            if(target!=null) target.Release();
            Release(target); Release(capture); target = null; capture = null;
            if (bitmap != IntPtr.Zero) { SelectObject(dc, previousBitmap); DeleteObject(bitmap); bitmap = IntPtr.Zero; }
            pixels = IntPtr.Zero; bytes = null;
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            ReleaseBuffers(); if (dc != IntPtr.Zero) { DeleteDC(dc); dc = IntPtr.Zero; }
        }
    }
}
