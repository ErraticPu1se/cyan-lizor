using UnityEngine;

namespace CyanPreview
{
    // A climbable background has support everywhere inside the host bounds.
    public sealed class BackgroundSurfaces : IPetSurfaces
    {
        public bool SupportsFreeClimbing => true;
        readonly FrameSurfaces frame;
        public BackgroundSurfaces(Rect bounds) { frame = new FrameSurfaces(bounds); }
        public SurfaceSupport SupportAt(Vector2 point) => new SurfaceSupport(SurfaceKind.Background,Nearest(point,out var n),n);
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

    public readonly struct WindowPoles
    {
        readonly FrameSurfaces perimeter;
        readonly long windowId;
        public WindowPoles(Rect bounds,long windowId) { perimeter=new FrameSurfaces(bounds); this.windowId=windowId; }
        public SurfaceSupport SupportAt(Vector2 point,int preferred=-1)
        {
            var p=perimeter.Nearest(point,out var n);
            var b=perimeter.Bounds;
            int segment=n.y>0?0:n.x<0?1:n.y<0?2:3;
            if(preferred>=0 && preferred<4)
            {
                var old=preferred==0?new Vector2(Mathf.Clamp(point.x,b.xMin,b.xMax),b.yMin):preferred==1?new Vector2(b.xMax,Mathf.Clamp(point.y,b.yMin,b.yMax)):preferred==2?new Vector2(Mathf.Clamp(point.x,b.xMin,b.xMax),b.yMax):new Vector2(b.xMin,Mathf.Clamp(point.y,b.yMin,b.yMax));
                if(Vector2.Distance(point,old)<=Vector2.Distance(point,p)+6)
                { p=old; segment=preferred; n=preferred==0?Vector2.up:preferred==1?Vector2.left:preferred==2?Vector2.down:Vector2.right; }
            }
            if(Vector2.Dot(point-p,n)<0) n=-n;
            return new SurfaceSupport(SurfaceKind.Pole,p,n,windowId,segment);
        }
        public Vector2 Nearest(Vector2 point,out Vector2 normal)
        { var support=SupportAt(point); normal=support.Normal; return support.Point; }
        public Vector2 Advance(Vector2 point,float distance) => perimeter.Advance(point,distance);
        public Vector2 Approach(Vector2 point,Vector2 destination,float distance) => perimeter.Approach(point,destination,distance);
    }

    // A single vertical rail models a window edge for the movement lab.
    // shortcut: rail transfers and moving windows need a connected surface graph.
    public sealed class PoleSurfaces : IPetSurfaces, IPoleSupports
    {
        public bool SupportsFreeClimbing => false;
        readonly FrameSurfaces frame;
        readonly float x;
        public bool TryPole(Vector2 point,long windowId,int segment,out SurfaceSupport support)
        { support=SupportAt(point); return windowId==0; }
        public PoleSurfaces(Rect bounds, float x) { frame = new FrameSurfaces(bounds); this.x = x; }
        public SurfaceSupport SupportAt(Vector2 point) => new SurfaceSupport(SurfaceKind.Pole,Nearest(point,out var n),n);
        public Vector2 Nearest(Vector2 point, out Vector2 inward)
        { inward = point.x<x?Vector2.left:Vector2.right; return new Vector2(x, Mathf.Clamp(point.y, frame.Bounds.yMin, frame.Bounds.yMax)); }
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
