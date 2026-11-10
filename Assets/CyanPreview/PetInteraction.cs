using UnityEngine;

namespace CyanPreview
{
    // Input timing belongs to each pet, independently of Unity's mouse polling.
    public sealed class PetInteraction
    {
        readonly PetMotion pet;
        public PettingContact Petting { get; } = new PettingContact();
        public bool Hover(Vector2 point, float seconds, bool blocked = false)
            => Petting.Advance(seconds, !blocked && !pressed && pet.State == PetMotionState.Crawling && pet.Contains(point));
        bool pressed, startedOnPet, dragging;
        float heldSeconds;
        Vector2 pressOffset;
        public bool CapturesPointer => pressed && startedOnPet;
        public bool ClickToExplore { get; set; } = true;
        public const float GrabDelay = .1f;
        public PetInteraction(PetMotion pet) { this.pet = pet; }
        public void Press(Vector2 point)
        {
            Petting.ResetContact();
            pressed = true; heldSeconds = 0; dragging = false;
            startedOnPet = pet.Contains(point);
            pressOffset = point - pet.Position;
            if (!startedOnPet && ClickToExplore) pet.ExploreClick(point);
        }
        public void Hold(Vector2 point, float seconds)
        {
            if (!pressed) return;
            heldSeconds += seconds;
            if (!dragging && startedOnPet && heldSeconds >= GrabDelay)
            { pet.BeginGrab(pet.Position + pressOffset); dragging = true; }
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
            Petting.ResetContact();
            if (dragging) pet.EndGrab();
            pressed = dragging = false;
        }
    }
}
