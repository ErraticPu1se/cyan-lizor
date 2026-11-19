using System;
using System.Runtime.InteropServices;
using UnityEngine;
namespace CyanPreview
{
    public static class DesktopBackgroundSampler
    {
        [DllImport("user32.dll")] static extern bool SetWindowDisplayAffinity(IntPtr window,uint affinity);
        [DllImport("user32.dll")] static extern bool GetWindowDisplayAffinity(IntPtr window,out uint affinity);
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr window);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr window,IntPtr dc);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleBitmap(IntPtr dc,int width,int height);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc,IntPtr obj);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll")] static extern bool BitBlt(IntPtr dest,int x,int y,int w,int h,IntPtr source,int sx,int sy,uint op);
        [DllImport("gdi32.dll")] static extern uint GetPixel(IntPtr dc,int x,int y);
        [DllImport("dwmapi.dll")] static extern int DwmFlush();
        public static Vector2 ScreenPoint(Vector2 world,Rect workArea) => new Vector2(workArea.center.x+world.x,workArea.center.y-world.y);
        public static bool TrySample(IntPtr overlay,Rect area,Vector2 a,Vector2 b,Vector2 c,out Color color)
        {
            color=Color.white;
            // Older Windows treats exclusion as a black rectangle: never sample that as camouflage.
            if(Environment.OSVersion.Version.Build<19041 || overlay==IntPtr.Zero || !GetWindowDisplayAffinity(overlay,out var previous)) return false;
            if(!SetWindowDisplayAffinity(overlay,0x11)) return false;
            IntPtr screen=IntPtr.Zero,dc=IntPtr.Zero,bitmap=IntPtr.Zero,old=IntPtr.Zero;
            try
            {
                if(DwmFlush()!=0) return false;
                screen=GetDC(IntPtr.Zero); if(screen==IntPtr.Zero) return false;
                dc=CreateCompatibleDC(screen); bitmap=CreateCompatibleBitmap(screen,1,1);
                if(dc==IntPtr.Zero || bitmap==IntPtr.Zero) return false;
                old=SelectObject(dc,bitmap);
                if(!Read(screen,dc,ScreenPoint(a,area),area,out var ca) || !Read(screen,dc,ScreenPoint(b,area),area,out var cb)
                    || !Read(screen,dc,ScreenPoint(c,area),area,out var cc)) return false;
                color=PetCamouflage.Combine(ca,cb,cc); return true;
            }
            finally
            {
                if(old!=IntPtr.Zero) SelectObject(dc,old);
                if(bitmap!=IntPtr.Zero) DeleteObject(bitmap);
                if(dc!=IntPtr.Zero) DeleteDC(dc);
                if(screen!=IntPtr.Zero) ReleaseDC(IntPtr.Zero,screen);
                SetWindowDisplayAffinity(overlay,previous);
            }
        }
        static bool Read(IntPtr screen,IntPtr target,Vector2 p,Rect area,out Color color)
        {
            color=Color.white;
            int x=Mathf.Clamp(Mathf.RoundToInt(p.x),(int)area.xMin,(int)area.xMax-1);
            int y=Mathf.Clamp(Mathf.RoundToInt(p.y),(int)area.yMin,(int)area.yMax-1);
            if(!BitBlt(target,0,0,1,1,screen,x,y,0x00CC0020)) return false;
            uint rgb=GetPixel(target,0,0); if(rgb==0xffffffff) return false;
            color=new Color((rgb&255)/255f,((rgb>>8)&255)/255f,((rgb>>16)&255)/255f); return true;
        }
    }
}
