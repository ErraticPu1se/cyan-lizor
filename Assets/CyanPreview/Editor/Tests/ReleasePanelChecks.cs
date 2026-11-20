using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using static CyanPreview.ReferenceContext;

namespace CyanPreview.Editor
{
    public static class ReleasePanelChecks
    {
        static void Check(bool value,string message) { if(!value) throw new Exception(message); }
        public static void Verify()
        { using(var context=new ReferenceContext(Path.GetFullPath("local-data"))) Run(context); }
        public static void Run(ReferenceContext context)
        {
            var root=new GameObject("Release panel checks"); root.SetActive(false);
            var app=root.AddComponent<PreviewApp>();
            string directory=Path.Combine(Path.GetTempPath(),"LizorReleaseChecks-"+Guid.NewGuid().ToString("N"));
            try
            {
                var primary=context.CreatePet("cyan"); primary.Generate("9320");
                Set(app,"context",context); Set(app,"reference",primary); Set(app,"id","9320"); Set(app,"appliedId","9320");
                Set(app,"view",new SpriteMeshView(root.transform));
                var store=new PetPresetStore(Path.Combine(directory,"pets.json"),PetPreset.Capture("9320",new CompanionPreferences()));
                Set(app,"presets",store); string original=File.ReadAllText(Path.Combine(directory,"pets.json"));
                Call(app,"ChooseDraftSpecies","white");
                Check(primary.Species.Id=="cyan" && primary.Seed==9320,"Browsing species changed primary pet");
                var draft=(PetPreset)Get(app,"addDraft"); draft.appearanceId="7205";
                Set(app,"draftPresetKey",store.Data.selected);
                Set(app,"speciesSelectorOpen",true);
                Call(app,"ChooseDraftSpecies","white");
                Check(ReferenceEquals(draft,Get(app,"addDraft")) && draft.appearanceId=="7205","Reselecting species erased draft ID");
                Check(!(bool)Get(app,"speciesSelectorOpen"),"Species choice did not close selector");
                Call(app,"SetPetPanelOpen",true); Call(app,"SetPetPanelOpen",false); Call(app,"SetPetPanelOpen",true);
                Check(ReferenceEquals(draft,Get(app,"addDraft")) && (string)Get(app,"selectedPetKey")=="primary","Collapsing panel changed draft or live selection");
                Check((float)typeof(PreviewApp).GetMethod("SpeciesListHeight",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).Invoke(null,new object[]{2})<180 && (float)typeof(PreviewApp).GetMethod("SpeciesListHeight",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).Invoke(null,new object[]{30})==180,"Species list grows beyond its scroll viewport");
                Call(app,"AddDraftPet");
                var extra=(List<DesktopPetConfiguration>)Get(app,"extraPets");
                Check(extra.Count==1 && extra[0].species=="white","White draft was not added");
                Check(primary.Species.Id=="cyan" && primary.Seed==9320,"Adding White replaced Cyan");
                Call(app,"AddDraftPet");
                Check(extra.Count==2 && extra[0].key!=extra[1].key,"Repeated appearance shares instance identity");
                extra[0].scale=2;
                Check(extra[1].scale!=2 && (float)Get(app,"desktopScale")!=2,"Editing one pet changed another");
                Call(app,"AddDraftPet"); Check(extra.Count==2,"Release pet cap bypassed");
                Call(app,"SelectLivePet",extra[0].key);
                Check((string)Get(app,"selectedPetKey")==extra[0].key,"Pet selection failed");
                Call(app,"RemoveActivePet",extra[0].key);
                Check((string)Get(app,"selectedPetKey")=="primary","Removing selection did not fall back to primary");
                Call(app,"RemoveActivePet","primary");
                Check((string)Get(app,"selectedPetKey")==extra[0].key,"Removing primary did not select remaining pet");
                Call(app,"RemoveActivePet",extra[0].key);
                Check(Get(app,"selectedPetKey")==null,"Empty roster retains removed selection");
                Call(app,"AddDraftPet"); Check(extra.Count==1,"Cannot add after removing last pet");
                Check(File.ReadAllText(Path.Combine(directory,"pets.json"))==original,"Browsing/adding/removing overwrote saved presets");
                var settings=(DesktopSettings)Call(app,"DesktopConfiguration");
                Check(!settings.showAttentionLabels && settings.feedRequest==0 && settings.scuffleRequest==0,"Release configuration enables prototype controls");
                CheckLayout();
                CheckStartup(app,store,directory);
                Set(app,"muteAll",true);
                settings=(DesktopSettings)Call(app,"DesktopConfiguration");
                Check(settings.muteAll && settings.pets.Length==1 && settings.pets[0].key!="primary","Global mute depends on primary being present");
                CheckMute(context,root.transform);
            }
            finally
            {
                Set(app,"context",null); Call(app,"OnDestroy"); UnityEngine.Object.DestroyImmediate(root);
                if(Directory.Exists(directory)) Directory.Delete(directory,true);
            }
            var host=new GameObject("Disabled feeding check"); host.SetActive(false);
            var desktop=host.AddComponent<DesktopApp>();
            Call(desktop,"BeginFeeding");
            Check(((SharedFood)Get(desktop,"food")).Owner==FoodOwner.None,"Legacy feeding entry remains enabled");
            UnityEngine.Object.DestroyImmediate(host);
            Debug.Log("RELEASE_PANEL_CHECKS_PASSED");
        }
        static void CheckLayout()
        {
            var method=typeof(PreviewApp).GetMethod("UseTwoColumns",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
            foreach(int width in new[]{420,540,839,840,920})
                Check((bool)method.Invoke(null,new object[]{(float)width,true})==(width>=840),"Narrow panel did not reflow");
            Check(!(bool)method.Invoke(null,new object[]{920f,false}),"Collapsed panel retains sidebar");
            var ids=typeof(PreviewApp).GetMethod("SameAppearanceId",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
            Check((bool)ids.Invoke(null,new object[]{"9320","ID.-1.9320"}),"Canonical ID is incorrectly shown as an unapplied edit");
            Check(!(bool)ids.Invoke(null,new object[]{"9320","9321"}) && !(bool)ids.Invoke(null,new object[]{"bad","bad"}),"Invalid or different IDs compare as equal");
        }
        static void CheckStartup(PreviewApp app,PetPresetStore store,string directory)
        {
            string originalKey=store.Data.selected;
            ((Dictionary<string,string>)Get(app,"livePresetKeys"))["primary"]=originalKey;
            var next=store.Data.Copy(); var white=PetPreset.Capture("7205",new CompanionPreferences {sound=false});
            white.species="white"; white.name="Next White"; next.pets.Add(white); store.Commit(next);
            var reference=Get(app,"reference"); var selection=Get(app,"selectedPetKey");
            Call(app,"UseOnNextLaunch",white.key);
            Check(store.Data.selected==white.key && ReferenceEquals(reference,Get(app,"reference")) && Equals(selection,Get(app,"selectedPetKey")),"Startup selection changed a live pet");
            var reloaded=new PetPresetStore(Path.Combine(directory,"pets.json"),PetPreset.Capture("1",new CompanionPreferences()));
            Check(reloaded.Data.Current.species=="white" && reloaded.Data.Current.appearanceId=="7205" && !reloaded.Data.Current.sound,"Next-launch choice did not survive reload");
            Set(app,"liveName","Updated live Cyan");
            var primary=DesktopPetConfiguration.From(new DesktopSettings {id="9320",scale=1.5f},"primary");
            Call(app,"SaveLivePreset",primary,false);
            Check(store.Data.selected==white.key && store.Data.Current.name=="Next White" && store.Data.Current.species=="white","Updating live Cyan overwrote the startup White");
            Check(store.Data.pets.Find(p=>p.key==originalKey).name=="Updated live Cyan","Live pet lost its original preset mapping");
            string before=File.ReadAllText(Path.Combine(directory,"pets.json"));
            Call(app,"UseOnNextLaunch","missing");
            Check(store.Data.selected==white.key && File.ReadAllText(Path.Combine(directory,"pets.json"))==before,"Invalid startup selection changed saved data");
        }
        static void CheckMute(ReferenceContext context,Transform parent)
        {
            var bounds=new Rect(-400,-300,800,600);
            var audible=DesktopPetConfiguration.From(new DesktopSettings {id="9320"},"audible");
            var silent=DesktopPetConfiguration.From(new DesktopSettings {id="7205",species="white",sound=false},"silent");
            using(var a=new DesktopPet(context,parent,audible,bounds,0))
            using(var b=new DesktopPet(context,parent,silent,bounds,1))
            {
                Check(a.Audio!=null && b.Audio!=null,"Mute regression requires actual audio instances");
                a.Configure(audible,false,true); b.Configure(silent,false,true);
                Check(!a.Audio.Enabled && !b.Audio.Enabled && audible.sound && !silent.sound,"Mute changed per-pet choices or left audio enabled");
                a.Configure(audible,false,false); b.Configure(silent,false,false);
                Check(a.Audio.Enabled && !b.Audio.Enabled,"Unmute did not restore individual sound choices");
                a.Configure(audible,true,false); Check(!a.Audio.Enabled,"Unmute bypassed pause");
            }
            var prefs=CompanionPreferences.Read(JsonUtility.ToJson(new CompanionPreferences {sound=true,muteAll=true}),out var warning);
            Check(warning==null && prefs.muteAll && prefs.sound,"Global mute preference did not round-trip independently");
            Check(!CompanionPreferences.Read("{\"sound\":false}",out _).muteAll,"Old preferences unexpectedly mute all pets");
            Debug.Log("MASTER_MUTE_AND_STARTUP_PRESET_CHECKS_PASSED");
        }
    }
}

