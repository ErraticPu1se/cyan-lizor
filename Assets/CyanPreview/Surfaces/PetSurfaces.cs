using System;
using UnityEngine;

namespace CyanPreview
{
    public enum SurfaceKind { Floor, Wall, Ceiling, Pole, Background }
    public readonly struct SurfaceSupport
    {
        public readonly SurfaceKind Kind;
        public readonly Vector2 Point, Normal;
        public readonly long WindowId;
        public readonly int Segment;
        public SurfaceSupport(SurfaceKind kind,Vector2 point,Vector2 normal,long windowId=0,int segment=-1)
        { Kind=kind; Point=point; Normal=normal; WindowId=windowId; Segment=segment; }
    }

    // Geometry is supplied by the host; simulation never enumerates OS windows.
    public interface IPetSurfaces
    {
        bool SupportsFreeClimbing { get; }
        SurfaceSupport SupportAt(Vector2 point);
        Vector2 Nearest(Vector2 point, out Vector2 inward);
        Vector2 Constrain(Vector2 point, float radius, out Vector2 normal);
        Vector2 Advance(Vector2 point, float distance);
        Vector2 Approach(Vector2 point, Vector2 destination, float distance);
    }

    public interface IWindowFloors
    {
        void BeginFloorStep();
        bool TryFloor(Vector2 point,long owner,out SurfaceSupport support);
        bool TryLand(Vector2 previous,Vector2 next,float radius,out SurfaceSupport support);
    }

    public interface IPoleSupports
    {
        bool TryPole(Vector2 point,long windowId,int segment,out SurfaceSupport support);
    }

    public interface IEdgeCatchSurfaces
    {
        bool TryCatchEdge(Vector2 point, float reach, out Vector2 edge, out Vector2 normal);
        bool TryCatchSupport(Vector2 point,float reach,out SurfaceSupport support);
    }

    public sealed class FrameSurfaces : IPetSurfaces, IEdgeCatchSurfaces
    {
        public bool SupportsFreeClimbing => false;
        public Rect Bounds { get; }
        public FrameSurfaces(Rect bounds) { Bounds = bounds; }
        public SurfaceSupport SupportAt(Vector2 point)
        {
            var p=Nearest(point,out var n);
            return new SurfaceSupport(n.y>.5f?SurfaceKind.Floor:n.y<-.5f?SurfaceKind.Ceiling:SurfaceKind.Wall,p,n);
        }
        public bool TryCatchSupport(Vector2 point,float reach,out SurfaceSupport support)
        { support=SupportAt(point); return TryCatchEdge(point,reach,out _,out _); }
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
        public bool TryCatchEdge(Vector2 point, float reach, out Vector2 edge, out Vector2 normal)
        {
            edge = Nearest(point, out normal);
            return normal.y < .5f && Vector2.Distance(point,edge) <= reach; // Ground uses normal collision landing.
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
        public readonly bool ClimbWalls, ClimbPoles, ClimbBackgrounds;
        public bool CanPropel => LaunchMax>0;
        public readonly float CrawlSpeed, LaunchMin, LaunchMax;
        public MovementCapabilities(bool climbWalls, float crawlSpeed, float launchMin = 0, float launchMax = 0,
            bool? climbPoles=null,bool? climbBackgrounds=null)
        { ClimbWalls=climbWalls; ClimbPoles=climbPoles??climbWalls; ClimbBackgrounds=climbBackgrounds??climbWalls;
          CrawlSpeed=crawlSpeed; LaunchMin=launchMin; LaunchMax=launchMax; }
        public bool Supports(SurfaceKind kind)
        {
            switch(kind)
            {
                case SurfaceKind.Floor: return true;
                case SurfaceKind.Wall: case SurfaceKind.Ceiling: return ClimbWalls;
                case SurfaceKind.Pole: return ClimbPoles;
                case SurfaceKind.Background: return ClimbBackgrounds;
                default: return false;
            }
        }
    }
}
