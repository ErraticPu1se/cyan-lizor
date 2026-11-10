using UnityEngine;

namespace CyanPreview
{
    // A climbable background has support everywhere inside the host bounds.
    public sealed class BackgroundSurfaces : IPetSurfaces
    {
        public bool SupportsFreeClimbing => true;
        readonly FrameSurfaces frame;
        public BackgroundSurfaces(Rect bounds) { frame = new FrameSurfaces(bounds); }
        public Vector2 Nearest(Vector2 point, out Vector2 inward)
        { inward = Vector2.zero; return frame.Constrain(point, 8, out _); }
        public Vector2 Constrain(Vector2 point, float radius, out Vector2 normal) => frame.Constrain(point, radius, out normal);
        public Vector2 Advance(Vector2 point, float distance)
        {
            float angle = distance * .13f;
            return Constrain(point + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Mathf.Abs(distance), 14, out _);
        }
        public Vector2 Approach(Vector2 point, Vector2 destination, float distance) => Vector2.MoveTowards(point, destination, distance);
    }

    // A single vertical rail models a window edge for the movement lab.
    // shortcut: rail transfers and moving windows need a connected surface graph.
    public sealed class PoleSurfaces : IPetSurfaces
    {
        public bool SupportsFreeClimbing => false;
        readonly FrameSurfaces frame;
        readonly float x;
        public PoleSurfaces(Rect bounds, float x) { frame = new FrameSurfaces(bounds); this.x = x; }
        public Vector2 Nearest(Vector2 point, out Vector2 inward)
        { inward = Vector2.right; return new Vector2(x, Mathf.Clamp(point.y, frame.Bounds.yMin, frame.Bounds.yMax)); }
        public Vector2 Constrain(Vector2 point, float radius, out Vector2 normal) => frame.Constrain(point, radius, out normal);
        public Vector2 Advance(Vector2 point, float distance)
        {
            float height = frame.Bounds.height;
            float offset = Mathf.PingPong(point.y - frame.Bounds.yMin + distance, height);
            return new Vector2(x, frame.Bounds.yMin + offset);
        }
        public Vector2 Approach(Vector2 point, Vector2 destination, float distance)
            => Vector2.MoveTowards(Nearest(point, out _), Nearest(destination, out _), distance);
    }
}
