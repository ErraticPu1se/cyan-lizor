using System;
using UnityEngine;

namespace CyanPreview
{
    // Geometry is supplied by the host; simulation never enumerates OS windows.
    public interface IPetSurfaces
    {
        bool SupportsFreeClimbing { get; }
        Vector2 Nearest(Vector2 point, out Vector2 inward);
        Vector2 Constrain(Vector2 point, float radius, out Vector2 normal);
        Vector2 Advance(Vector2 point, float distance);
        Vector2 Approach(Vector2 point, Vector2 destination, float distance);
    }

    public sealed class FrameSurfaces : IPetSurfaces
    {
        public bool SupportsFreeClimbing => false;
        public Rect Bounds { get; }
        public FrameSurfaces(Rect bounds) { Bounds = bounds; }
        public Vector2 Nearest(Vector2 p, out Vector2 inward)
        {
            p = new Vector2(Mathf.Clamp(p.x, Bounds.xMin, Bounds.xMax), Mathf.Clamp(p.y, Bounds.yMin, Bounds.yMax));
            float[] distances = { p.y - Bounds.yMin, Bounds.xMax - p.x, Bounds.yMax - p.y, p.x - Bounds.xMin };
            int side = 0;
            for (int i = 1; i < 4; i++) if (distances[i] < distances[side]) side = i;
            inward = side == 0 ? Vector2.up : side == 1 ? Vector2.left : side == 2 ? Vector2.down : Vector2.right;
            if (side == 0) p.y = Bounds.yMin;
            else if (side == 1) p.x = Bounds.xMax;
            else if (side == 2) p.y = Bounds.yMax;
            else p.x = Bounds.xMin;
            return p;
        }
        public Vector2 Constrain(Vector2 p, float radius, out Vector2 normal)
        {
            var result = new Vector2(Mathf.Clamp(p.x, Bounds.xMin + radius, Bounds.xMax - radius), Mathf.Clamp(p.y, Bounds.yMin + radius, Bounds.yMax - radius));
            normal = (result - p).normalized;
            return result;
        }
        float PerimeterPosition(Vector2 point)
        {
            var p = Nearest(point, out var n);
            float w = Bounds.width, h = Bounds.height;
            return n == Vector2.up ? p.x - Bounds.xMin : n == Vector2.left ? w + p.y - Bounds.yMin : n == Vector2.down ? w + h + Bounds.xMax - p.x : 2 * w + h + Bounds.yMax - p.y;
        }
        public Vector2 Approach(Vector2 point, Vector2 destination, float distance)
        {
            float perimeter = 2 * (Bounds.width + Bounds.height);
            float delta = Mathf.Repeat(PerimeterPosition(destination) - PerimeterPosition(point), perimeter);
            if (delta > perimeter / 2) delta -= perimeter;
            return Advance(point, Mathf.Sign(delta) * Mathf.Min(distance, Mathf.Abs(delta)));
        }
        public Vector2 Advance(Vector2 point, float distance)
        {
            float w = Bounds.width, h = Bounds.height;
            float t = PerimeterPosition(point);
            t = Mathf.Repeat(t + distance, 2 * (w + h));
            if (t < w) return new Vector2(Bounds.xMin + t, Bounds.yMin);
            if ((t -= w) < h) return new Vector2(Bounds.xMax, Bounds.yMin + t);
            if ((t -= h) < w) return new Vector2(Bounds.xMax - t, Bounds.yMax);
            return new Vector2(Bounds.xMin, Bounds.yMax - (t - w));
        }
    }

    public sealed class MovementCapabilities
    {
        public readonly bool ClimbWalls;
        public readonly float CrawlSpeed, LaunchMin, LaunchMax;
        public MovementCapabilities(bool climbWalls, float crawlSpeed, float launchMin = 0, float launchMax = 0)
        { ClimbWalls = climbWalls; CrawlSpeed = crawlSpeed; LaunchMin = launchMin; LaunchMax = launchMax; }
    }
}
