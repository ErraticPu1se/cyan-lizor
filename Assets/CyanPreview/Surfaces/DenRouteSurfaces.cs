using System;
using UnityEngine;
namespace CyanPreview
{
    // A private extension of this pet's normal climbing area; other pets retain their normal desktop bounds.
    public sealed class DenRouteSurfaces : IPetSurfaces, IEdgeCatchSurfaces, IPoleSupports, IWindowFloors
    {
        readonly IPetSurfaces desktop;
        readonly Rect bounds;
        FrameSurfaces route;
        BackgroundSurfaces background;
        public bool Active => route != null;
        IPetSurfaces Current => !Active ? desktop : (IPetSurfaces)background;
        public bool SupportsFreeClimbing => Current.SupportsFreeClimbing;
        public void BeginFloorStep() { if(desktop is IWindowFloors floors) floors.BeginFloorStep(); }
        public bool TryFloor(Vector2 point,long owner,out SurfaceSupport support)
        { if(!Active && desktop is IWindowFloors floors) return floors.TryFloor(point,owner,out support); support=default; return false; }
        public bool TryLand(Vector2 previous,Vector2 next,float radius,out SurfaceSupport support)
        { if(!Active && desktop is IWindowFloors floors) return floors.TryLand(previous,next,radius,out support); support=default; return false; }
        public bool TryPole(Vector2 point,long windowId,int segment,out SurfaceSupport support)
        {
            if(Current is IPoleSupports poles) return poles.TryPole(point,windowId,segment,out support);
            support=default; return false;
        }
        public SurfaceSupport SupportAt(Vector2 point) => Current.SupportAt(point);
        public bool TryCatchSupport(Vector2 point,float reach,out SurfaceSupport support)
        {
            if(Current is IEdgeCatchSurfaces edges) return edges.TryCatchSupport(point,reach,out support);
            support=default; return false;
        }
        public DenRouteSurfaces(IPetSurfaces desktop, Rect bounds)
        { this.desktop = desktop ?? throw new ArgumentNullException(nameof(desktop)); this.bounds = bounds; }
        public void Open(float denDistance)
        {
            if(float.IsNaN(denDistance) || float.IsInfinity(denDistance) || denDistance<=0)
                throw new ArgumentOutOfRangeException(nameof(denDistance));
            var extended = bounds;
            extended.yMin -= denDistance+200;
            route = new FrameSurfaces(extended);
            background = new BackgroundSurfaces(extended);
        }
        public void Close() { route = null; background = null; }
        public Vector2 Nearest(Vector2 p, out Vector2 normal) => Current.Nearest(p, out normal);
        public Vector2 Constrain(Vector2 p, float radius, out Vector2 normal) => Current.Constrain(p, radius, out normal);
        public Vector2 Advance(Vector2 p, float distance) => Current.Advance(p, distance);
        public Vector2 Approach(Vector2 p, Vector2 destination, float distance) => Current.Approach(p, destination, distance);
        public bool TryCatchEdge(Vector2 p, float reach, out Vector2 edge, out Vector2 normal)
        {
            if(Current is IEdgeCatchSurfaces edges) return edges.TryCatchEdge(p, reach, out edge, out normal);
            edge = normal = Vector2.zero; return false;
        }
    }
}
