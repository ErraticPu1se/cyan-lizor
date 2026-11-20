using System;
using System.Collections.Generic;
using UnityEngine;

namespace CyanPreview
{
    public sealed partial class PreviewApp
    {
        string selectedPetKey="primary", liveId, liveName;
        ReferenceBridge selectionPreview;
        Vector2 petsScroll, speciesScroll;
        bool petPanelOpen, speciesSelectorOpen;
        bool addOpen, savePresetOpen;
        PetPreset addDraft;
        string draftPresetKey;
        readonly Dictionary<string,string> livePresetKeys=new Dictionary<string,string>();
        int PetCount => (primaryPresent?1:0)+extraPets.Count;

        DesktopPetConfiguration SelectedLivePet()
        {
            if(selectedPetKey=="primary" && primaryPresent && Ready)
                return DesktopPetConfiguration.From(PrimaryConfiguration(),"primary",primaryName??presets?.Data.Current.name??"My lizard");
            return extraPets.Find(p=>p.key==selectedPetKey);
        }
        string primaryName, presetFeedback;
        void SelectLivePet(string key)
        {
            ReferenceBridge candidate=null;
            try
            {
                var config=key=="primary" && primaryPresent && Ready
                    ?DesktopPetConfiguration.From(PrimaryConfiguration(),"primary",primaryName??presets?.Data.Current.name??"My lizard")
                    :extraPets.Find(p=>p.key==key);
                if(config!=null && key!="primary")
                {
                    candidate=context.CreatePet(config.species); candidate.Generate(config.id);
                    candidate.Pose(phase,depth); candidate.Draw();
                }
                var old=selectionPreview; selectionPreview=candidate; candidate=null; old?.Dispose();
                selectedPetKey=config?.key; liveId=config?.id; liveName=config?.name;
                if(Ready) RefreshPreview();
            }
            catch(Exception e) { Fail(e); }
            finally { candidate?.Dispose(); }
        }
        void ApplyLiveControls(DesktopPetConfiguration pet)
        {
            if(pet.key!="primary") return;
            desktopScale=pet.scale; desktopRoam=pet.roam; desktopSounds=pet.sound;
            desktopClicks=pet.clickToExplore; desktopClimbingMode=pet.climbingMode;
        }
        void ApplyLiveId(DesktopPetConfiguration pet)
        {
            ReferenceBridge candidate=null;
            try
            {
                candidate=context.CreatePet(pet.species); candidate.Generate(liveId);
                candidate.Pose(phase,depth); candidate.Draw();
                if(pet.key=="primary")
                {
                    var old=reference; reference=candidate; candidate=null; old.Dispose();
                    id=appliedId=liveId;
                }
                else
                {
                    var old=selectionPreview; selectionPreview=candidate; candidate=null; old?.Dispose(); pet.id=liveId;
                }
                error=null; RefreshPreview();
            }
            catch(Exception e) { Fail(e); }
            finally { candidate?.Dispose(); }
        }
        void SaveLivePreset(DesktopPetConfiguration pet,bool asNew)
        {
            try
            {
                var next=presets.Data.Copy();
                string key=LivePreset(pet)?.key;
                var existing=next.pets.Find(p=>p.key==key);
                if(!asNew && existing==null) throw new InvalidOperationException("Save this pet as a new preset first.");
                var value=PetPreset.Capture(pet.id,new CompanionPreferences {scale=pet.scale,sound=pet.sound,roam=pet.roam,
                    jumps=pet.jumps,clickToExplore=pet.clickToExplore,climbingMode=pet.climbingMode});
                value.species=pet.species; value.name=(liveName??"").Trim();
                if(asNew) next.pets.Add(value);
                else { value.key=existing.key; next.pets[next.pets.IndexOf(existing)]=value; }
                // Preserve the startup selection when saving another live pet.
                presets.Commit(next); livePresetKeys[pet.key]=value.key;
                if(pet.key=="primary") primaryName=value.name; else pet.name=value.name;
                presetFeedback="Saved: "+value.name; error=null;
            }
            catch(Exception e) { Fail(e); }
        }
        PetPreset LivePreset(DesktopPetConfiguration pet)
        {
            if(presets==null || pet==null) return null;
            return livePresetKeys.TryGetValue(pet.key,out var key)?presets.Data.pets.Find(p=>p.key==key):null;
        }
        void UseOnNextLaunch(string key)
        {
            try
            {
                var next=presets.Data.Copy(); next.selected=key; presets.Commit(next);
                presetFeedback="Next launch: "+presets.Data.Current.name; error=null;
            }
            catch(Exception e) { Fail(e); }
        }
        bool MatchesSavedPreset(DesktopPetConfiguration pet)
        {
            var saved=LivePreset(pet);
            return saved!=null && saved.name==(liveName??pet.name).Trim() && saved.species==pet.species && SameAppearanceId(saved.appearanceId,pet.id)
                && saved.scale==pet.scale && saved.sound==pet.sound && saved.roam==pet.roam && saved.jumps==pet.jumps
                && saved.clickToExplore==pet.clickToExplore && saved.climbingMode==pet.climbingMode;
        }
        static bool SameAppearanceId(string first,string second) =>
            ReferenceBridge.TryParseId(first,out var firstSpawner,out var firstSeed)
            && ReferenceBridge.TryParseId(second,out var secondSpawner,out var secondSeed)
            && firstSpawner==secondSpawner && firstSeed==secondSeed;
        void ChooseDraftSpecies(string species)
        {
            LizardSpecies.Find(species);
            speciesSelectorOpen=false;
            if(addDraft!=null && addDraft.species==species) return;
            addDraft=PetPreset.Capture("12345",new CompanionPreferences());
            addDraft.species=species; addDraft.name="My "+species; draftPresetKey=null;
        }
        void AddDraftPet()
        {
            if(!Ready || PetCount>=DesktopSettings.MaximumPets || addDraft==null) return;
            ReferenceBridge candidate=null;
            try
            {
                addDraft.Validate();
                candidate=context.CreatePet(addDraft.species); candidate.Generate(addDraft.appearanceId);
                candidate.Pose(phase,depth); candidate.Draw();
                var added=DesktopPetConfiguration.From(addDraft); added.Validate();
                extraPets.Add(added);
                if(draftPresetKey!=null) livePresetKeys[added.key]=draftPresetKey;
                var old=selectionPreview; selectionPreview=candidate; candidate=null; old?.Dispose();
                selectedPetKey=added.key; liveId=added.id; liveName=added.name;
                addOpen=false; error=null; RefreshPreview();
            }
            catch(Exception e) { Fail(e); }
            finally { candidate?.Dispose(); }
        }
        void SetPetPanelOpen(bool open)
        {
            if(petPanelOpen==open) return;
            petPanelOpen=open;
            // Preserve the draft, active selection, and scroll positions when hiding the panel.
            if(!Application.isEditor)
                Screen.SetResolution(Mathf.Min(open?920:540,Display.main.systemWidth),Screen.height,FullScreenMode.Windowed);
        }
        static float SpeciesListHeight(int count) => Mathf.Clamp(count*38+8,46,180);
        static bool UseTwoColumns(float width,bool open) => open && width>=840;
        void DrawSpeciesSelector()
        {
            if(GUILayout.Button("Species: "+LizardSpecies.Find(addDraft.species).DisplayName+(speciesSelectorOpen?"  ?":"  ?"),GUILayout.Height(34)))
                speciesSelectorOpen=!speciesSelectorOpen;
            if(!speciesSelectorOpen) return;
            speciesScroll=GUILayout.BeginScrollView(speciesScroll,false,false,GUIStyle.none,GUI.skin.verticalScrollbar,
                GUILayout.Height(SpeciesListHeight(LizardSpecies.AvailableIds.Length)));
            foreach(var species in LizardSpecies.AvailableIds)
                if(GUILayout.Button(LizardSpecies.Find(species).DisplayName,addDraft.species==species?theme.Selected:GUI.skin.button,GUILayout.Height(32)))
                    ChooseDraftSpecies(species);
            GUILayout.EndScrollView();
        }
        void DrawPanel()
        {
            float width=Screen.width, height=Screen.height;
            bool columns=UseTwoColumns(width,petPanelOpen);
            float leftWidth=columns?width-348:width-32, rightX=leftWidth+32, rightWidth=width-rightX-16;
            GUI.Label(new Rect(16,10,220,36),"LIZOR",theme.Title);
            GUI.Label(new Rect(18,44,width-160,22),"A little company on your desktop.",theme.Small);
            GUI.Label(new Rect(width-128,18,112,28),desktop.Active?(trayState.Paused?"PAUSED":"RUNNING"):"STOPPED",theme.Badge);
            var selected=SelectedLivePet();
            GUI.Label(new Rect(18,78,width-190,34),selected==null?"No lizard selected":"Editing: "+selected.name,theme.Heading);
            if(GUI.Button(new Rect(width-170,76,154,34),(petPanelOpen?"Hide lizards":"Lizards")+" �� "+PetCount+" / "+DesktopSettings.MaximumPets,petPanelOpen?theme.Selected:GUI.skin.button))
            { SetPetPanelOpen(!petPanelOpen); GUIUtility.ExitGUI(); }
            float bottom=height-110;
            GUILayout.BeginArea(new Rect(16,122,leftWidth,Mathf.Max(30,bottom-132)));
            panelScroll=GUILayout.BeginScrollView(panelScroll,false,false,GUIStyle.none,GUI.skin.verticalScrollbar);
            if(!string.IsNullOrEmpty(error))
            {
                GUILayout.BeginVertical(theme.Error); GUILayout.Label(error,theme.Warning);
                if(GUILayout.Button("Dismiss")) error=null; GUILayout.EndVertical();
            }
            if(!string.IsNullOrEmpty(preferenceWarning)) GUILayout.Label(preferenceWarning,theme.Small);
            if(!string.IsNullOrEmpty(presetWarning)) GUILayout.Label(presetWarning,theme.Small);
            if(!string.IsNullOrEmpty(trayWarning)) GUILayout.Label(trayWarning,theme.Small);
            if(!Ready) DrawSetup();
            else
            {
                DrawSelectedControls();
                if(petPanelOpen && !columns) { DrawPetRoster(); DrawSavedPresets(); DrawAddPanel(); }
            }
            GUILayout.EndScrollView(); GUILayout.EndArea();
            if(columns)
            {
                GUILayout.BeginArea(new Rect(rightX,122,rightWidth,Mathf.Max(30,bottom-132)));
                petsScroll=GUILayout.BeginScrollView(petsScroll,false,false,GUIStyle.none,GUI.skin.verticalScrollbar);
                DrawPetRoster(); DrawSavedPresets(); DrawAddPanel();
                GUILayout.EndScrollView(); GUILayout.EndArea();
            }
            DrawReleaseFooter(new Rect(16,bottom,width-32,100));
        }
        void DrawSelectedControls()
        {
            var pet=SelectedLivePet();
            if(pet==null)
            {
                GUILayout.BeginVertical(theme.Card); GUILayout.Label("Your desktop is a little quiet.",theme.Heading);
                if(GUILayout.Button("Add a lizard",theme.Primary,GUILayout.Height(36))) { petPanelOpen=true; addOpen=true; }
                GUILayout.EndVertical(); return;
            }
            if(liveId==null) liveId=pet.id;
            if(liveName==null) liveName=pet.name;
            GUILayout.BeginVertical(theme.Card);
            GUILayout.Label(pet.name+" �� "+LizardSpecies.Find(pet.species).DisplayName,theme.Heading);
            var hero=GUILayoutUtility.GetRect(100,124,GUILayout.ExpandWidth(true));
            if(previewTexture!=null) GUI.DrawTexture(hero,previewTexture,ScaleMode.ScaleToFit,false);
            GUILayout.Label("Appearance ID"); GUILayout.BeginHorizontal();
            liveId=GUILayout.TextField(liveId,64,GUILayout.Height(32));
            GUI.enabled=ReferenceBridge.TryParseId(liveId,out _,out _);
            if(GUILayout.Button("Apply ID",theme.Primary,GUILayout.Width(86),GUILayout.Height(32))) ApplyLiveId(pet);
            GUI.enabled=true; GUILayout.EndHorizontal();
            if(!ReferenceBridge.TryParseId(liveId,out _,out _)) GUILayout.Label("Enter a signed 32-bit number, or ID.spawner.number.",theme.Warning);
            else if(!SameAppearanceId(liveId,pet.id)) GUILayout.Label("ID not applied. Apply it to update this lizard.",theme.Warning);
            GUILayout.Label("Size �� "+pet.scale.ToString("0.00")+"��");
            GUILayout.BeginHorizontal();
            string[] labels={"Small","Medium","Large","XL"}; float[] sizes={1,1.2f,1.5f,2};
            for(int i=0;i<sizes.Length;i++)
                if(GUILayout.Button(labels[i],Mathf.Approximately(pet.scale,sizes[i])?theme.Selected:GUI.skin.button)) pet.scale=sizes[i];
            GUILayout.EndHorizontal(); pet.scale=GUILayout.HorizontalSlider(pet.scale,.5f,2);
            ToggleRow("Roam independently",ref pet.roam);
            GUILayout.Label("Climbing"); GUILayout.BeginHorizontal();
            if(GUILayout.Button("Edges only",pet.climbingMode==ClimbingMode.EdgesOnly?theme.Selected:GUI.skin.button)) pet.climbingMode=ClimbingMode.EdgesOnly;
            if(GUILayout.Button("Edges + backgrounds",pet.climbingMode==ClimbingMode.EdgesAndBackgrounds?theme.Selected:GUI.skin.button)) pet.climbingMode=ClimbingMode.EdgesAndBackgrounds;
            GUILayout.EndHorizontal();
            ToggleRow("Explore where I click",ref pet.clickToExplore);
            ToggleRow("Sound",ref pet.sound);
            GUI.enabled=desktop.Active && pet.sound && !trayState.Paused && !muteAll;
            if(GUILayout.Button("Play sample call")) { vocalizePetKey=pet.key; desktopVocalizeRequest++; }
            GUI.enabled=true;
            if(muteAll) GUILayout.Label("All sounds are muted. Individual sound choices are preserved.",theme.Small);
            GUILayout.Label("These controls apply live. Save a preset to keep a copy.",theme.Small);
            ApplyLiveControls(pet);
            GUILayout.EndVertical();
            GUILayout.BeginVertical(theme.Card); GUILayout.Label("All pets",theme.Heading);
            ToggleRow("Mute all sounds",ref muteAll);
            ToggleRow("Allow fighting",ref allowFighting); GUILayout.EndVertical();
            GUILayout.BeginVertical(theme.Card);
            var saved=LivePreset(pet);
            bool matchesSaved=MatchesSavedPreset(pet);
            GUILayout.Label(matchesSaved?"Saved preset �� "+saved.name:"Unsaved changes",matchesSaved?theme.Small:theme.Warning);
            if(Section("Save preset",ref savePresetOpen))
            {
            GUILayout.Label("Preset name"); liveName=GUILayout.TextField(liveName,40,GUILayout.Height(32));
            bool canSave=presets!=null && !string.IsNullOrWhiteSpace(liveName) && SameAppearanceId(liveId,pet.id);
            GUILayout.BeginHorizontal(); GUI.enabled=canSave;
            if(GUILayout.Button("Save as new")) SaveLivePreset(pet,true);
            GUI.enabled=canSave && saved!=null;
            if(GUILayout.Button("Update preset")) SaveLivePreset(pet,false);
            GUI.enabled=true; GUILayout.EndHorizontal();
            if(!SameAppearanceId(liveId,pet.id)) GUILayout.Label("Apply the ID before saving.",theme.Warning);
            if(saved!=null)
            {
                GUI.enabled=presets.Data.selected!=saved.key;
                if(GUILayout.Button(presets.Data.selected==saved.key?"Used on next launch":"Use on next launch")) UseOnNextLaunch(saved.key);
                GUI.enabled=true;
            }
            if(presetFeedback!=null) GUILayout.Label(presetFeedback,theme.Small);
            }
            GUILayout.EndVertical();
            GUILayout.BeginVertical(theme.Card);
            if(Section("Details",ref advancedOpen))
            {
                var shown=selectionPreview??reference;
                GUILayout.Label(shown.Personality.ToString(),theme.Small);
                ToggleRow("Animated preview",ref animate);
                GUILayout.Label("View angle");
                float angle=GUILayout.HorizontalSlider(depth,-.95f,.95f);
                if(angle!=depth) { depth=angle; RefreshPreview(); }
                if(GUILayout.Button("Save comparison")) StartCoroutine(Capture());
            }
            GUILayout.EndVertical();
        }
        void DrawPetRoster()
        {
            GUILayout.BeginVertical(theme.Card);
            GUILayout.Label("Desktop pets �� "+PetCount+" / "+DesktopSettings.MaximumPets,theme.Heading);
            GUILayout.Label("Select one to edit. Extra pets last for this session.",theme.Small);
            if(Ready && primaryPresent) DrawPetRow("primary",primaryName??presets?.Data.Current.name??"My lizard",reference.Species.DisplayName,reference.Identity);
            foreach(var p in extraPets.ToArray()) DrawPetRow(p.key,p.name,LizardSpecies.Find(p.species).DisplayName,p.id);
            if(PetCount==0) GUILayout.Label("No pets added.",theme.Small);
            GUILayout.EndVertical();
        }
        void DrawPetRow(string key,string name,string species,string appearance)
        {
            GUILayout.BeginHorizontal();
            if(GUILayout.Button(name+"\n"+species+" �� "+appearance,key==selectedPetKey?theme.Selected:GUI.skin.button,GUILayout.MinHeight(52))) SelectLivePet(key);
            if(GUILayout.Button("Remove from\ndesktop",GUILayout.Width(106),GUILayout.MinHeight(58))) RemoveActivePet(key);
            GUILayout.EndHorizontal();
        }
        void DrawSavedPresets()
        {
            GUILayout.BeginVertical(theme.Card); GUILayout.Label("Next launch",theme.Heading);
            if(presets==null) GUILayout.Label("Preset storage is unavailable.",theme.Small);
            else
            {
                GUILayout.Label("Choose the saved lizard to load when Lizor opens. Pets still wait for Start.",theme.Small);
                foreach(var preset in presets.Data.pets)
                {
                    bool chosen=presets.Data.selected==preset.key;
                    if(GUILayout.Button((chosen?"?  ":"")+preset.name+"\n"+LizardSpecies.Find(preset.species).DisplayName+" �� "+preset.appearanceId,chosen?theme.Selected:GUI.skin.button,GUILayout.MinHeight(46))) UseOnNextLaunch(preset.key);
                }
            }
            GUILayout.EndVertical();
        }
        void DrawAddPanel()
        {
            GUILayout.BeginVertical(theme.Card);
            if(Section("Add a lizard",ref addOpen))
            {
                if(addDraft==null) ChooseDraftSpecies("cyan");
                DrawSpeciesSelector();
                GUILayout.Space(8); GUILayout.Label("Saved presets",theme.Heading);
                bool any=false;
                if(presets!=null) foreach(var preset in presets.Data.pets)
                {
                    if(preset.species!=addDraft.species) continue; any=true;
                    if(GUILayout.Button(preset.name+" �� "+preset.appearanceId,draftPresetKey==preset.key?theme.Selected:GUI.skin.button))
                    { addDraft=JsonUtility.FromJson<PetPreset>(JsonUtility.ToJson(preset)); draftPresetKey=preset.key; }
                }
                if(!any) GUILayout.Label("No saved presets yet.",theme.Small);
                GUILayout.Space(8); GUILayout.Label("Appearance ID");
                addDraft.appearanceId=GUILayout.TextField(addDraft.appearanceId,64,GUILayout.Height(32));
                if(!ReferenceBridge.TryParseId(addDraft.appearanceId,out _,out _)) GUILayout.Label("Enter a signed 32-bit number, or ID.spawner.number.",theme.Warning);
                GUILayout.Label("Name"); addDraft.name=GUILayout.TextField(addDraft.name,40);
                GUI.enabled=Ready && PetCount<DesktopSettings.MaximumPets && ReferenceBridge.TryParseId(addDraft.appearanceId,out _,out _) && !string.IsNullOrWhiteSpace(addDraft.name);
                if(GUILayout.Button("Add "+LizardSpecies.Find(addDraft.species).DisplayName,theme.Primary,GUILayout.Height(36))) AddDraftPet();
                GUI.enabled=true;
                if(GUILayout.Button("Cancel")) { addOpen=false; speciesSelectorOpen=false; }
                if(PetCount>=DesktopSettings.MaximumPets) GUILayout.Label("Three pets maximum. Remove one to add another.",theme.Small);
            }
            GUILayout.EndVertical();
        }
        void DrawReleaseFooter(Rect area)
        {
            GUILayout.BeginArea(area); GUILayout.BeginHorizontal();
            GUI.enabled=desktop.Active;
            if(GUILayout.Button(trayState.Paused?"Resume all":"Pause all",GUILayout.Height(34))) trayState.TogglePause(desktop.Active);
            GUI.enabled=true;
            if(GUILayout.Button(muteAll?"Unmute all":"Mute all",muteAll?theme.Selected:GUI.skin.button,GUILayout.Height(34))) muteAll=!muteAll;
            GUI.enabled=desktop.Active || (Ready && PetCount>0 && StartBlocker==null);
            if(GUILayout.Button(desktop.Active?"Stop all pets":"Start all pets",theme.Primary,GUILayout.Height(34)))
            {
                if(desktop.Active) desktop.Stop();
                else try { desktop.Start(DesktopConfiguration()); } catch(Exception e) { Fail(e); }
            }
            GUI.enabled=!Application.isEditor;
            if(GUILayout.Button("Quit Lizor",GUILayout.Height(34))) QuitApp();
            GUI.enabled=true; GUILayout.EndHorizontal();
            if(!desktop.Active && StartBlocker!=null) GUILayout.Label(StartBlocker,theme.Small);
            if(presets!=null) GUILayout.Label("Next launch: "+presets.Data.Current.name+(muteAll?"  ��  All sounds muted":""),theme.Small);
            GUILayout.Label("Unofficial Rain World companion �� Videocult / Akupara Games",theme.Small);
            GUILayout.EndArea();
        }
    }
}

