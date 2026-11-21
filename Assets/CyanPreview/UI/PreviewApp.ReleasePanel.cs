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
        void DrawSpeciesSelector()
        {
            if(GUILayout.Button("Species: "+LizardSpecies.Find(addDraft.species).DisplayName+(speciesSelectorOpen?"  ▴":"  ▾"),GUILayout.Height(34)))
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
            // Scale the two-column layout together on smaller laptop windows.
            float scale=Mathf.Min(1,Screen.width/(petPanelOpen?820f:540f));
            var oldMatrix=GUI.matrix; GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));
            try
            {
                float width=Screen.width/scale, height=Screen.height/scale;
                float leftWidth=petPanelOpen?508:width-32, rightX=leftWidth+32, rightWidth=width-rightX-16;
                GUI.Label(new Rect(16,14,250,36),"LIZOR",theme.Title);
                GUI.Label(new Rect(width-122,20,106,26),desktop.Active?(trayState.Paused?"PAUSED":"RUNNING"):"STOPPED",theme.Badge);
                if(GUI.Button(new Rect(16,55,leftWidth,32),"Lizards · "+PetCount+" / "+DesktopSettings.MaximumPets+(petPanelOpen?"  ‹ Collapse":"  ›"),petPanelOpen?theme.Selected:GUI.skin.button))
                { SetPetPanelOpen(!petPanelOpen); GUIUtility.ExitGUI(); }
                float bottom=height-104;
                GUILayout.BeginArea(new Rect(16,96,leftWidth,bottom-104));
                panelScroll=GUILayout.BeginScrollView(panelScroll,false,false,GUIStyle.none,GUI.skin.verticalScrollbar);
                if(!string.IsNullOrEmpty(error))
                {
                    GUILayout.BeginVertical(theme.Error); GUILayout.Label(error,theme.Small);
                    if(GUILayout.Button("Dismiss")) error=null; GUILayout.EndVertical();
                }
                if(!string.IsNullOrEmpty(preferenceWarning)) GUILayout.Label(preferenceWarning,theme.Small);
                if(!string.IsNullOrEmpty(presetWarning)) GUILayout.Label(presetWarning,theme.Small);
                if(!string.IsNullOrEmpty(trayWarning)) GUILayout.Label(trayWarning,theme.Small);
                if(!Ready) DrawSetup();
                else DrawSelectedControls();
                GUILayout.EndScrollView(); GUILayout.EndArea();
                if(petPanelOpen)
                {
                GUILayout.BeginArea(new Rect(rightX,96,rightWidth,bottom-104));
                petsScroll=GUILayout.BeginScrollView(petsScroll,false,false,GUIStyle.none,GUI.skin.verticalScrollbar);
                DrawPetRoster(); DrawAddPanel();
                GUILayout.EndScrollView(); GUILayout.EndArea();
                }
                DrawReleaseFooter(new Rect(16,bottom,width-32,100));
            }
            finally { GUI.matrix=oldMatrix; }
        }
        void DrawSelectedControls()
        {
            var pet=SelectedLivePet();
            if(pet==null) { GUILayout.Label("Choose or add a lizard."); return; }
            if(liveId==null) liveId=pet.id;
            if(liveName==null) liveName=pet.name;
            GUILayout.BeginVertical(theme.Card);
            GUILayout.Label(pet.name+" · "+LizardSpecies.Find(pet.species).DisplayName,theme.Heading);
            var hero=GUILayoutUtility.GetRect(100,Mathf.Clamp(Screen.height*.28f,180,340),GUILayout.ExpandWidth(true));
            if(previewTexture!=null) GUI.DrawTexture(hero,previewTexture,ScaleMode.ScaleToFit,false);
            GUILayout.BeginHorizontal(); GUILayout.Label("Preview zoom: "+previewZoom.ToString("0.00")+"x");
            if(GUILayout.Button("Reset",GUILayout.Width(64))) SetPreviewZoom(1.5f);
            GUILayout.EndHorizontal();
            float zoom=GUILayout.HorizontalSlider(previewZoom,.5f,3);
            if(zoom!=previewZoom) SetPreviewZoom(zoom);
            GUILayout.Label("Appearance ID"); GUILayout.BeginHorizontal();
            liveId=GUILayout.TextField(liveId,64,GUILayout.Height(32));
            GUI.enabled=ReferenceBridge.TryParseId(liveId,out _,out _);
            if(GUILayout.Button("Apply ID",theme.Primary,GUILayout.Width(86),GUILayout.Height(32))) ApplyLiveId(pet);
            GUI.enabled=true; GUILayout.EndHorizontal();
            GUILayout.Label("Size · "+pet.scale.ToString("0.00")+"×");
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
            ApplyLiveControls(pet);
            GUILayout.EndVertical();
            GUILayout.BeginVertical(theme.Card); GUILayout.Label("All pets",theme.Heading);
            ToggleRow("Mute all sounds",ref muteAll);
            ToggleRow("Allow fighting",ref allowFighting); GUILayout.EndVertical();
            GUILayout.BeginVertical(theme.Card);
            if(Section("Save preset",ref savePresetOpen))
            {
            liveName=GUILayout.TextField(liveName,40);
            GUILayout.BeginHorizontal(); GUI.enabled=presets!=null;
            if(GUILayout.Button("Save as new")) SaveLivePreset(pet,true);
            var saved=LivePreset(pet);
            bool hasPreset=saved!=null;
            GUI.enabled=presets!=null && hasPreset;
            if(GUILayout.Button("Update preset")) SaveLivePreset(pet,false);
            GUI.enabled=true; GUILayout.EndHorizontal();
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
            if(Section("Advanced",ref advancedOpen))
            {
                var shown=selectionPreview??reference;
                GUILayout.Label("Body specifications and cosmetics",theme.Heading);
                GUILayout.Label(shown.Summary??"",theme.Small);
                GUILayout.Label("Personality from ID",theme.Small);
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
            GUILayout.Label("Desktop pets · "+PetCount+" / "+DesktopSettings.MaximumPets,theme.Heading);
            if(Ready && primaryPresent) DrawPetRow("primary",primaryName??presets?.Data.Current.name??"My lizard",reference.Species.DisplayName,reference.Identity);
            foreach(var p in extraPets.ToArray()) DrawPetRow(p.key,p.name,LizardSpecies.Find(p.species).DisplayName,p.id);
            if(PetCount==0) GUILayout.Label("No pets added.",theme.Small);
            GUILayout.EndVertical();
        }
        void DrawPetRow(string key,string name,string species,string appearance)
        {
            GUILayout.BeginHorizontal();
            if(GUILayout.Button(name+"\n"+species+" · "+appearance,key==selectedPetKey?theme.Selected:GUI.skin.button,GUILayout.MinHeight(52))) SelectLivePet(key);
            if(GUILayout.Button("Remove",GUILayout.Width(68),GUILayout.MinHeight(52))) RemoveActivePet(key);
            GUILayout.EndHorizontal();
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
                    if(GUILayout.Button(preset.name+" · "+preset.appearanceId,draftPresetKey==preset.key?theme.Selected:GUI.skin.button))
                    { addDraft=JsonUtility.FromJson<PetPreset>(JsonUtility.ToJson(preset)); draftPresetKey=preset.key; }
                    GUI.enabled=presets.Data.selected!=preset.key;
                    if(GUILayout.Button(presets.Data.selected==preset.key?"Used on next launch":"Use on next launch")) UseOnNextLaunch(preset.key);
                    GUI.enabled=true;
                }
                if(!any) GUILayout.Label("No saved presets yet.",theme.Small);
                GUILayout.Space(8); GUILayout.Label("Appearance ID");
                addDraft.appearanceId=GUILayout.TextField(addDraft.appearanceId,64,GUILayout.Height(32));
                GUILayout.Label("Name"); addDraft.name=GUILayout.TextField(addDraft.name,40);
                GUI.enabled=Ready && PetCount<DesktopSettings.MaximumPets && ReferenceBridge.TryParseId(addDraft.appearanceId,out _,out _) && !string.IsNullOrWhiteSpace(addDraft.name);
                if(GUILayout.Button("Add "+LizardSpecies.Find(addDraft.species).DisplayName,theme.Primary,GUILayout.Height(36))) AddDraftPet();
                GUI.enabled=true;
                if(PetCount>=DesktopSettings.MaximumPets) GUILayout.Label("Three pets maximum. Remove one to add another.",theme.Small);
            }
            GUILayout.EndVertical();
        }
        void DrawReleaseFooter(Rect area)
        {
            GUILayout.BeginArea(area); GUILayout.BeginHorizontal();
            GUI.enabled=desktop.Active;
            if(GUILayout.Button(trayState.Paused?"Resume all":"Pause all",GUILayout.Height(34))) trayState.TogglePause(desktop.Active);
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
            GUILayout.Label("An unofficial lizor desktop pet app. Rain World belongs to Videocult and Akupara Games.",theme.Small);
            GUILayout.EndArea();
        }
    }
}

