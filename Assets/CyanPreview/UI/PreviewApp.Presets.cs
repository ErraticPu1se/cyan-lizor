using System;
using System.IO;
using UnityEngine;
namespace CyanPreview
{
    public sealed partial class PreviewApp
    {
        PetPresetStore presets;
        string presetName="", presetWarning;
        bool showPresets, confirmPresetDelete, speciesOpen;
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
        void DrawPresets()
        {
            GUILayout.BeginVertical(theme.Card);
            GUILayout.Label("Choose your lizard",theme.Heading);
            if(Section("Species · "+LizardSpecies.Find(selectedSpecies).DisplayName,ref speciesOpen))
                foreach(var species in LizardSpecies.AvailableIds)
                    if(GUILayout.Button(LizardSpecies.Find(species).DisplayName,selectedSpecies==species?theme.Selected:GUI.skin.button))
                    { selectedSpecies=species; speciesOpen=false; confirmPresetDelete=false; }
            if(Section("Saved presets · "+LizardSpecies.Find(selectedSpecies).DisplayName,ref showPresets))
            {
                if(!string.IsNullOrEmpty(presetWarning)) GUILayout.Label(presetWarning,theme.Small);
                if(presets!=null)
                {
                    bool any=false;
                    foreach(var item in presets.Data.pets.ToArray())
                    {
                        if(item.species!=selectedSpecies) continue;
                        any=true;
                        if(GUILayout.Button(item.name+" · "+item.appearanceId,item.key==presets.Data.selected?theme.Selected:GUI.skin.button))
                        {
                            try { SavePreset(); var next=presets.Data.Copy(); next.selected=item.key; SelectPreset(next); }
                            catch(Exception e) { Fail(e); }
                        }
                    }
                    if(!any) GUILayout.Label("No saved presets for this species yet. Apply an ID below to create one.",theme.Small);
                    GUI.enabled=selectedSpecies==presets.Data.Current.species;
                    presetName=GUILayout.TextField(presetName,40);
                    GUILayout.BeginHorizontal();
                    if(GUILayout.Button("Save as new"))
                    { try { SavePreset(); SelectPreset(PetSelection.Create(presets.Data,selectedSpecies,id,CurrentPreferences(),presetName)); } catch(Exception e) { Fail(e); } }
                    if(GUILayout.Button("Rename")) { try { presets.Rename(presetName); presetName=presets.Data.Current.name; } catch(Exception e) { Fail(e); } }
                    GUI.enabled=selectedSpecies==presets.Data.Current.species && presets.Data.pets.Count>1;
                    if(GUILayout.Button("Delete")) confirmPresetDelete=true;
                    GUI.enabled=true; GUILayout.EndHorizontal();
                    if(confirmPresetDelete)
                    {
                        GUILayout.Label("Delete this saved pet? Another preset will become active.",theme.Small);
                        GUILayout.BeginHorizontal();
                        if(GUILayout.Button("Confirm delete")) SelectPreset(presets.WithoutCurrent());
                        if(GUILayout.Button("Cancel")) confirmPresetDelete=false;
                        GUILayout.EndHorizontal();
                    }
                }
            }
            GUILayout.Label("Appearance ID · "+LizardSpecies.Find(selectedSpecies).DisplayName,theme.Small);
            GUILayout.BeginHorizontal(); GUI.SetNextControlName("CreatureId");
            id=GUILayout.TextField(id,64,GUILayout.Height(34));
            GUI.enabled=context!=null && ReferenceBridge.TryParseId(id,out _,out _);
            if(GUILayout.Button("Apply ID",theme.Primary,GUILayout.Width(94),GUILayout.Height(34))) Generate();
            GUI.enabled=true; GUILayout.EndHorizontal();
            GUILayout.Label("Seed number or ID.spawner.number",theme.Small);
            if(reference!=null && selectedSpecies!=reference.Species.Id)
                GUILayout.Label("Browsing only. Apply ID creates a new preset; your current pet is unchanged.",theme.Small);
            if(selectedSpecies=="white") GUILayout.Label("Blends into its background after resting. Uses its tongue to catch loose food.",theme.Small);
            GUILayout.EndVertical();
        }
    }
}
