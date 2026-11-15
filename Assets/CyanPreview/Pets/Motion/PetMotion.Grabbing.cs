using System;
using System.Collections;
using UnityEngine;
using static CyanPreview.ReferenceContext;

namespace CyanPreview
{
    public sealed partial class PetMotion
    {
        void StepGrabbed()
        {
            Behavior.Social.Advance(TickSeconds);
            heldTicks++;
            var dragDelta = grabAnchor - (Vector2)Get(chunks[grabbedChunk], "pos");
            // Carry only excess displacement on extreme cursor warps to bound joint stretch.
            var impulse = Vector2.ClampMagnitude(dragDelta, 24);
            grabVelocity = impulse;
            if (dragDelta.magnitude > 24) ShiftWithSurface(dragDelta - impulse, false);
            Set(chunks[grabbedChunk], "vel", impulse);
            for (int i = 0; i < chunks.Length; i++)
            {
                var p = (Vector2)Get(chunks[i], "pos");
                Set(chunks[i], "lastPos", p);
                // Only the held point is fixed. Gravity and small muscular
                // impulses act on the rest through the existing body joints.
                var struggle = new Vector2(Mathf.Sin(heldTicks * .23f + i * 1.7f) * .35f,
                    Mathf.Cos(heldTicks * .17f + i) * .15f);
                var velocity = (Vector2)Get(chunks[i], "vel") * .94f + Vector2.down * .9f + struggle;
                Set(chunks[i], "pos", i == grabbedChunk ? grabAnchor : surfaces.Constrain(p + velocity, 8, out _));
                Set(chunks[i], "vel", i == grabbedChunk ? Vector2.zero : velocity);
            }
            for (int pass = 0; pass < 6; pass++)
            {
                foreach (var connection in connections) Call(connection, "Update");
                foreach (var chunk in chunks)
                {
                    Set(chunk, "pos", surfaces.Constrain((Vector2)Get(chunk, "pos"), 8, out var hit));
                    var velocity = (Vector2)Get(chunk, "vel");
                    Set(chunk, "vel", velocity - hit * Mathf.Min(0, Vector2.Dot(velocity, hit)));
                }
                Set(chunks[grabbedChunk], "pos", grabAnchor); Set(chunks[grabbedChunk], "vel", Vector2.zero);
            }
            for (int i = 0; i < 4; i++)
            {
                var shoulder = (Vector2)Get(chunks[i < 2 ? 0 : 2], "pos");
                Set(feet[i], "absoluteHuntPos", shoulder + new Vector2(Mathf.Sin(heldTicks * .28f + i * 2) * 12, -8 + Mathf.Cos(heldTicks * .28f + i * 2) * 9));
                Call(feet[i], "Update");
                float reach = (float)Get(((Array)Get(graphics, "limbs")).GetValue(i), "jointDist");
                Call(feet[i], "ConnectToPoint", shoulder, reach, false, 0f, Vector2.zero, 0f, 0f);
            }
        }
        public void BeginGrab(Vector2 point)
        {
            grabbedChunk = 0;
            for (int i = 1; i < chunks.Length; i++)
                if (Vector2.Distance(point, (Vector2)Get(chunks[i], "pos")) < Vector2.Distance(point, (Vector2)Get(chunks[grabbedChunk], "pos"))) grabbedChunk = i;
            grabAnchor = (Vector2)Get(chunks[grabbedChunk], "pos");
            grabOffset = grabAnchor - point; grabVelocity = Vector2.zero; heldTicks = 0; propelled = false;
            Behavior.Rest(); ChangeState(PetMotionState.Grabbed);
            for (int i = 0; i < 4; i++) { planted[i] = seeking[i] = false; gripAge[i] = 0; }
        }
        public void DragTo(Vector2 point)
        {
            if (State != PetMotionState.Grabbed) return;
            // Cursor updates set a target; physics owns body and tail momentum.
            if (float.IsNaN(point.x) || float.IsNaN(point.y) || float.IsInfinity(point.x) || float.IsInfinity(point.y))
                throw new ArgumentOutOfRangeException(nameof(point));
            grabAnchor = surfaces.Constrain(point + grabOffset, 8, out _);
        }
        public void EndGrab()
        {
            if (State != PetMotionState.Grabbed) return;
            Set(chunks[grabbedChunk], "vel", grabVelocity);
            foreach (var chunk in chunks) Set(chunk, "vel", Vector2.ClampMagnitude((Vector2)Get(chunk, "vel"), 18));
            cooldown = 0; flightTicks = 0; Behavior.Rest();
            ChangeState(PetMotionState.Airborne);
        }
    }
}
