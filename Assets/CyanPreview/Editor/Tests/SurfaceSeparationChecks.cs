using System;
using UnityEngine;
namespace CyanPreview.Editor
{
    public static class SurfaceSeparationChecks
    {
        static void Check(bool ok,string why) { if(!ok) throw new Exception(why); }
        public static void Run()
        {
            var area=new Rect(-400,-250,800,500);
            var window=new DesktopWindow(7,new Rect(-100,-100,200,200));
            var frame=new FrameSurfaces(area);
            Check(frame.SupportAt(new Vector2(0,-240)).Kind==SurfaceKind.Floor,"Bottom is not a floor");
            Check(frame.SupportAt(new Vector2(0,240)).Kind==SurfaceKind.Ceiling,"Top is not a ceiling");
            Check(frame.SupportAt(new Vector2(-390,0)).Kind==SurfaceKind.Wall,"Side is not a wall");
            var cyan=new DesktopSurfaces(area,LizardSpecies.Find("cyan").Movement);
            cyan.UpdateWindows(new[]{window});
            foreach(float side in new[]{-1f,1f})
            {
                var p=new Vector2(100+side*5,0); var support=cyan.SupportAt(p);
                Check(support.Kind==SurfaceKind.Pole && support.WindowId==7 && support.Point.x==100,"Window edge is not an owned pole");
                Check(support.Normal.x==side,"Pole grip pointed through the window from one side");
                Check(cyan.TryCatchSupport(p,10,out var caught) && caught.Kind==SurfaceKind.Pole && caught.Normal.x==side,"Falling pole catch lost side or kind");
            }
            foreach(var point in new[]{new Vector2(95,95),new Vector2(105,105)})
            { var next=cyan.Approach(point,new Vector2(-100,0),15); Check(cyan.SupportAt(next).Kind==SurfaceKind.Pole || cyan.SupportAt(next).Kind==SurfaceKind.Floor,"Corner traversal left the rail or top platform"); }
            cyan.Mode=ClimbingMode.EdgesAndBackgrounds;
            Check(cyan.SupportAt(new Vector2(95,0)).Kind==SurfaceKind.Background && cyan.SupportAt(new Vector2(95,0)).WindowId==7,"Window plane lost background ownership");
            Check(cyan.Nearest(new Vector2(95,0),out var n)==new Vector2(95,0) && n==Vector2.zero,"Background magnetized toward pole");
            Check(!cyan.TryCatchSupport(Vector2.zero,10,out _),"Background became a falling catch surface");
            Check(!cyan.TryCatchSupport(new Vector2(0,-245),10,out _),"Floor bypassed normal landing");
            Check(!cyan.TryCatchSupport(Vector2.zero,float.NaN,out _) && !cyan.TryCatchSupport(Vector2.zero,-1,out _),"Invalid catch reach accepted");
            var polesOnly=new DesktopSurfaces(area,new MovementCapabilities(false,2,climbPoles:true,climbBackgrounds:false)) {Mode=ClimbingMode.EdgesAndBackgrounds};
            polesOnly.UpdateWindows(new[]{window});
            Check(!polesOnly.SupportsFreeClimbing && polesOnly.SupportAt(new Vector2(110,0)).Kind==SurfaceKind.Pole,"Pole permission requires wall/background ability");
            Check(polesOnly.TryCatchSupport(new Vector2(110,0),15,out _) && !polesOnly.TryCatchSupport(new Vector2(395,0),15,out _),"Pole-only species caught wall or missed pole");
            var wallsOnly=new DesktopSurfaces(area,new MovementCapabilities(true,2,climbPoles:false,climbBackgrounds:false)) {Mode=ClimbingMode.EdgesAndBackgrounds};
            wallsOnly.UpdateWindows(new[]{window});
            Check(!wallsOnly.TryCatchSupport(new Vector2(105,0),15,out _) && wallsOnly.TryCatchSupport(new Vector2(395,0),15,out var wall) && wall.Kind==SurfaceKind.Wall,"Wall/pole permissions coupled");
            var floorOnly=new DesktopSurfaces(area,new MovementCapabilities(false,2)); floorOnly.UpdateWindows(new[]{window});
            Check(floorOnly.SupportAt(Vector2.zero).Kind==SurfaceKind.Floor && floorOnly.Advance(Vector2.zero,50).y==area.yMin,"Non-climber gained elevated support");
            Check(floorOnly.WindowAtSurface(new Vector2(105,0))==0,"Non-climber attached to a window");
            var backgroundOnly=new DesktopSurfaces(area,new MovementCapabilities(false,2,climbPoles:false,climbBackgrounds:true)) {Mode=ClimbingMode.EdgesAndBackgrounds};
            Check(backgroundOnly.SupportsFreeClimbing,"Background permission requires walls");
            backgroundOnly.Mode=ClimbingMode.EdgesOnly;
            Check(!backgroundOnly.SupportsFreeClimbing && backgroundOnly.SupportAt(Vector2.zero).Kind==SurfaceKind.Floor,"User restriction granted unsupported climbing");
            var moved=new DesktopWindow(7,new Rect(-80,-90,200,200));
            cyan.Mode=ClimbingMode.EdgesOnly;
            var delta=cyan.UpdateWindows(new[]{moved},new Vector2(105,0),true);
            Check(delta==new Vector2(20,10) && !cyan.SupportLost,"Outside pole did not follow its window");
            Check(cyan.UpdateWindows(new[]{moved},new Vector2(125,10),true)==Vector2.zero,"Stationary pole caused correction");
            cyan.UpdateWindows(new DesktopWindow[0],new Vector2(125,10),true);
            Check(cyan.SupportLost && !cyan.TryCatchSupport(new Vector2(125,10),10,out _),"Closed pole retained support");
            cyan.UpdateWindows(new[]{new DesktopWindow(9,new Rect(70,-120,100,240)),window});
            Check(!cyan.TryCatchSupport(new Vector2(105,0),10,out _),"Hidden pole remained catchable");
            cyan.UpdateWindows(new[]{window});
            var den=new DenRouteSurfaces(cyan,area);
            Check(den.SupportAt(new Vector2(105,0)).Kind==SurfaceKind.Pole && den.TryCatchSupport(new Vector2(105,0),10,out var ds) && ds.WindowId==7,"Den wrapper erased support kind or owner");
            den.Open(300); Check(den.SupportAt(new Vector2(0,-400)).Kind==SurfaceKind.Background && !den.TryCatchSupport(Vector2.zero,10,out _),"Private den route gained desktop pole catches");
            den.Close(); Check(den.SupportAt(new Vector2(105,0)).Kind==SurfaceKind.Pole,"Closing den did not restore poles");
            Debug.Log("SURFACE_KINDS_TWO_SIDED_POLES_CAPABILITIES_OWNERSHIP_CATCH_OCCLUSION_CHECK_PASSED");
        }
    }
}
