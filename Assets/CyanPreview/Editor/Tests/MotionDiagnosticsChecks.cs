using System;
using System.IO;
using UnityEngine;
namespace CyanPreview.Editor
{
    public static class MotionDiagnosticsChecks
    {
        static void Check(bool value,string message) { if(!value) throw new Exception(message); }
        public static void InvestigateStationaryWindow()
        {
            var surfaces=new DesktopSurfaces(new Rect(-800,-450,1600,900)) {Mode=ClimbingMode.EdgesAndBackgrounds};
            var windows=new[]{new DesktopWindow(17,new Rect(-600.25f,-256.666656f,747.58356f,650.8333f))};
            surfaces.UpdateWindows(windows);
            int falseMoves=0; float largest=0;
            for(int i=0;i<1000;i++)
            {
                var point=new Vector2(windows[0].Bounds.xMax-14,-240+i*.6f);
                var delta=surfaces.UpdateWindows(windows,point,true);
                if(delta.sqrMagnitude>0) falseMoves++;
                largest=Mathf.Max(largest,delta.magnitude);
            }
            Debug.Log("STATIONARY_WINDOW_PROBE identicalBounds=1000 falseMoves="+falseMoves+" largest="+largest.ToString("R"));
        }
        public static void Run(ReferenceContext context)
        {
            string directory=Path.Combine(Path.GetTempPath(),"lizor-motion-check-"+Guid.NewGuid().ToString("N"));
            var random=UnityEngine.Random.state;
            try
            {
                using(var first=context.CreatePet()) using(var second=context.CreatePet())
                {
                    first.Generate("9320"); second.Generate("9320");
                    var bounds=new Rect(-700,-400,1400,800);
                    var a=new PetMotion(first,new FrameSurfaces(bounds),42) {Roam=false,AutonomousJumps=false};
                    var b=new PetMotion(second,new FrameSurfaces(bounds),42) {Roam=false,AutonomousJumps=false};
                    int ticks=0; PetMotion.MotionSample last=null;
                    using(var trace=new MotionDiagnostics(directory))
                    {
                        b.MotionObserver=s=>{ ticks++; last=s; trace.Record("duplicate-id-instance","9320",1.2f,s); };
                        for(int i=0;i<400;i++)
                        {
                            Vector2? cursor=i<100?null:(Vector2?)new Vector2(120,-300);
                            a.ObserveCursor(cursor); b.ObserveCursor(cursor);
                            if(i==200) { a.Behavior.TaskTarget=new Vector2(300,-386); b.Behavior.TaskTarget=a.Behavior.TaskTarget; }
                            var state=UnityEngine.Random.state; a.Advance(.025f); UnityEngine.Random.state=state; b.Advance(.025f);
                            for(int c=0;c<3;c++) Check(a.BodyPosition(c)==b.BodyPosition(c),"Tracing changed body physics");
                            Check(a.HeadPosition==b.HeadPosition && a.State==b.State,"Tracing changed head/state");
                        }
                        Check(ticks==400 && last.hasGoal && last.drive[0].sqrMagnitude>0,"Trace missed fixed ticks or drive force");
                        long length=new FileInfo(trace.TracePath).Length;
                        Check(length>0,"Trace not flushed during capture");
                        ReferenceContext.Set(trace,"samples",MotionDiagnostics.MaximumSamples);
                        trace.Record("cap","cap",1,last);
                        trace.Dispose(); long capped=new FileInfo(trace.TracePath).Length;
                        trace.Record("disposed","disposed",1,last);
                        Check(new FileInfo(trace.TracePath).Length==capped,"Trace wrote after cap/disposal");
                        string text=File.ReadAllText(trace.TracePath);
                        Check(text.Contains("duplicate-id-instance") && text.Contains("connection") && !text.Contains("disposed"),"Trace lost identity/forces");
                    }
                    b.MotionObserver=null; b.Advance(.025f); Check(ticks==400,"Disabled observer still sampled");
                }
                string blocked=Path.Combine(directory,"file"); File.WriteAllText(blocked,"occupied");
                using(var denied=new MotionDiagnostics(blocked)) denied.Record("test","test",1,new PetMotion.MotionSample());
                Debug.Log("MOTION_DIAGNOSTICS_NONINTERFERENCE_TICKS_CAP_IO_CHECK_PASSED");
            }
            finally { UnityEngine.Random.state=random; if(Directory.Exists(directory)) Directory.Delete(directory,true); }
        }
    }
}
