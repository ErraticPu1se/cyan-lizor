using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using static CyanPreview.ReferenceContext;
namespace CyanPreview.Editor
{
    public static class PickupDiagnosticsChecks
    {
        static void Check(bool condition,string message) { if(!condition) throw new Exception(message); }
        public static void Run(ReferenceContext context)
        {
            var root=new GameObject("Diagnostic observation checks");
            string directory=Path.Combine(Path.GetTempPath(),"lizor-diagnostic-test-"+Guid.NewGuid().ToString("N"));
            var pets=new List<DesktopPet>(); PickupDiagnostics diagnostic=null;
            try
            {
                for(int i=0;i<2;i++) pets.Add(new DesktopPet(context,root.transform,
                    DesktopPetConfiguration.From(new DesktopSettings {id="9320",sound=false,roam=false,jumps=false},"test-"+i),new Rect(-600,-400,1200,800),i));
                var a=pets[0]; var b=pets[1];
                a.Motion.RivalTarget=a.Local(b.Motion.BodyPosition(2)*b.Config.scale);
                a.Motion.Behavior.TaskTarget=a.Motion.RivalTarget; a.Motion.Behavior.HoldPosition=true;
                var position=a.Motion.Position; float jaw=a.Bite.Jaw; var target=a.Motion.Behavior.TaskTarget;
                var snapshot=PickupDiagnostics.Read(a,pets,a.WorldPosition);
                Check(snapshot.targetChunk==2 && snapshot.rival==b.Config.key,"Trace did not identify the target body chunk");
                diagnostic=new PickupDiagnostics(root.transform,directory);
                var food=new SharedFood(); food.Begin(); food.Place(Vector2.zero);
                diagnostic.BeginFrame(.1f,pets); diagnostic.Record("before-contacts",pets,food);
                food.Take(b.Config.key); diagnostic.Pickup(pets,food); diagnostic.Record("after-motion",pets,food);
                Check(File.Exists(diagnostic.TracePath),"Pickup did not automatically create trace");
                string trace; using(var reader=new StreamReader(new FileStream(diagnostic.TracePath,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))) trace=reader.ReadToEnd();
                Check(trace.Contains("before-contacts") && trace.Contains("pickup") && trace.Contains("after-motion") && trace.Contains("test-1"),"Pickup trace lost history or pet identity");
                Check(root.GetComponentsInChildren<TextMesh>().Length==2,"Missing per-pet labels");
                Check(a.Motion.Position==position && a.Bite.Jaw==jaw && a.Motion.Behavior.TaskTarget==target && a.Motion.Behavior.HoldPosition && food.Holder==b.Config.key,"Diagnostics mutated simulation state");
                Set(diagnostic,"lines",PickupDiagnostics.MaximumLines);
                long length=new FileInfo(diagnostic.TracePath).Length;
                diagnostic.Record("after-motion",pets,food);
                Check(new FileInfo(diagnostic.TracePath).Length==length,"Diagnostic trace exceeded its cap");
                diagnostic.Dispose(); diagnostic.Dispose(); diagnostic.Record("after-motion",pets,food);
                Check(a.Motion.Position==position,"Disposing diagnostics moved the pet");
                Debug.Log("PICKUP_DIAGNOSTICS_READONLY_HISTORY_LABELS_CAP_CHECK_PASSED");
            }
            finally
            {
                diagnostic?.Dispose(); foreach(var p in pets) p.Dispose(); UnityEngine.Object.DestroyImmediate(root);
                if(Directory.Exists(directory)) Directory.Delete(directory,true);
            }
        }
    }
}
