using System;
using UnityEngine;
using static CyanPreview.ReferenceContext;
namespace CyanPreview.Editor
{
    public static class PolePostureChecks
    {
        static void Check(bool value,string why) { if(!value) throw new Exception(why); }
        static readonly Rect area=new Rect(-500,-400,1000,800);
        public static void Verify() { using(var c=new ReferenceContext(System.IO.Path.GetFullPath("local-data"))) Run(c); }
        public static void Run(ReferenceContext context)
        {
            var window=new DesktopWindow(1,new Rect(-100,-200,200,400));
            foreach(string species in new[]{"cyan","white"})
            foreach(float heading in new[]{-1f,1f})
            foreach(float side in new[]{-1f,1f}) using(var pet=context.CreatePet(species))
            {
                pet.Generate(species=="cyan"?"9320":"2455"); var surfaces=new DesktopSurfaces(area); surfaces.UpdateWindows(new[]{window});
                var motion=new PetMotion(pet,surfaces,42) {Roam=false,AutonomousJumps=false};
                Place(pet,new Vector2(100+side*14,0),Vector2.up*heading);
                float lastDepth=.7f,maxDepthStep=0,travel=0,maxMove=0;
                for(int i=0;i<600;i++)
                {
                    var before=motion.Position; motion.ObserveCursor(null); motion.Advance(.025f);
                    float depth=(float)Get(pet.Graphics,"depthRotation"); maxDepthStep=Mathf.Max(maxDepthStep,Mathf.Abs(depth-lastDepth)); lastDepth=depth;
                    if(i>200) { float move=Vector2.Distance(before,motion.Position); travel+=move; maxMove=Mathf.Max(maxMove,move); }
                }
                Check(motion.OnPole && motion.GripCount>0,"Vertical rail lost planted support");
                var grip=(SurfaceSupport)Get(motion,"poleGrip"); Check(grip.Normal.x==side,"Pole switched body side while resting");
                Check(Mathf.Abs(motion.Position.x-100)<12 && Mathf.Abs(motion.Position.x-100)>4,"Pole body retained wall clearance or collapsed into rail");
                Check(maxDepthStep<.2f && Mathf.Abs(lastDepth-.7f)>.05f,"Pole body rotation did not adapt smoothly");
                Check(travel>.1f && maxMove<1,"Pole idle froze or jittered");
                Check(Mathf.Abs(lastDepth)<.05f && Mathf.Abs((float)Get(pet.Graphics,"headDepthRotation"))<.05f,"Pole did not settle into back view: "+species);
                var limbs=(Array)Get(pet.Graphics,"limbs");
                for(int pair=0;pair<4;pair+=2) Check((float)Get(limbs.GetValue(pair),"flip")>.9f && (float)Get(limbs.GetValue(pair+1),"flip")<-.9f,"Pole knees did not wrap opposite sides");
                if(side==1 && heading==1) Capture(pet,motion.Position,species);
                motion.ExploreClick(motion.Position+Vector2.up*heading*80);
                for(int step=0;step<20;step++) { motion.Advance(.025f); Check(Mathf.Abs((float)Get(pet.Graphics,"depthRotation"))<.15f,"Walking rolled into side view"); }

                motion.BeginGrab(motion.Position); Check(!motion.OnPole,"Grab retained pole attachment"); motion.EndGrab();
                surfaces.UpdateWindows(new DesktopWindow[0]); motion.Advance(.025f); Check(!motion.OnPole,"Deleted rail retained pole pose");
                for(int frame=0;frame<150;frame++) motion.Advance(.025f);
                Check(Mathf.Abs((float)Get(pet.Graphics,"depthRotation")-.7f)<.01f,"Leaving pole did not restore normal rotation");
            }
            var floorWalker=new DesktopSurfaces(area,new MovementCapabilities(false,2)); floorWalker.UpdateWindows(new[]{window});
            Check(floorWalker.Advance(new Vector2(0,210),30)==new Vector2(30,200),"Floor-only species walked toward desktop floor instead of along window top");
            Check(floorWalker.Approach(new Vector2(0,210),new Vector2(-80,200),30)==new Vector2(-30,200),"Window-floor approach used pole abilities");
            var geometry=new DesktopSurfaces(area); geometry.UpdateWindows(new[]{window}); geometry.BeginFloorStep();
            Check(geometry.TryLand(new Vector2(0,260),new Vector2(0,100),8,out var top) && top.Kind==SurfaceKind.Floor && top.Point.y==200,"Fast fall missed top floor");
            Check(!geometry.TryLand(new Vector2(0,100),new Vector2(0,260),8,out _),"Upward travel collided with one-way floor");
            Check(!geometry.TryLand(new Vector2(0,170),new Vector2(0,150),8,out _),"Below-floor fall snapped above");
            Check(!geometry.TryLand(new Vector2(120,260),new Vector2(120,100),8,out _),"Floor extended beyond window");
            geometry.UpdateWindows(new[]{new DesktopWindow(1,new Rect(-100,-150,200,400))}); geometry.BeginFloorStep();
            Check(geometry.TryLand(new Vector2(0,230),new Vector2(0,230),8,out _),"Moving top crossed body without support");
            geometry.BeginFloorStep(); Check(!geometry.TryLand(new Vector2(0,230),new Vector2(0,230),8,out _),"Moving-floor sweep repeated on later physics tick");
            geometry.UpdateWindows(new[]{new DesktopWindow(2,new Rect(-120,150,240,160)),window}); geometry.BeginFloorStep();
            Check(!geometry.TryLand(new Vector2(0,260),new Vector2(0,180),8,out _),"Covered top floor caught pet");
            geometry.UpdateWindows(new DesktopWindow[0]); geometry.BeginFloorStep();
            Check(!geometry.TryLand(new Vector2(0,260),new Vector2(0,100),8,out _),"Closed top floor retained collision");
            foreach(var mode in new[]{ClimbingMode.EdgesOnly,ClimbingMode.EdgesAndBackgrounds}) using(var pet=context.CreatePet())
            {
                pet.Generate("9320"); var surfaces=new DesktopSurfaces(area) {Mode=mode}; surfaces.UpdateWindows(new[]{window});
                var motion=new PetMotion(pet,surfaces,42) {Roam=false,AutonomousJumps=false};
                Place(pet,new Vector2(0,310),Vector2.right); motion.ReleaseSupport();
                for(int i=0;i<150;i++) motion.Advance(.025f);
                Check(motion.OnWindowFloor && motion.State==PetMotionState.Crawling && motion.Position.y>200 && motion.Position.y<230,"Top landing did not remain supported: "+mode);
                surfaces.UpdateWindows(new DesktopWindow[0]); motion.Advance(.025f);
                Check(!motion.OnWindowFloor && motion.State==PetMotionState.Airborne,"Removed floor did not release pet");
            }
            Debug.Log("POLE_POSTURE_GRIP_ROTATION_IDLE_AND_ONE_WAY_WINDOW_TOP_CHECK_PASSED");
        }
        static void Capture(ReferenceBridge pet,Vector2 position,string species)
        {
            var root=new GameObject("Pole render"); var cameraObject=new GameObject("Pole camera");
            var camera=cameraObject.AddComponent<Camera>(); camera.orthographic=true; camera.orthographicSize=100; camera.aspect=1;
            camera.transform.position=new Vector3(position.x,position.y-40,-10); camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.22f,.25f,.27f,1);
            var rt=new RenderTexture(256,256,24); var pixels=new Texture2D(256,256,TextureFormat.RGBA32,false); var previous=RenderTexture.active;
            var view=new SpriteMeshView(root.transform);
            try
            {
                pet.Draw(); view.Refresh(pet); camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt;
                pixels.ReadPixels(new Rect(0,0,256,256),0,0); pixels.Apply();
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(System.IO.Path.GetTempPath(),"lizor-pole-"+species+".png"),pixels.EncodeToPNG());
            }
            finally { view.Dispose(); RenderTexture.active=previous; camera.targetTexture=null; UnityEngine.Object.DestroyImmediate(pixels); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(cameraObject); UnityEngine.Object.DestroyImmediate(root); }
        }
        static void Place(ReferenceBridge pet,Vector2 point,Vector2 forward)
        {
            var chunks=(Array)Get(pet.Lizard,"bodyChunks");
            for(int i=0;i<3;i++) { var p=point-forward*17*i; Set(chunks.GetValue(i),"pos",p); Set(chunks.GetValue(i),"lastPos",p); Set(chunks.GetValue(i),"vel",Vector2.zero); }
        }
    }
}
