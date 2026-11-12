using System;
using System.Collections.Generic;
using UnityEngine;
using static CyanPreview.ReferenceContext;
namespace CyanPreview.Editor
{
    public static class DevelopmentInputChecks
    {
        static void Check(bool ok,string message) { if(!ok) throw new Exception(message); }
        public static void Run(ReferenceContext context)
        {
            var root=new GameObject("Multi-pet pointer and labels");
            var pets=new List<DesktopPet>();
            var area=new Rect(-1920,-200,1920,1080); var bounds=new Rect(-960,-540,1920,1080);
            var food=new SharedFood();
            try
            {
                for(int i=0;i<3;i++)
                {
                    var config=DesktopPetConfiguration.From(new DesktopSettings {id="9320",sound=false,roam=false,jumps=false,scale=1+i*.5f},"same-id-"+i);
                    var pet=new DesktopPet(context,root.transform,config,bounds,i); pets.Add(pet);
                    pet.Motion.ShiftWithSurface(pet.Local(new Vector2(-400+i*400,0))-pet.Motion.Position);
                }
                Func<Vector2,Vector2> screen=v=>new Vector2(area.center.x+v.x,area.center.y-v.y);
                var hit=DesktopPointerSnapshot.Capture(pets,food,area,false);
                foreach(var pet in pets)
                {
                    var point=screen(pet.WorldPosition); Check(hit.Contains(point.x,point.y),"Different sized duplicate-ID pet missing from snapshot");
                }
                var old=screen(pets[2].WorldPosition); var removed=pets[2]; pets.RemoveAt(2);
                hit=DesktopPointerSnapshot.Capture(pets,food,area,false);
                Check(!hit.Contains(old.x,old.y),"Removed pet still captures clicks"); removed.Dispose();
                var first=screen(pets[0].WorldPosition);
                Check(!DesktopPointerSnapshot.Capture(pets,food,area,true).Contains(first.x,first.y),"Paused pet captures clicks");
                Set(pets[0].Feeding,"Stage",FeedingStage.Eating);
                Check(!DesktopPointerSnapshot.Capture(pets,food,area,false).Contains(first.x,first.y),"Hidden den pet captures clicks");
                pets[0].Feeding.Cancel();
                var foodPoint=new Vector2(300,250); var fs=screen(foodPoint);
                food.Begin(); hit=DesktopPointerSnapshot.Capture(pets,food,area,false);
                Check(hit.Contains(fs.x,fs.y) && !hit.Contains(area.xMin-1,fs.y),"Food placement failed to capture only the work area");
                food.Place(foodPoint); hit=DesktopPointerSnapshot.Capture(pets,food,area,false);
                Check(hit.Contains(fs.x,fs.y) && !hit.Contains(fs.x+19,fs.y),"Loose food capture radius changed");
                food.Take(pets[1].Config.key); hit=DesktopPointerSnapshot.Capture(pets,food,area,false);
                Check(!hit.Contains(fs.x,fs.y),"Carried food retains a separate click target");
                food.Cancel();
                var a=pets[0]; var b=pets[1];
                a.Motion.Behavior.Social.Notice(b.Config.key,a.Local(b.WorldPosition),1,55,b.Config.name);
                a.Motion.Advance(.025f);
                string source=a.Motion.Behavior.Social.Source; float utility=a.Motion.Behavior.Social.Utility; var position=a.WorldPosition;
                using(var labels=new PickupDiagnostics(root.transform,System.IO.Path.GetTempPath(),true))
                {
                    labels.Pickup(pets,food); Check(labels.TracePath==null,"Attention labels unexpectedly created pickup traces");
                    labels.BeginFrame(.1f,pets); labels.Record("after-motion",pets,food);
                    Check(root.GetComponentsInChildren<TextMesh>().Length>0,"Social label fixture did not show attention");
                    labels.LabelsVisible=false;
                    Check(root.GetComponentsInChildren<TextMesh>().Length==0,"Hide toggle left labels visible");
                    for(int i=0;i<10;i++) { labels.BeginFrame(.1f,pets); labels.Record("after-motion",pets,food); }
                    Check(root.GetComponentsInChildren<TextMesh>().Length==0,"Sampling resurrected hidden labels");
                    labels.LabelsVisible=true; labels.BeginFrame(.1f,pets); labels.Record("after-motion",pets,food);
                    Check(root.GetComponentsInChildren<TextMesh>().Length>0,"Show toggle did not restore labels");
                    Check(a.Motion.Behavior.Social.Source==source && a.Motion.Behavior.Social.Utility==utility && a.WorldPosition==position,"Label visibility altered social decisions or movement");
                    labels.LabelsVisible=false; a.Motion.Behavior.Social.Clear(); a.Motion.Advance(.025f);
                    labels.LabelsVisible=true; labels.BeginFrame(.1f,pets); labels.Record("after-motion",pets,food);
                    Check(root.GetComponentsInChildren<TextMesh>().Length==0,"Expired attention resurfaced after toggling");
                }
                var settings=new DesktopSettings {showAttentionLabels=false};
                Check(!JsonUtility.FromJson<DesktopSettings>(JsonUtility.ToJson(settings)).showAttentionLabels,"Hidden preference lost in controller IPC");
                var legacy=new DesktopSettings(); JsonUtility.FromJsonOverwrite("{}",legacy);
                Check(legacy.showAttentionLabels,"Missing legacy setting unexpectedly hides labels");
                Debug.Log("MULTI_PET_POINTER_FOOD_PAUSE_REMOVAL_LABEL_VISIBILITY_READONLY_CHECK_PASSED");
            }
            finally { foreach(var pet in pets) pet.Dispose(); UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
