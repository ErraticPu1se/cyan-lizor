using System;

namespace CyanPreview
{
    // Immutable screen coordinates: safe for the input thread, with no Unity/game calls.
    public sealed class DesktopHitSnapshot
    {
        public struct Circle
        {
            public readonly float X, Y, Radius;
            public Circle(float x, float y, float radius) { X=x; Y=y; Radius=radius; }
        }
        public static readonly DesktopHitSnapshot Empty = new DesktopHitSnapshot(new Circle[0]);
        readonly Circle[] circles;
        readonly bool captureArea;
        readonly float left, top, right, bottom;
        readonly long created = System.Diagnostics.Stopwatch.GetTimestamp();
        public DesktopHitSnapshot(Circle[] circles, float left=float.NegativeInfinity, float top=float.NegativeInfinity, float right=float.PositiveInfinity, float bottom=float.PositiveInfinity, bool captureArea=false)
        { this.circles=(Circle[])circles.Clone(); this.left=left; this.top=top; this.right=right; this.bottom=bottom; this.captureArea=captureArea; }
        public bool Contains(float x, float y)
        {
            if(x<left || y<top || x>=right || y>=bottom) return false;
            // A stalled/closing renderer must not leave an invisible mouse-blocking pet.
            if ((System.Diagnostics.Stopwatch.GetTimestamp()-created)/(double)System.Diagnostics.Stopwatch.Frequency > .25) return false;
            if(captureArea) return true;
            foreach (var circle in circles)
            {
                float dx=x-circle.X, dy=y-circle.Y;
                if (circle.Radius>0 && dx*dx+dy*dy<=circle.Radius*circle.Radius) return true;
            }
            return false;
        }
    }
}
