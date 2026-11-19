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
    }
}
