using System;
using UnityEngine;
namespace CyanPreview
{
    // A private extension of this pet's floor; other pets retain their normal desktop bounds.
    public sealed class DenRouteSurfaces : IPetSurfaces, IEdgeCatchSurfaces
    {
        readonly IPetSurfaces desktop;
        readonly Rect bounds;
        FrameSurfaces route;
        public bool Active => route != null;
        IPetSurfaces Current => (IPetSurfaces)route ?? desktop;
        public bool SupportsFreeClimbing => Current.SupportsFreeClimbing;
        public DenRouteSurfaces(IPetSurfaces desktop, Rect bounds)
        { this.desktop = desktop ?? throw new ArgumentNullException(nameof(desktop)); this.bounds = bounds; }
        public void Open(bool left)
        {
            var extended = bounds;
            if(left) extended.xMin -= 400; else extended.xMax += 400;
            route = new FrameSurfaces(extended);
        }
        public void Close() { route = null; }
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
