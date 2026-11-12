using System.Collections.Generic;
using UnityEngine;
namespace CyanPreview
{
    public static class DesktopPointerSnapshot
    {
        // Called only on Unity's main thread. The hook never reads live pets or food.
        public static DesktopHitSnapshot Capture(IList<DesktopPet> pets, SharedFood food, Rect area, bool paused)
        {
            if(paused) return DesktopHitSnapshot.Empty;
            var circles=new List<DesktopHitSnapshot.Circle>();
            foreach(var pet in pets) if(!pet.Feeding.Frozen) pet.Motion.AppendHitCircles(circles,area,pet.Config.scale);
            if(food.Available) circles.Add(new DesktopHitSnapshot.Circle(area.center.x+food.Position.x,area.center.y-food.Position.y,18));
            return new DesktopHitSnapshot(circles.ToArray(),area.xMin,area.yMin,area.xMax,area.yMax,food.Owner==FoodOwner.Placing);
        }
    }
}
