using System;
using UnityEngine;
using static CyanPreview.ReferenceContext;
namespace CyanPreview
{
    public sealed partial class PetMotion
    {
        // Rest lengths remain stable when the pet bends, unlike its screen bounding box.
        public float HeadToTailLength
        {
            get
            {
                float length=58; // Three connected body chunks plus head clearance.
                foreach(var segment in (Array)Get(graphics,"tail")) length+=(float)Get(segment,"connectionRad");
                return length;
            }
        }
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
