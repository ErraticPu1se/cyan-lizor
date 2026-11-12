using System;
using UnityEngine;
namespace CyanPreview.Editor
{
    public static class WindowStabilityChecks
    {
        static void Check(bool value,string message) { if(!value) throw new Exception(message); }
        public static void Verify()
        { Run(); using(var context=new ReferenceContext(System.IO.Path.GetFullPath("local-data"))) Motion(context); }
        public static void Motion(ReferenceContext context)
        {
            foreach(bool roam in new[]{false,true})
            using(var first=context.CreatePet()) using(var second=context.CreatePet())
            {
                first.Generate("9320"); second.Generate("9320");
                var bounds=new Rect(-700,-400,1400,800);
                var windows=new[]{new DesktopWindow(1,new Rect(-200.25f,-100.333f,400.5f,200.666f))};
                var surfaces=new DesktopSurfaces(bounds) {Mode=ClimbingMode.EdgesAndBackgrounds}; surfaces.UpdateWindows(windows);
                var a=new PetMotion(first,surfaces,42) {Roam=roam,AutonomousJumps=false};
                var b=new PetMotion(second,new BackgroundSurfaces(bounds),42) {Roam=roam,AutonomousJumps=false};
                a.ShiftWithSurface(new Vector2(185,0)-a.Position); b.ShiftWithSurface(new Vector2(185,0)-b.Position);
                float travel=0; var start=a.Position;
                for(int i=0;i<1600;i++)
                {
                    Vector2? cursor=i<800?null:(Vector2?)new Vector2(250,60);
                    a.ObserveCursor(cursor); b.ObserveCursor(cursor);
                    var delta=surfaces.UpdateWindows(windows,a.Position,a.GripCount>0);
                    if(delta.sqrMagnitude>0) { a.Behavior.Rest(); a.ShiftWithSurface(delta,false); }
                    var random=UnityEngine.Random.state; a.Advance(.025f); UnityEngine.Random.state=random; b.Advance(.025f);
                    Check(a.Behavior.Intent==b.Behavior.Intent,"Stationary window interrupted behavior");
                    for(int c=0;c<3;c++) Check(Vector2.Distance(a.BodyPosition(c),b.BodyPosition(c))<.0001f,"Nearby frame pulls free-climbing body");
                    Check(Vector2.Distance(a.HeadPosition,b.HeadPosition)<.0001f,"Nearby frame alters head movement");
                    travel=Mathf.Max(travel,Vector2.Distance(start,a.Position));
                }
                Check(roam?travel>20:travel>.1f,"Natural idle or roaming movement froze");
            }
            Debug.Log("WINDOW_REFRESH_BACKGROUND_IDLE_ROAM_HEAD_EQUIVALENCE_PASSED");
        }
        public static void Run()
        {
            foreach(float scale in new[]{1f,1.2f,1.5f,2f})
            foreach(var mode in new[]{ClimbingMode.EdgesOnly,ClimbingMode.EdgesAndBackgrounds})
            {
                var surfaces=new DesktopSurfaces(new Rect(-1000,-600,2000,1200)) {Mode=mode};
                var rect=new Rect(-601/scale,-257/scale,748/scale,651/scale);
                var windows=new[]{new DesktopWindow(17,rect)};
                surfaces.UpdateWindows(windows);
                for(int i=0;i<1000;i++)
                {
                    var point=new Vector2(rect.xMax-(mode==ClimbingMode.EdgesOnly?14:60),Mathf.Lerp(rect.yMin+40,rect.yMax-40,i/999f));
                    Check(surfaces.UpdateWindows(windows,point,true)==Vector2.zero,"Stationary window falsely moves at scale "+scale+" mode "+mode);
                    Check(!surfaces.SupportLost,"Stationary visible support was lost");
                }
                var pos=new Vector2(rect.xMax-14,rect.center.y);
                var shift=new Vector2(.25f,-.125f);
                var moved=new Rect(rect.position+shift,rect.size);
                Check(Vector2.Distance(surfaces.UpdateWindows(new[]{new DesktopWindow(17,moved)},pos,true),shift)<.0001f,"Small real movement discarded");
                surfaces.UpdateWindows(windows);
                var resized=new Rect(rect.position,rect.size+new Vector2(20,10));
                Check(Vector2.Distance(surfaces.UpdateWindows(new[]{new DesktopWindow(17,resized)},pos,true),new Vector2(20,5))<.0001f,"Resize lost proportional edge attachment");
                surfaces.UpdateWindows(windows);
                var cover=new DesktopWindow(18,new Rect(rect.position-Vector2.one*10,rect.size+Vector2.one*20));
                surfaces.UpdateWindows(new[]{cover,windows[0]},pos,true);
                Check(surfaces.SupportLost,"Unchanged bounds bypassed occlusion check");
                surfaces.UpdateWindows(windows); surfaces.UpdateWindows(new DesktopWindow[0],pos,true);
                Check(surfaces.SupportLost,"Closed window retained attachment");
            }
            var free=new DesktopSurfaces(new Rect(-700,-400,1400,800)) {Mode=ClimbingMode.EdgesAndBackgrounds};
            free.UpdateWindows(new[]{new DesktopWindow(1,new Rect(-200,-100,400,200))});
            foreach(float distance in new[]{-31f,-29f,-14f,0f,14f,29f,31f})
            {
                var point=new Vector2(200+distance,0);
                Check(free.Nearest(point,out var normal)==point && normal==Vector2.zero,"Free climbing attracts body toward nearby window edge");
                Check(Vector2.Distance(free.Advance(point,22),point)<=22.001f,"Free-climb route snaps onto edge");
                Check(free.TryCatchEdge(new Vector2(205,0),10,out _,out _),"Free climbing lost falling edge catch");
            }
            free.Mode=ClimbingMode.EdgesOnly;
            Check(free.Nearest(new Vector2(186,0),out var edgeNormal).x==200 && edgeNormal!=Vector2.zero,"Edges-only support disappeared");
            Debug.Log("WINDOW_STATIONARY_PLANE_EDGE_SCALE_TRANSLATION_RESIZE_OCCLUSION_NO_MAGNET_PASSED");
        }
    }
}
