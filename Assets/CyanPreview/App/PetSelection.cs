using System;
namespace CyanPreview
{
    public static class PetSelection
    {
        // A new species/ID never overwrites an existing saved companion.
        public static PetPresetCollection Create(PetPresetCollection current,string species,string id,CompanionPreferences preferences,string name=null)
        {
            var definition=LizardSpecies.Find(species);
            var next=current.Copy(); var pet=PetPreset.Capture(id,preferences);
            pet.species=species; pet.name=string.IsNullOrWhiteSpace(name)?"My "+definition.DisplayName:name.Trim();
            pet.Validate(); next.pets.Add(pet); next.selected=pet.key; next.Validate(); return next;
        }
    }
}
