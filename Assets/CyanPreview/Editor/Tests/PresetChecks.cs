using System;
using System.IO;
using UnityEngine;
namespace CyanPreview.Editor
{
    public static class PresetChecks
    {
        static void Check(bool condition,string message) { if(!condition) throw new Exception(message); }
        static void Reject(Action action,string message) { bool rejected=false; try { action(); } catch { rejected=true; } Check(rejected,message); }
        public static void Run()
        {
            string dir=Path.Combine(Path.GetTempPath(),"LizorPresetTests-"+Guid.NewGuid().ToString("N"));
            string file=Path.Combine(dir,"pets.json");
            try
            {
                var preferences=new CompanionPreferences {scale=1.5f,sound=false,roam=false,jumps=false,clickToExplore=true,climbingMode=ClimbingMode.EdgesOnly,gameFolder="private path"};
                var legacy=PetPreset.Capture("ID.7.9320",preferences);
                var store=new PetPresetStore(file,legacy); string original=File.ReadAllText(file);
                Check(store.Data.Current.appearanceId==legacy.appearanceId && store.Data.Current.scale==1.5f && !store.Data.Current.sound && !store.Data.Current.roam && !store.Data.Current.jumps && store.Data.Current.clickToExplore,"Migration lost user settings");
                Check(!original.Contains("private path") && !original.Contains("paused"),"Preset contains app or transient state");
                var repeated=new PetPresetStore(file,PetPreset.Capture("123",new CompanionPreferences()));
                Check(File.ReadAllText(file)==original,"Migration ran twice");
                store.Duplicate("Twin"); Check(store.Data.pets.Count==2 && store.Data.pets[0].key!=store.Data.pets[1].key && store.Data.pets[0].appearanceId==store.Data.pets[1].appearanceId,"Duplicate appearance shares identity");
                store.Rename("Second"); Check(store.Data.pets[0].name=="My cyan","Rename changed another pet");
                Reject(()=>store.Rename("  "),"Empty name accepted");
                var invalid=store.Data.Copy(); invalid.Current.species="white"; Reject(()=>store.Commit(invalid),"Unsupported species accepted");
                invalid=store.Data.Copy(); invalid.Current.scale=float.NaN; Reject(()=>store.Commit(invalid),"Nonfinite size accepted");
                invalid=store.Data.Copy(); invalid.Current.appearanceId="bad"; Reject(()=>store.Commit(invalid),"Invalid appearance accepted");
                invalid=store.Data.Copy(); invalid.selected="missing"; Reject(()=>store.Commit(invalid),"Missing selection accepted");
                invalid=store.Data.Copy(); invalid.pets[1].key=invalid.pets[0].key; Reject(()=>store.Commit(invalid),"Duplicate preset key accepted");
                File.WriteAllText(file+".tmp","interrupted write");
                Check(new PetPresetStore(file,legacy).Data.Current.name=="Second","Interrupted save replaced good file");
                string backup=File.ReadAllText(file+".bak"); File.WriteAllText(file,"broken");
                var recovered=new PetPresetStore(file,legacy); Check(File.ReadAllText(file)==backup && recovered.Warning!=null && Directory.GetFiles(dir,"*.damaged-*").Length==1,"Backup recovery lost damaged data");
                recovered.Commit(recovered.WithoutCurrent()); Reject(()=>recovered.WithoutCurrent(),"Last preset deleted");
                File.WriteAllText(file,"bad"); File.WriteAllText(file+".bak","bad backup");
                Reject(()=>new PetPresetStore(file,legacy),"Corrupt store silently reset"); Check(File.ReadAllText(file)=="bad","Corrupt store overwritten");
                Debug.Log("SAVED_PET_PRESET_EDGE_CASES_PASSED");
            }
            finally { if(Directory.Exists(dir)) Directory.Delete(dir,true); }
        }
    }
}
