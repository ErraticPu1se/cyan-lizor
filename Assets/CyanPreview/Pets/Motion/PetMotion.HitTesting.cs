using System;
using System.Collections;
using UnityEngine;
using static CyanPreview.ReferenceContext;

namespace CyanPreview
{
    public sealed partial class PetMotion
    {
        public void AppendHitCircles(System.Collections.Generic.List<DesktopHitSnapshot.Circle> circles, Rect area, float scale)
        {
            for(int i=0;i<3;i++) circles.Add(HitCircle(BodyPosition(i),14,area,scale));
            foreach(var part in (Array)Get(graphics,"tail")) circles.Add(HitCircle((Vector2)Get(part,"pos"),7,area,scale));
            circles.Add(HitCircle(HeadPosition,12,area,scale));
        }
        static DesktopHitSnapshot.Circle HitCircle(Vector2 point,float radius,Rect area,float scale)
            => new DesktopHitSnapshot.Circle(area.center.x+point.x*scale,area.center.y-point.y*scale,radius*scale);
        public bool Contains(Vector2 point)
        {
            foreach (var chunk in chunks)
                if (Vector2.Distance(point, (Vector2)Get(chunk, "pos")) <= 14) return true;
            foreach (var tail in (Array)Get(graphics, "tail"))
                if (Vector2.Distance(point, (Vector2)Get(tail, "pos")) <= 7) return true;
            return Vector2.Distance(point, pet.HeadPosition) <= 12;
        }
    }
}
