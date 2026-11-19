using System;
using System.Collections;
using UnityEngine;
using static CyanPreview.ReferenceContext;

namespace CyanPreview
{
    public sealed partial class PetMotion
    {
        public void MoveTo(Vector2 point) { clickInvestigation = false; Behavior.Target(surfaces.Nearest(point, out _)); }
        public void ExploreClick(Vector2 point)
        {
            if (float.IsNaN(point.x) || float.IsNaN(point.y) || float.IsInfinity(point.x) || float.IsInfinity(point.y))
                throw new ArgumentOutOfRangeException(nameof(point));
            // World-space reach scales with the rendered pet, not the monitor resolution.
            const float nearbyReach = 150; // Roughly three cyan body lengths, excluding the tail.
            var origin = surfaces.Nearest(Position, out _);
            var target = surfaces.Nearest(point, out _);
            float distance = Vector2.Distance(origin, target);
            if (distance > nearbyReach) target = surfaces.Nearest(Vector2.MoveTowards(origin, target, nearbyReach + (distance - nearbyReach) * .65f), out _);
            Behavior.Target(target);
            clickInvestigation = true;
        }
        public void BeginIdleHold()
        {
            Behavior.TaskTarget=null; Behavior.Rest(); Behavior.HoldPosition=true;
            if(State==PetMotionState.Charging) ChangeState(PetMotionState.Crawling);
        }
    }
}
