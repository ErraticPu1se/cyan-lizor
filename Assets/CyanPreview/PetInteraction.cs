using UnityEngine;

namespace CyanPreview
{
    // Input timing belongs to each pet, independently of Unity's mouse polling.
    public sealed class PetInteraction
    {
        readonly PetMotion pet;
        readonly PetDenJourney denJourney;
        bool draggingDen;
        bool pressed, startedOnPet, dragging;
        float heldSeconds;
        public bool ClickToExplore { get; set; } = true;
        public bool HasPointerCapture => pressed || draggingDen;
        public const float GrabDelay = .1f;
        public PetInteraction(PetMotion pet, PetDenJourney denJourney = null) { this.pet = pet; this.denJourney = denJourney; }
        public void Press(Vector2 point)
        {
            Cancel();
            if (denJourney != null && denJourney.Den.Contains(point))
            { draggingDen = true; denJourney.Den.BeginDrag(point); return; }
            if (denJourney != null && !denJourney.CanInteract) return;
            pressed = true; heldSeconds = 0; dragging = false;
            startedOnPet = pet.Contains(point);
            if (!startedOnPet && ClickToExplore && (denJourney == null || denJourney.Phase == DenPhase.Outside)) pet.MoveTo(point);
        }
        public void Hold(Vector2 point, float seconds)
        {
            if (draggingDen) { denJourney.Den.DragTo(point); return; }
            if (!pressed) return;
            heldSeconds += seconds;
            if (!dragging && startedOnPet && heldSeconds >= GrabDelay && pet.Contains(point))
            { denJourney?.CancelReturn(); pet.BeginGrab(point); dragging = true; }
            if (dragging) pet.DragTo(point);
        }
        public void Release(Vector2 point, bool insidePreview)
        {
            if (draggingDen) { denJourney.Den.EndDrag(); draggingDen = false; return; }
            if (!pressed) return;
            if (dragging) pet.EndGrab();
            pressed = dragging = false;
        }
        public void Cancel()
        {
            if (draggingDen) denJourney.Den.EndDrag();
            draggingDen = false;
            if (dragging) pet.EndGrab();
            pressed = dragging = false;
        }
    }
}
