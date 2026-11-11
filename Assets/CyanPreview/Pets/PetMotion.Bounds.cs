using System;
using UnityEngine;
using static CyanPreview.ReferenceContext;
namespace CyanPreview
{
    public sealed partial class PetMotion
    {
        // Include the tail and limbs, not just the leading chunk, before closing an exit.
        public Rect OccupiedBounds
        {
            get
            {
                var min=HeadPosition; var max=min;
                foreach(var chunk in chunks) IncludePosition(chunk,ref min,ref max);
                foreach(var part in (Array)Get(graphics,"tail")) IncludePosition(part,ref min,ref max);
                foreach(var part in (Array)Get(graphics,"limbs")) IncludePosition(part,ref min,ref max);
                return Rect.MinMaxRect(min.x-24,min.y-24,max.x+24,max.y+24);
            }
        }
        static void IncludePosition(object part,ref Vector2 min,ref Vector2 max)
        { var p=(Vector2)Get(part,"pos"); min=Vector2.Min(min,p); max=Vector2.Max(max,p); }
    }
}
