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
    public sealed class DesktopSurfaces : IPetSurfaces, IEdgeCatchSurfaces, IPoleSupports, IWindowFloors
    {
        readonly FrameSurfaces frame;
        readonly MovementCapabilities capabilities;
        readonly BackgroundSurfaces background;
        readonly List<DesktopWindow> windows = new List<DesktopWindow>();
        readonly Dictionary<long,Rect> pendingFloorBounds=new Dictionary<long,Rect>();
        readonly Dictionary<long,Rect> stepFloorBounds=new Dictionary<long,Rect>();
        bool floorSnapshotPending;
        ClimbingMode mode;
        public void BeginFloorStep()
        {
            stepFloorBounds.Clear();
            if(floorSnapshotPending) foreach(var entry in pendingFloorBounds) stepFloorBounds[entry.Key]=entry.Value;
            pendingFloorBounds.Clear(); floorSnapshotPending=false;
        }
        bool FloorVisible(int index,Vector2 point)
        {
            if(!Bounds.Contains(point)) return false;
            for(int j=0;j<index;j++) if(windows[j].Bounds.Contains(point)) return false;
            return true;
        }
        public bool TryFloor(Vector2 point,long owner,out SurfaceSupport support)
        {
            support=default;
            for(int i=0;i<windows.Count;i++)
            {
                var w=windows[i]; if(w.Id!=owner || point.x<w.Bounds.xMin || point.x>w.Bounds.xMax) continue;
                var p=new Vector2(point.x,w.Bounds.yMax);
                if(!FloorVisible(i,p)) return false;
                support=new SurfaceSupport(SurfaceKind.Floor,p,Vector2.up,w.Id,2); return true;
            }
            return false;
        }
        public bool TryLand(Vector2 previous,Vector2 next,float radius,out SurfaceSupport support)
        {
            support=default; float first=2;
            if(float.IsNaN(radius)||float.IsInfinity(radius)||radius<0) return false;
            for(int i=0;i<windows.Count;i++)
            {
                var w=windows[i]; var old=stepFloorBounds.TryGetValue(w.Id,out var bounds)?bounds:w.Bounds;
                float before=previous.y-radius-old.yMax,after=next.y-radius-w.Bounds.yMax;
                if(before<-.01f || after>0 || before-after<=.0001f) continue;
                float t=Mathf.Clamp01(before/(before-after));
                float x=Mathf.Lerp(previous.x,next.x,t);
                if(x<Mathf.Lerp(old.xMin,w.Bounds.xMin,t) || x>Mathf.Lerp(old.xMax,w.Bounds.xMax,t)
                    || next.x<w.Bounds.xMin || next.x>w.Bounds.xMax || t>=first) continue;
                var p=new Vector2(next.x,w.Bounds.yMax);
                if(!FloorVisible(i,p)) continue;
                first=t; support=new SurfaceSupport(SurfaceKind.Floor,p,Vector2.up,w.Id,2);
            }
            return first<=1;
        }
        public ClimbingMode Mode
        {
            get => mode;
            set { if (!Enum.IsDefined(typeof(ClimbingMode), value)) throw new ArgumentOutOfRangeException(nameof(value)); mode = value; }
        }
        public bool SupportsFreeClimbing => Mode == ClimbingMode.EdgesAndBackgrounds && capabilities.ClimbBackgrounds;
        public Rect Bounds => frame.Bounds;
        public int WindowCount => windows.Count;
        public bool SupportLost { get; private set; }
        public DesktopSurfaces(Rect bounds,MovementCapabilities capabilities=null)
        { frame=new FrameSurfaces(bounds); background=new BackgroundSurfaces(bounds); this.capabilities=capabilities??new MovementCapabilities(true,0); }
        public bool TryPole(Vector2 point,long windowId,int segment,out SurfaceSupport support)
        {
            support=default;
            if(!capabilities.ClimbPoles) return false;
            for(int i=0;i<windows.Count;i++)
            {
                if(windows[i].Id!=windowId) continue;
                support=new WindowPoles(windows[i].Bounds,windowId).SupportAt(point,segment);
                if(!Bounds.Contains(support.Point)) return false;
                for(int j=0;j<i;j++) if(windows[j].Bounds.Contains(support.Point)) return false;
                return true;
            }
            return false;
        }
        SurfaceSupport BoundarySupport(Vector2 point)
        {
            if(capabilities.ClimbWalls) return frame.SupportAt(point);
            return new SurfaceSupport(SurfaceKind.Floor,new Vector2(Mathf.Clamp(point.x,Bounds.xMin,Bounds.xMax),Bounds.yMin),Vector2.up);
        }
        public SurfaceSupport SupportAt(Vector2 point)
        {
            // Preserve background support near poles; proximity alone must not pull the body onto a rail.
            if(SupportsFreeClimbing)
            {
                var support=background.SupportAt(point);
                long owner=0;
                foreach(var window in windows) if(window.Bounds.Contains(point)) { owner=window.Id; break; }
                return new SurfaceSupport(SurfaceKind.Background,support.Point,support.Normal,owner);
            }
            var boundary=BoundarySupport(point);
            foreach(var window in windows)
                if(point.y>=window.Bounds.yMax && TryFloor(point,window.Id,out var floor)
                    && Vector2.Distance(point,floor.Point)<Vector2.Distance(point,boundary.Point)) boundary=floor;
            long id=FindWindowEdge(point,Vector2.Distance(point,boundary.Point),out var edge,out var normal);
            return id==0?boundary:new SurfaceSupport(SurfaceKind.Pole,edge,normal,id);
        }
        public void UpdateWindows(IEnumerable<DesktopWindow> snapshot)
        {
            if(!floorSnapshotPending)
            { foreach(var w in windows) pendingFloorBounds[w.Id]=w.Bounds; floorSnapshotPending=true; }
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
                var oldEdge = new WindowPoles(oldBounds,attached).Nearest(position, out var normal);
                bool onPlane = SupportsFreeClimbing && oldBounds.Contains(position) && Vector2.Distance(oldEdge, position) >= 30;
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
            foreach(var window in windows)
                if(point.y>=window.Bounds.yMax && point.y-window.Bounds.yMax<30 && TryFloor(point,window.Id,out _)) return window.Id;
            return WindowAtEdge(point, out _, out _);
        }
        public long WindowAtEdge(Vector2 point, out Vector2 edge, out Vector2 normal)
        {
            return FindWindowEdge(point, 30, out edge, out normal);
        }
        long FindWindowEdge(Vector2 point, float limit, out Vector2 edge, out Vector2 normal)
        {
            edge = point; normal = Vector2.zero; float best = limit; long id = 0;
            if(!capabilities.ClimbPoles) return 0;
            for (int i = 0; i < windows.Count; i++)
            {
                var candidate = new WindowPoles(windows[i].Bounds,windows[i].Id).Nearest(point, out var inward);
                bool covered = false;
                for (int j = 0; j < i; j++) if (windows[j].Bounds.Contains(candidate)) { covered = true; break; }
                float distance = Vector2.Distance(point, candidate);
                if (covered || distance >= best || !Bounds.Contains(candidate)) continue;
                best = distance; edge = candidate; normal = inward; id = windows[i].Id;
            }
            return id;
        }
        public bool TryCatchEdge(Vector2 point,float reach,out Vector2 edge,out Vector2 normal)
        {
            bool found=TryCatchSupport(point,reach,out var support);
            edge=support.Point; normal=support.Normal; return found;
        }
        public bool TryCatchSupport(Vector2 point,float reach,out SurfaceSupport support)
        {
            support=BoundarySupport(point);
            if(float.IsNaN(reach)||float.IsInfinity(reach)||reach<0) return false;
            float distance=support.Kind==SurfaceKind.Floor?float.PositiveInfinity:Vector2.Distance(point,support.Point);
            long id=FindWindowEdge(point,Mathf.Min(reach+.001f,distance),out var edge,out var normal);
            if(id!=0) { support=new SurfaceSupport(SurfaceKind.Pole,edge,normal,id); distance=Vector2.Distance(point,edge); }
            return distance<=reach;
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
            var support=SupportAt(point); inward=support.Normal; return support.Point;
        }
        public Vector2 Constrain(Vector2 point, float radius, out Vector2 normal) => frame.Constrain(point, radius, out normal);
        public Vector2 Advance(Vector2 point, float distance)
        {
            if (SupportsFreeClimbing) return background.Advance(point,distance);
            var support=SupportAt(point);
            if(support.Kind==SurfaceKind.Floor && support.WindowId!=0)
                foreach(var w in windows) if(w.Id==support.WindowId) return new Vector2(Mathf.Clamp(point.x+distance,w.Bounds.xMin,w.Bounds.xMax),w.Bounds.yMax);
            long id = WindowAtEdge(point, out _, out _);
            foreach (var window in windows) if (window.Id == id)
                return VisibleStep(point, new WindowPoles(window.Bounds,window.Id).Advance(point, distance), id);
            return capabilities.ClimbWalls?frame.Advance(point,distance):BoundarySupport(point+Vector2.right*distance).Point;
        }
        public Vector2 Approach(Vector2 point, Vector2 target, float distance)
        {
            if (SupportsFreeClimbing) return Vector2.MoveTowards(point, target, distance);
            var support=SupportAt(point);
            if(support.Kind==SurfaceKind.Floor && support.WindowId!=0)
                foreach(var w in windows) if(w.Id==support.WindowId) return Vector2.MoveTowards(support.Point,new Vector2(Mathf.Clamp(target.x,w.Bounds.xMin,w.Bounds.xMax),w.Bounds.yMax),distance);
            long id = WindowAtEdge(point, out _, out _);
            foreach (var window in windows)
                if (window.Id == id) return VisibleStep(point, new WindowPoles(window.Bounds,window.Id).Approach(point, target, distance), id);
            return capabilities.ClimbWalls?frame.Approach(point,target,distance):Vector2.MoveTowards(BoundarySupport(point).Point,BoundarySupport(target).Point,distance);
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
