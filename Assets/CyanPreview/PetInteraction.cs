using UnityEngine;

namespace CyanPreview
{
    // Input timing belongs to each pet, independently of Unity's mouse polling.
    public sealed class PetInteraction
    {
        readonly PetMotion pet;
        bool pressed, startedOnPet, dragging;
        float heldSeconds;
        public bool ClickToExplore { get; set; } = true;
        public const float GrabDelay = .1f;
        public PetInteraction(PetMotion pet) { this.pet = pet; }
        public void Press(Vector2 point)
        {
            pressed = true; heldSeconds = 0; dragging = false;
            startedOnPet = pet.Contains(point);
            if (!startedOnPet && ClickToExplore) pet.MoveTo(point);
        }
        public void Hold(Vector2 point, float seconds)
        {
            if (!pressed) return;
            heldSeconds += seconds;
            if (!dragging && startedOnPet && heldSeconds >= GrabDelay && pet.Contains(point))
            { pet.BeginGrab(point); dragging = true; }
            if (dragging) pet.DragTo(point);
        }
        public void Release(Vector2 point, bool insidePreview)
        {
            if (!pressed) return;
            if (dragging) pet.EndGrab();
            pressed = dragging = false;
        }
        public void Cancel()
        {
            if (dragging) pet.EndGrab();
            pressed = dragging = false;
        }
    }
}
