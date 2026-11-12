using System;
using System.Collections.Generic;
using UnityEngine;

namespace CyanPreview
{
    public enum ClimbingMode { EdgesOnly, EdgesAndBackgrounds }

    public struct DesktopWindow
    {
        public long Id;
        public Rect Bounds;
        public DesktopWindow(long id, Rect bounds) { Id = id; Bounds = bounds; }
    }
    // Desktop snapshots are front-to-back and contain no window titles or content.
    public sealed class DesktopSurfaces : IPetSurfaces, IEdgeCatchSurfaces
    {
        readonly FrameSurfaces frame;
        readonly BackgroundSurfaces background;
        readonly List<DesktopWindow> windows = new List<DesktopWindow>();
        ClimbingMode mode;
        public ClimbingMode Mode
        {
            get => mode;
            set { if (!Enum.IsDefined(typeof(ClimbingMode), value)) throw new ArgumentOutOfRangeException(nameof(value)); mode = value; }
        }
        public bool SupportsFreeClimbing => Mode == ClimbingMode.EdgesAndBackgrounds;
        public Rect Bounds => frame.Bounds;
        public int WindowCount => windows.Count;
        public bool SupportLost { get; private set; }
        public DesktopSurfaces(Rect bounds) { frame = new FrameSurfaces(bounds); background = new BackgroundSurfaces(bounds); }
        public void UpdateWindows(IEnumerable<DesktopWindow> snapshot)
        {
            windows.Clear();
            foreach (var window in snapshot)
                if (window.Bounds.width > 20 && window.Bounds.height > 20 && window.Bounds.Overlaps(Bounds)) windows.Add(window);
        }
        public Vector2 UpdateWindows(IEnumerable<DesktopWindow> snapshot, Vector2 position, bool gripping)
        {
            SupportLost = false;
            long attached = gripping ? WindowAtSurface(position) : 0;
            Rect oldBounds = default;
            foreach (var window in windows) if (window.Id == attached) oldBounds = window.Bounds;
            UpdateWindows(snapshot);
            if (attached == 0) return Vector2.zero;
            SupportLost = true;
            foreach (var window in windows)
            {
                if (window.Id != attached) continue;
                var oldEdge = new FrameSurfaces(oldBounds).Nearest(position, out var normal);
                bool onPlane = oldBounds.Contains(position) && Vector2.Distance(oldEdge, position) >= 30;
                if (onPlane) { oldEdge = position; normal = Vector2.zero; }
                var fraction = new Vector2(Mathf.InverseLerp(oldBounds.xMin, oldBounds.xMax, oldEdge.x),
                    Mathf.InverseLerp(oldBounds.yMin, oldBounds.yMax, oldEdge.y));
                // Compute changes directly: unchanged rectangles must yield exactly zero,
                // rather than round-trip a point and cancel roaming on float noise.
                var delta = (window.Bounds.min-oldBounds.min) + Vector2.Scale(fraction,window.Bounds.size-oldBounds.size);
                var nextEdge = oldEdge + delta;
                var nextPosition = position + delta;
                // A hidden or off-screen support must release the pet, not teleport it.
                var safeBounds = new Rect(Bounds.x + 8, Bounds.y + 8, Bounds.width - 16, Bounds.height - 16);
                if (!safeBounds.Contains(nextPosition) || WindowAtSurface(onPlane ? nextPosition : nextEdge + normal) != attached)
                    return Vector2.zero;
                SupportLost = false;
                return delta;
            }
            return Vector2.zero;
        }
        public long WindowAtSurface(Vector2 point)
        {
            if (SupportsFreeClimbing)
                foreach (var window in windows) if (window.Bounds.Contains(point)) return window.Id;
            return WindowAtEdge(point, out _, out _);
        }
        public long WindowAtEdge(Vector2 point, out Vector2 edge, out Vector2 normal)
        {
            return FindWindowEdge(point, 30, out edge, out normal);
        }
        long FindWindowEdge(Vector2 point, float limit, out Vector2 edge, out Vector2 normal)
        {
            edge = point; normal = Vector2.zero; float best = limit; long id = 0;
            for (int i = 0; i < windows.Count; i++)
            {
                var candidate = new FrameSurfaces(windows[i].Bounds).Nearest(point, out var inward);
                bool covered = false;
                for (int j = 0; j < i; j++) if (windows[j].Bounds.Contains(candidate)) { covered = true; break; }
                float distance = Vector2.Distance(point, candidate);
                if (covered || distance >= best || !Bounds.Contains(candidate)) continue;
                best = distance; edge = candidate; normal = inward; id = windows[i].Id;
            }
            return id;
        }
        public bool TryCatchEdge(Vector2 point, float reach, out Vector2 edge, out Vector2 normal)
        {
            edge = frame.Nearest(point, out normal);
            float distance = normal.y > .5f ? float.PositiveInfinity : Vector2.Distance(point,edge);
            if (FindWindowEdge(point, Mathf.Min(reach + .001f,distance), out var windowEdge, out var windowNormal) != 0)
            { edge = windowEdge; normal = windowNormal; distance = Vector2.Distance(point,edge); }
            if (Vector2.Dot(point-edge,normal) < 0) normal = -normal;
            return distance <= reach;
        }
        public bool IsWindowChrome(Vector2 point, float scale)
        {
            foreach (var window in windows)
            {
                var b = window.Bounds;
                if (!b.Contains(point)) continue;
                // Custom title bars do not consistently return HTCAPTION. Keep a conservative top strip.
                return point.y >= b.yMax - 40 / scale || point.x < b.xMin + 8 / scale
                    || point.x > b.xMax - 8 / scale || point.y < b.yMin + 8 / scale;
            }
            return false;
        }
        public Vector2 Nearest(Vector2 point, out Vector2 inward)
        {
            // Background support remains valid beside a frame. Do not magnetize
            // the body to an edge just because it entered the edge-search radius.
            if (SupportsFreeClimbing) return background.Nearest(point, out inward);
            var border = frame.Nearest(point, out inward);
            if (FindWindowEdge(point, Vector2.Distance(point, border), out var edge, out var normal) == 0) return border;
            inward = normal; return edge;
        }
        public Vector2 Constrain(Vector2 point, float radius, out Vector2 normal) => frame.Constrain(point, radius, out normal);
        public Vector2 Advance(Vector2 point, float distance)
        {
            if (SupportsFreeClimbing) return background.Advance(point,distance);
            long id = WindowAtEdge(point, out _, out _);
            foreach (var window in windows) if (window.Id == id)
                return VisibleStep(point, new FrameSurfaces(window.Bounds).Advance(point, distance), id);
            return frame.Advance(point, distance);
        }
        public Vector2 Approach(Vector2 point, Vector2 target, float distance)
        {
            if (SupportsFreeClimbing) return Vector2.MoveTowards(point, target, distance);
            long id = WindowAtEdge(point, out _, out _);
            foreach (var window in windows)
                if (window.Id == id) return VisibleStep(point, new FrameSurfaces(window.Bounds).Approach(point, target, distance), id);
            return frame.Approach(point, target, distance);
        }
        Vector2 VisibleStep(Vector2 origin, Vector2 next, long id)
        {
            // Stop at occlusion rather than walking onto a hidden edge.
            return Bounds.Contains(next) && WindowAtEdge(next, out _, out _) == id ? next : origin;
        }
    }
    public static class DesktopCoordinates
    {
        public static Vector2 ToWorld(Vector2 point, Rect workArea, float scale)
            => new Vector2(point.x - workArea.center.x, workArea.center.y - point.y) / scale;
        public static Rect ToWorld(Rect screenRect, Rect workArea, float scale)
        {
            var topLeft = ToWorld(screenRect.position, workArea, scale);
            return new Rect(topLeft.x, topLeft.y - screenRect.height / scale, screenRect.width / scale, screenRect.height / scale);
        }
    }
}
