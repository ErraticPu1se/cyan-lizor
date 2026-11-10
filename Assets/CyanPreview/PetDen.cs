using System;
using UnityEngine;

namespace CyanPreview
{
    public sealed class PetDen
    {
        readonly IPetSurfaces surfaces;
        readonly Rect bounds;
        public const float Size = 44;
        public Vector2 Position { get; private set; }
        public Vector2 Entrance { get; private set; }
        public Vector2 ExitDirection { get; private set; }
        public bool Dragging { get; private set; }
        Vector2 dragOffset;
        public PetDen(IPetSurfaces surfaces, Rect bounds, Vector2 position)
        { this.surfaces = surfaces; this.bounds = bounds; Move(position); }
        public bool Contains(Vector2 point) => new Rect(Position - Vector2.one * Size / 2, Vector2.one * Size).Contains(point);
        public void Move(Vector2 position)
        {
            if (float.IsNaN(position.x) || float.IsNaN(position.y) || float.IsInfinity(position.x) || float.IsInfinity(position.y)) throw new ArgumentOutOfRangeException(nameof(position));
            position = surfaces.Constrain(position, Size / 2, out _);
            var edge = surfaces.Nearest(position, out var normal);
            Position = surfaces.Constrain(edge + normal * (Size / 2), Size / 2, out _);
            Entrance = surfaces.Constrain(edge + normal * 14, 8, out _);
            var tangent = normal == Vector2.zero ? Vector2.right : new Vector2(normal.y, -normal.x);
            if (Vector2.Dot(bounds.center - Entrance, tangent) < 0) tangent = -tangent;
            ExitDirection = tangent;
        }
        public void BeginDrag(Vector2 point) { Dragging = true; dragOffset = Position - point; }
        public void DragTo(Vector2 point) { if (Dragging) Move(point + dragOffset); }
        public void EndDrag() { Dragging = false; }
    }

    public enum DenPhase { Outside, Returning, Entering, Inside, Emerging }

    public sealed class PetDenJourney
    {
        readonly PetMotion motion;
        public PetDen Den { get; }
        public DenPhase Phase { get; private set; } = DenPhase.Outside;
        public bool Visible => Phase != DenPhase.Inside;
        public bool CanInteract => Phase == DenPhase.Outside || Phase == DenPhase.Returning;
        float accumulator;
        int ticks, stalled;
        Vector2 previous, entrance;
        public PetDenJourney(PetMotion motion, PetDen den) { this.motion = motion; Den = den; }
        public void Spawn()
        {
            motion.CancelDenDestination(); motion.Behavior.Rest();
            Phase = DenPhase.Inside; ticks = 1; stalled = 0; accumulator = 0;
        }
        // Temporary preview trigger; real prey delivery will call this task later.
        public bool RequestFoodReturn()
        {
            if (Phase != DenPhase.Outside || motion.State == PetMotionState.Grabbed) return false;
            motion.Behavior.Rest(); Phase = DenPhase.Returning; stalled = 0; previous = motion.Position;
            return true;
        }
        public void ComeOut() { if (Phase == DenPhase.Inside) ticks = 0; }
        public void CancelReturn()
        {
            if (Phase != DenPhase.Returning) return;
            Phase = DenPhase.Outside; motion.CancelDenDestination(); motion.Behavior.Rest();
        }
        public void Advance(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            accumulator += Mathf.Min(seconds, .25f);
            while (accumulator + .000001f >= .025f) { accumulator -= .025f; Step(); }
        }
        void Step()
        {
            if (Phase == DenPhase.Outside) { motion.Advance(.025f); return; }
            if (Phase == DenPhase.Returning)
            {
                if (motion.State == PetMotionState.Grabbed) { CancelReturn(); motion.Advance(.025f); return; }
                motion.SetDenDestination(Den.Entrance); motion.Advance(.025f);
                stalled = Vector2.Distance(previous, motion.Position) < .03f ? stalled + 1 : 0;
                previous = motion.Position;
                if (stalled > 400) { CancelReturn(); return; }
                if (!Den.Dragging && motion.State == PetMotionState.Crawling && Vector2.Distance(motion.Position, Den.Entrance) < 25)
                { Phase = DenPhase.Entering; ticks = 0; entrance = Den.Entrance; motion.CancelDenDestination(); }
                return;
            }
            if (Phase == DenPhase.Entering)
            {
                if (Den.Dragging || Vector2.Distance(entrance, Den.Entrance) > 2)
                { Phase = DenPhase.Returning; motion.CancelDenDestination(); return; }
                motion.EnterDenStep(Den.Entrance);
                if (++ticks >= 36) { Phase = DenPhase.Inside; ticks = 200; }
                return;
            }
            if (Phase == DenPhase.Inside)
            {
                if (Den.Dragging || --ticks > 0) return;
                entrance = Den.Entrance;
                motion.ExitDen(entrance, Den.ExitDirection);
                Phase = DenPhase.Emerging; ticks = 0;
                return;
            }
            if (Den.Dragging || Vector2.Distance(entrance, Den.Entrance) > 2)
            { Phase = DenPhase.Inside; ticks = 1; motion.CancelDenDestination(); return; }
            motion.SetDenDestination(Den.Entrance + Den.ExitDirection * 65);
            motion.Advance(.025f);
            if (++ticks >= 60)
            { Phase = DenPhase.Outside; motion.CancelDenDestination(); motion.Behavior.Rest(); }
        }
    }
}
