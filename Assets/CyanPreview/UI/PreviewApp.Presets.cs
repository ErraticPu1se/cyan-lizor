using System;
using System.IO;
using UnityEngine;
namespace CyanPreview
{
    public sealed partial class PreviewApp
    {
        PetPresetStore presets;
        string presetName="", presetWarning;
        bool confirmPresetDelete;
        string selectedSpecies="cyan";
        CompanionPreferences CurrentPreferences() => new CompanionPreferences { gameFolder=gameFolder, scale=desktopScale,
            climbingMode=desktopClimbingMode,sound=desktopSounds,roam=desktopRoam,jumps=autonomousJumps,clickToExplore=desktopClicks };
        void ApplyPresetValues(PetPreset value)
        {
            selectedSpecies=value.species;
            id=value.appearanceId; desktopScale=value.scale; desktopClimbingMode=value.climbingMode;
            desktopSounds=value.sound; desktopRoam=value.roam; autonomousJumps=value.jumps; desktopClicks=value.clickToExplore;
            presetName=value.name; confirmPresetDelete=false;
        }
        void InitializePresets(CompanionPreferences legacy)
        {
            try
            {
                presets=new PetPresetStore(Path.Combine(Application.persistentDataPath,"saved-pets.v1.json"),PetPreset.Capture(id,legacy));
                ApplyPresetValues(presets.Data.Current); presetWarning=presets.Warning;
            }
            catch(Exception e) { presetWarning=e.Message+" Preset saving is disabled; your current lizard is still available."; }
        }
        void SavePreset()
        {
            if(presets==null || !ReferenceBridge.TryParseId(appliedId??id,out _,out _)) return;
            var value=PetPreset.Capture(appliedId??id,CurrentPreferences());
            value.species=reference?.Species.Id??presets.Data.Current.species;
            presets.SaveCurrent(value);
        }
        void SelectPreset(PetPresetCollection next)
        {
            ReferenceBridge candidate=null;
            try
            {
                next.Validate();
                if(context!=null)
                {
                    candidate=context.CreatePet(next.Current.species); candidate.Generate(next.Current.appearanceId);
                    candidate.Pose(0,depth); candidate.Draw();
                }
                // Load before committing the selection so a failed appearance never replaces the current pet.
                presets.Commit(next);
                if(candidate!=null) { var old=reference; reference=candidate; candidate=null; old?.Dispose(); phase=0; view.Refresh(reference); }
                ApplyPresetValues(presets.Data.Current); appliedId=Ready?id:null;
                if(Ready) { PlayerPrefs.SetString("LastId",id); status=reference.Identity+"  ·  "+reference.Species.DisplayName; }
                error=null; SavePreferences();
            }
            catch(Exception e) { Fail(e); }
            finally { candidate?.Dispose(); }
        }
    }
}
