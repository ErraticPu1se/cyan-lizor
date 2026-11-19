using System;
using UnityEngine;
using static CyanPreview.ReferenceContext;
namespace CyanPreview.Editor
{
    public static class CamouflageSelectionChecks
    {
        static void Check(bool ok,string reason) { if(!ok) throw new Exception(reason); }
        public static void Run(ReferenceContext context)
        {
            var c=new PetCamouflage(); int requests=0;
            for(int i=0;i<400;i++) if(c.Advance(.025f,true,new Vector2(Mathf.Sin(i)*.4f,0))) { requests++; c.Accept(Color.red); }
            Check(requests==1 && c.Amount>.99f,"Idle camouflage resampled or did not blend");
            c.Advance(.1f,false,Vector2.zero); Check(c.Amount<1,"Active pet stayed fully camouflaged");
            for(int i=0;i<80;i++) if(c.Advance(.025f,true,Vector2.zero)) { requests++; c.Accept(Color.blue); }
            Check(requests==2 && c.Color.b>c.Color.r,"New idle episode did not resample");
            c.Advance(.1f,true,new Vector2(20,0));
            for(int i=0;i<80;i++) if(c.Advance(.025f,true,new Vector2(20,0))) requests++;
            Check(requests==3,"Window movement did not rearm sample");
            var failed=new PetCamouflage(); requests=0;
            for(int i=0;i<500;i++) if(failed.Advance(.025f,true,Vector2.zero)) requests++;
            Check(requests==1 && failed.Amount==0,"Capture failure retried continuously or concealed pet");
            var invalid=new PetCamouflage(); Check(!invalid.Advance(float.NaN,true,Vector2.zero) && !invalid.Advance(0,true,Vector2.zero),"Invalid time progressed camouflage");
            Check(PetCamouflage.Combine(Color.red,Color.blue,Color.red)==Color.red,"Majority background color changed");
            Check(PetCamouflage.Combine(Color.red,Color.green,Color.blue)==(Color.red+Color.green+Color.blue)/3,"Three-color average changed");
            Check(DesktopBackgroundSampler.ScreenPoint(new Vector2(30,20),new Rect(-100,50,800,600))==new Vector2(330,330),"Screen coordinates lost offset or Y inversion");
            var original=PetPreset.Capture("9320",new CompanionPreferences());
            var collection=new PetPresetCollection {selected=original.key}; collection.pets.Add(original);
            var before=JsonUtility.ToJson(collection);
            var next=PetSelection.Create(collection,"white","9320",new CompanionPreferences());
            Check(JsonUtility.ToJson(collection)==before && next.pets.Count==2 && next.Current.species=="white" && next.Current.key!=original.key,"Species selection overwrote original");
            var duplicate=PetSelection.Create(next,"white","9320",new CompanionPreferences());
            Check(duplicate.Current.key!=next.Current.key,"Duplicate appearance reused runtime identity");
            bool rejected=false; try { PetSelection.Create(collection,"white","bad",new CompanionPreferences()); } catch(System.IO.InvalidDataException) { rejected=true; }
            Check(rejected && JsonUtility.ToJson(collection)==before,"Invalid ID mutated presets");
            using(var white=context.CreatePet("white")) using(var cyan=context.CreatePet())
            {
                white.Generate("9320"); cyan.Generate("9320");
                white.ApplyCamouflage(Color.red,1); white.Draw();
                Check((Color)Get(white.Graphics,"whiteCamoColor")==Color.red,"Reference adapter lost sampled color");
                Check((float)Get(white.Graphics,"whiteCamoColorAmount")==1,"Reference adapter lost blend");
                white.ApplyCamouflage(Color.blue,0); white.Draw();
                Check((float)Get(white.Graphics,"whiteCamoColorAmount")==0,"Reveal failed");
                cyan.ApplyCamouflage(Color.red,1); cyan.Draw();
                Check(!cyan.Species.CanCamouflage,"Cyan gained camouflage");
            }
            Debug.Log("CAMOUFLAGE_IDLE_ONESHOT_FAILURE_REARM_COLOR_SELECTOR_PRESERVATION_CHECK_PASSED");
        }
    }
}
