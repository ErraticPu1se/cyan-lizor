using System;
using System.Runtime.InteropServices;
using UnityEngine;
namespace CyanPreview.Editor
{
    public static class BackgroundSamplingChecks
    {
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr CreateWindowEx(uint ex,string cls,string title,uint style,int x,int y,int width,int height,IntPtr parent,IntPtr menu,IntPtr instance,IntPtr param);
        [DllImport("user32.dll")] static extern bool DestroyWindow(IntPtr window);
        [DllImport("user32.dll")] static extern bool SetLayeredWindowAttributes(IntPtr window,uint key,byte alpha,uint flags);
        [DllImport("user32.dll")] static extern bool UpdateWindow(IntPtr window);
        [DllImport("user32.dll")] static extern bool GetWindowDisplayAffinity(IntPtr window,out uint affinity);
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr window);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr window,IntPtr dc);
        [DllImport("gdi32.dll")] static extern uint GetPixel(IntPtr dc,int x,int y);
        [DllImport("dwmapi.dll")] static extern int DwmFlush();
        [StructLayout(LayoutKind.Sequential)] struct Rect { public int left,top,right,bottom; }
        [DllImport("user32.dll")] static extern int FillRect(IntPtr dc,ref Rect rect,IntPtr brush);
        [DllImport("gdi32.dll")] static extern IntPtr GetStockObject(int index);
        static void Paint(IntPtr window,int brush)
        {
            var dc=GetDC(window); var rect=new Rect {right=100,bottom=100};
            try { FillRect(dc,ref rect,GetStockObject(brush)); } finally { ReleaseDC(window,dc); }
        }
        public static void Verify()
        {
            // Owned fixtures: white background covered by a black layered overlay.
            IntPtr background=IntPtr.Zero,overlay=IntPtr.Zero;
            try
            {
                background=CreateWindowEx(8,"STATIC","Lizor sampler background test",0x90000006,100,100,100,100,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero);
                overlay=CreateWindowEx(0x80008,"STATIC","Lizor sampler overlay test",0x90000004,100,100,100,100,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero);
                if(background==IntPtr.Zero||overlay==IntPtr.Zero) throw new Exception("Capture fixture creation failed");
                SetLayeredWindowAttributes(overlay,0,255,2); UpdateWindow(background); UpdateWindow(overlay); Paint(background,0); Paint(overlay,4); DwmFlush();
                var screen=GetDC(IntPtr.Zero); uint before; try { before=GetPixel(screen,150,150); } finally { ReleaseDC(IntPtr.Zero,screen); }
                if(before!=0) throw new Exception("Capture fixture was not covered by black overlay: "+before);
                for(int i=0;i<3;i++)
                {
                    if(!DesktopBackgroundSampler.TrySample(overlay,new UnityEngine.Rect(100,100,100,100),Vector2.zero,Vector2.one,-Vector2.one,out var color))
                        throw new Exception("Background sampling unavailable: "+Environment.OSVersion);
                    if(color.r<.98f||color.g<.98f||color.b<.98f) throw new Exception("Sample included overlay instead of white background: "+color);
                    if(!GetWindowDisplayAffinity(overlay,out var affinity)||affinity!=0) throw new Exception("Sampling left capture exclusion enabled");
                }
                using(var context=new ReferenceContext(System.IO.Path.GetFullPath("local-data")))
                using(var desktop=new WindowsDesktop(0))
                {
                    var root=new GameObject("Camouflage desktop fixture");
                    try
                    {
                        ReferenceContext.Set(desktop,"handle",overlay);
                        var area=desktop.WorkArea;
                        using(var pet=new DesktopPet(context,root.transform,new DesktopPetConfiguration {key="camo-check",id="9320",species="white",scale=1,roam=false,sound=false},new UnityEngine.Rect(-area.width/2,-area.height/2,area.width,area.height),0))
                        {
                            var point=new Vector2(165-area.center.x,area.center.y-150);
                            pet.Motion.ShiftWithSurface(point-pet.Motion.Position);
                            for(int i=0;i<400;i++) { pet.Advance(.025f,false); pet.UpdateCamouflage(.025f,desktop); }
                            if(pet.Camouflage.Amount<.95f || pet.Camouflage.Color.r<.98f) throw new Exception("Desktop idle did not camouflage against sampled background: "+pet.Camouflage.Amount+" "+pet.Camouflage.Color+" "+pet.Motion.Behavior.Intent);
                        }
                    }
                    finally { ReferenceContext.Set(desktop,"handle",IntPtr.Zero); UnityEngine.Object.DestroyImmediate(root); }
                }
                Debug.Log("NATIVE_BACKGROUND_EXCLUDES_OVERLAY_RESTORES_CAPTURE_CHECK_PASSED");
            }
            finally { if(overlay!=IntPtr.Zero) DestroyWindow(overlay); if(background!=IntPtr.Zero) DestroyWindow(background); }
        }
    }
}
