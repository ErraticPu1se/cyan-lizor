using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;
using Debug=UnityEngine.Debug;

namespace CyanPreview.Editor
{
    public static class CursorInputChecks
    {
        static void Check(bool value,string message) { if(!value) throw new Exception(message); }
        static object Call(object target,string method,params object[] args) => target.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,args);
        static T Field<T>(object target,string name) => (T)target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);
        [DllImport("user32.dll")] static extern bool PostThreadMessage(uint thread,uint message,UIntPtr wParam,IntPtr lParam);
        [DllImport("user32.dll")] static extern void mouse_event(uint flags,uint dx,uint dy,uint data,UIntPtr extra);
        public static void Run()
        {
            var circles=new[]{new DesktopHitSnapshot.Circle(-100,40,14)};
            var snapshot=new DesktopHitSnapshot(circles); circles[0]=new DesktopHitSnapshot.Circle(0,0,1000);
            Check(snapshot.Contains(-100,40) && snapshot.Contains(-86,40) && !snapshot.Contains(-85,40),"Hit radius/negative monitor coordinates changed");
            Check(!snapshot.Contains(float.NaN,40) && !snapshot.Contains(float.PositiveInfinity,40) && !snapshot.Contains(0,0),"Invalid coordinates or caller mutation changed hit snapshot");
            var clipped=new DesktopHitSnapshot(new[]{new DesktopHitSnapshot.Circle(0,0,14)},0,0,100,100);
            Check(clipped.Contains(1,1) && !clipped.Contains(-1,1),"Offscreen pet geometry captured another monitor");
            using(var native=new WindowsDesktop(Process.GetCurrentProcess().Id))
            {
                native.PublishHitSnapshot(snapshot);
                Func<uint,int,int,bool> button=(kind,x,y)=>(bool)Call(native,"QueueMouseButton",kind,x,y,1u,false);
                Check(button(0x201,-100,40),"Pet down was not captured");
                native.PublishHitSnapshot(DesktopHitSnapshot.Empty); native.ClearButtonEvents();
                Check(button(0x202,800,900),"Pause/cancel leaked the paired release to another app");
                Check(native.TryReadButton(out var up) && !up.Down && up.Captured,"Captured release metadata missing");
                Check(!button(0x201,800,900) && !button(0x202,800,900),"Outside clicks blocked another app");
                native.ClearButtonEvents(); native.PublishHitSnapshot(new DesktopHitSnapshot(new[]{new DesktopHitSnapshot.Circle(-100,40,14)}));
                Check(button(0x204,-100,40) && button(0x205,900,900),"Right click pair was not captured");
                Check(native.TryReadMenuRequest(out var point) && point==new Vector2(900,900) && !native.TryReadMenuRequest(out _),"Menu request was lost or duplicated");
                for(int i=0;i<1000;i++) button(i%2==0?0x201u:0x202u,800,900);
                Check(native.ConsumeInputOverflow() && !native.ConsumeInputOverflow(),"Queue overload not signalled once");
                int count=0; while(native.TryReadButton(out _)) count++;
                Check(count==128,"Input backlog is unbounded");
                native.ClearButtonEvents(); Check(!native.TryReadButton(out _),"Input clear retained stale events");
                native.PublishHitSnapshot(snapshot); Thread.Sleep(280);
                Check(!button(0x201,-100,40),"Stalled rendering left stale click capture");
                button(0x202,-100,40);
                native.PublishHitSnapshot(DesktopHitSnapshot.Empty);
                for(int cycle=0;cycle<3;cycle++)
                {
                    Call(native,"StartMouseThread");
                    native.GetType().GetField("inputProbeEnabled",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(native,true);
                    int hookBefore=Field<int>(native,"inputHookProbeCount");
                    uint id=Field<uint>(native,"mouseThreadId");
                    int before=Field<int>(native,"inputProbeCount");
                    var timer=Stopwatch.StartNew();
                    // Deliberately withhold the Unity message pump for over half a second (depending on the OS timer resolution).
                    for(int i=0;i<100;i++) { Check(PostThreadMessage(id,0x8001,UIntPtr.Zero,IntPtr.Zero),"Input worker queue unavailable"); mouse_event(1,i%2==0?1u:unchecked((uint)-1),0,0,UIntPtr.Zero); Thread.Sleep(5); }
                    int handled=Field<int>(native,"inputProbeCount")-before;
                    int hookCalls=Field<int>(native,"inputHookProbeCount")-hookBefore;
                    Debug.Log("NATIVE_MOUSE_DELIVERY count="+hookCalls);
                    Check(hookCalls>=90,"Native mouse hook delivery stalled while Unity was busy");
                    Check(handled>=95,"Input pump stalled alongside Unity's main thread");
                    Debug.Log("CURSOR_WORKER_STALL_PROBE cycle="+cycle+" handled="+handled+" nativeHookCalls="+hookCalls+" elapsedMs="+timer.ElapsedMilliseconds);
                    Call(native,"StopMouseThread");
                }
            }
            Debug.Log("CURSOR_SNAPSHOT_PAIRING_QUEUE_STALL_LIFECYCLE_CHECK_PASSED");
        }
    }
}
