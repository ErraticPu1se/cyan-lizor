using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace CyanPreview.Editor
{
    public static class TrayChecks
    {
        static void Check(bool value,string message) { if(!value) throw new Exception(message); }
        public static void Run()
        {
            var state=new TrayState();
            state.TogglePause(false); Check(!state.Paused,"Stopped pet can be paused");
            state.TogglePause(true); Check(state.Paused,"Pause did not activate");
            state.Sync(true); Check(state.Paused,"Active paused pet resumed unexpectedly");
            state.TogglePause(true); Check(!state.Paused,"Resume failed");
            state.TogglePause(true); state.Sync(false); Check(!state.Paused,"Stopping retained pause for next pet");
            state.TogglePause(true); state.Quit(); state.TogglePause(true);
            Check(state.Quitting && !state.Paused,"Quit did not clear transient pause");
            Check(TrayState.HideOnClose(true,false,false),"Healthy close-to-tray rejected");
            Check(!TrayState.HideOnClose(false,false,false),"Unavailable icon stranded hidden app");
            Check(!TrayState.HideOnClose(true,true,false) && !TrayState.HideOnClose(true,false,true),"Quit or shutdown was hidden");
            string name=@"Local\Lizor.Test."+Guid.NewGuid().ToString("N");
            using(var first=new SingleInstance(name))
            using(var duplicate=new SingleInstance(name))
            {
                Check(first.IsPrimary && !duplicate.IsPrimary,"Duplicate controller acquired singleton");
                duplicate.Dispose(); duplicate.Dispose(); Check(first.IsPrimary,"Duplicate disposed primary ownership");
            }
            using(var recovered=new SingleInstance(name)) Check(recovered.IsPrimary,"Singleton did not recover after quit");
            var legacy=new Dictionary<string,string> {
                {"LastId","ID.7.9320"},{"ReferenceCache",@"D:\Rain World\local-data"},
                {"CompanionPreferences.v1","{\"version\":1,\"scale\":1.5,\"sound\":false,\"roam\":false,\"jumps\":false,\"clickToExplore\":true,\"climbingMode\":0}"}
            };
            var saved=new Dictionary<string,string>();
            PreferenceMigration.Apply(saved.ContainsKey,(k,v)=>saved[k]=v,k=>legacy[k]);
            foreach(var entry in legacy) Check(saved[entry.Key]==entry.Value,"Legacy settings changed during rename");
            saved["LastId"]="42";
            PreferenceMigration.Apply(saved.ContainsKey,(k,v)=>saved[k]=v,k=>throw new Exception("Migration ran twice"));
            Check(saved["LastId"]=="42","Existing new preference overwritten");
            saved.Clear(); saved["LastId"]="73";
            PreferenceMigration.Apply(saved.ContainsKey,(k,v)=>saved[k]=v,k=>legacy[k]);
            Check(saved["LastId"]=="73" && saved["ReferenceCache"]==legacy["ReferenceCache"],"Partial migration clobbered a newer preference");
            saved.Clear();
            try { PreferenceMigration.Apply(saved.ContainsKey,(k,v)=>saved[k]=v,k=>throw new IOException("Unavailable registry")); } catch(IOException) { }
            Check(!saved.ContainsKey(PreferenceMigration.Marker),"Failed migration was marked complete");
            PreferenceMigration.Apply(saved.ContainsKey,(k,v)=>saved[k]=v,k=>null);
            Check(saved.Count==1 && saved.ContainsKey(PreferenceMigration.Marker),"Fresh install invented preferences");
            var preferences=CompanionPreferences.Read(legacy["CompanionPreferences.v1"],out _);
            Check(preferences.scale==1.5f && !preferences.sound && !preferences.roam && !preferences.jumps && preferences.clickToExplore && preferences.climbingMode==ClimbingMode.EdgesOnly,"Migration lost individual settings");
            Check(!JsonUtility.ToJson(preferences).Contains("paused"),"Transient pause was saved between launches");
            var oldSettings=JsonUtility.FromJson<DesktopSettings>("{\"schema\":2,\"running\":true}");
            Check(!oldSettings.paused && oldSettings.running,"Old IPC settings no longer default to unpaused");
            foreach(int size in new[]{16,20,24,32,48})
            {
                byte[] pixels=TrayGlyph.Pixels(size); int opaque=0, transparent=0, cyan=0;
                for(int i=0;i<pixels.Length;i+=4) { if(pixels[i+3]==0) transparent++; else opaque++; if(pixels[i]==216 && pixels[i+1]==230 && pixels[i+2]==64) cyan++; }
                Check(pixels.Length==size*size*4 && opaque>0 && transparent>0 && cyan>0,"Liz tray glyph lost text or transparency");
                if(size==32)
                {
                    var image=new Texture2D(size,size,TextureFormat.RGBA32,false);
                    var colors=new Color32[size*size];
                    for(int y=0;y<size;y++) for(int x=0;x<size;x++) { int i=(y*size+x)*4; colors[(size-1-y)*size+x]=new Color32(pixels[i+2],pixels[i+1],pixels[i],pixels[i+3]); }
                    image.SetPixels32(colors); image.Apply(); Directory.CreateDirectory("Builds/Checks"); File.WriteAllBytes("Builds/Checks/liz-tray.png",image.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(image);
                }
            }
            bool invalid=false; try { TrayGlyph.Pixels(0); } catch(ArgumentOutOfRangeException) { invalid=true; }
            Check(invalid,"Invalid tray glyph size accepted");
            Debug.Log("TRAY_LIFECYCLE_MIGRATION_SINGLE_INSTANCE_CHECKS_PASSED");
        }
    }
}
