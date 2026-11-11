using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace CyanPreview
{
    public sealed class WindowsTray : IDisposable
    {
        const uint Callback = 0x801F;
        const string WindowMarker = "ErraticPu1se.Lizor.Controller";
        delegate bool EnumProc(IntPtr window,IntPtr data);
        delegate IntPtr WindowProc(IntPtr window,uint message,IntPtr wParam,IntPtr lParam);
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
        struct NotifyData
        {
            public uint Size; public IntPtr Window; public uint Id, Flags, Message; public IntPtr Icon;
            [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)] public string Tip;
            public uint State, StateMask;
            [MarshalAs(UnmanagedType.ByValTStr,SizeConst=256)] public string Info;
            public uint Timeout;
            [MarshalAs(UnmanagedType.ByValTStr,SizeConst=64)] public string InfoTitle;
            public uint InfoFlags; public Guid Guid; public IntPtr BalloonIcon;
        }
        [StructLayout(LayoutKind.Sequential)] struct Point { public int X,Y; }
        [StructLayout(LayoutKind.Sequential)] struct IconInfo { [MarshalAs(UnmanagedType.Bool)] public bool Icon; public uint X,Y; public IntPtr Mask,Color; }
        [StructLayout(LayoutKind.Sequential)] struct BitmapInfo
        { public uint Size; public int Width,Height; public ushort Planes,Bits; public uint Compression,ImageSize; public int XPixels,YPixels; public uint Colors,Important; }
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc callback,IntPtr data);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr window,StringBuilder name,int count);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern bool SetWindowText(IntPtr window,string text);
        [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW",SetLastError=true)] static extern IntPtr SetWindowLongPtr(IntPtr window,int index,IntPtr value);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr CallWindowProc(IntPtr previous,IntPtr window,uint message,IntPtr wParam,IntPtr lParam);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern uint RegisterWindowMessage(string message);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern bool SetProp(IntPtr window,string name,IntPtr value);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr GetProp(IntPtr window,string name);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr RemoveProp(IntPtr window,string name);
        [DllImport("user32.dll")] static extern bool PostMessage(IntPtr window,uint message,IntPtr wParam,IntPtr lParam);
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr window,uint message,IntPtr wParam,IntPtr lParam);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr window,int command);
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr window);
        [DllImport("user32.dll")] static extern bool AllowSetForegroundWindow(uint process);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr window);
        [DllImport("user32.dll")] static extern bool IsWindow(IntPtr window);
        [DllImport("user32.dll")] static extern int GetSystemMetrics(int metric);
        [DllImport("user32.dll")] static extern bool GetCursorPos(out Point point);
        [DllImport("user32.dll")] static extern IntPtr CreatePopupMenu();
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern bool AppendMenu(IntPtr menu,uint flags,UIntPtr id,string text);
        [DllImport("user32.dll")] static extern uint TrackPopupMenu(IntPtr menu,uint flags,int x,int y,int reserved,IntPtr owner,IntPtr rectangle);
        [DllImport("user32.dll")] static extern bool DestroyMenu(IntPtr menu);
        [DllImport("shell32.dll",CharSet=CharSet.Unicode)] static extern bool Shell_NotifyIcon(uint operation,ref NotifyData data);
        [DllImport("gdi32.dll")] static extern IntPtr CreateDIBSection(IntPtr dc,ref BitmapInfo info,uint usage,out IntPtr pixels,IntPtr section,uint offset);
        [DllImport("gdi32.dll")] static extern IntPtr CreateBitmap(int width,int height,uint planes,uint bits,byte[] data);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr bitmap);
        [DllImport("user32.dll")] static extern IntPtr CreateIconIndirect(ref IconInfo info);
        [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);

        readonly ConcurrentQueue<TrayAction> actions=new ConcurrentQueue<TrayAction>();
        readonly uint showMessage=RegisterWindowMessage("ErraticPu1se.Lizor.ShowSettings");
        readonly uint taskbarCreated=RegisterWindowMessage("TaskbarCreated");
        readonly WindowProc callback;
        IntPtr window, previousProc, icon, oldSmallIcon, oldLargeIcon;
        NotifyData data;
        bool disposed, endingSession, menuOpen;
        bool petActive, paused, sound;
        long retryAt;
        public bool Available { get; private set; }
        public bool AllowExit { get; set; }
        public bool PanelVisible => window!=IntPtr.Zero && IsWindowVisible(window) && !IsIconic(window);
        public WindowsTray()
        {
            callback=HandleMessage;
            int process=Process.GetCurrentProcess().Id;
            EnumWindows((candidate,unused)=>
            {
                GetWindowThreadProcessId(candidate,out uint pid);
                var name=new StringBuilder(64); GetClassName(candidate,name,name.Capacity);
                if(pid!=process || name.ToString()!="UnityWndClass") return true;
                window=candidate; return false;
            },IntPtr.Zero);
            if(window==IntPtr.Zero) throw new InvalidOperationException("Lizor settings window was not found.");
            try
            {
                icon=CreateLizIcon(Math.Max(16,GetSystemMetrics(49)));
                previousProc=SetWindowLongPtr(window,-4,Marshal.GetFunctionPointerForDelegate(callback));
                if(previousProc==IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(),"Cannot attach the tray to Lizor.");
                if(!SetProp(window,WindowMarker,new IntPtr(1))) throw new InvalidOperationException("Cannot register the Lizor window.");
                SetWindowText(window,"Lizor");
                oldSmallIcon=SendMessage(window,0x80,IntPtr.Zero,icon);
                oldLargeIcon=SendMessage(window,0x80,new IntPtr(1),icon);
                data=new NotifyData {Size=(uint)Marshal.SizeOf<NotifyData>(),Window=window,Id=1,Flags=7,Message=Callback,Icon=icon,Tip="Lizor",Info="",InfoTitle=""};
                Available=Shell_NotifyIcon(0,ref data);
            }
            catch { Dispose(); throw; }
        }
        static IntPtr CreateLizIcon(int size)
        {
            byte[] pixels=TrayGlyph.Pixels(size);
            var bitmapInfo=new BitmapInfo {Size=40,Width=size,Height=-size,Planes=1,Bits=32};
            IntPtr color=CreateDIBSection(IntPtr.Zero,ref bitmapInfo,0,out var memory,IntPtr.Zero,0), mask=IntPtr.Zero;
            try
            {
                if(color==IntPtr.Zero) throw new InvalidOperationException("Cannot create the Liz icon.");
                Marshal.Copy(pixels,0,memory,pixels.Length);
                mask=CreateBitmap(size,size,1,1,new byte[((size+15)/16)*2*size]);
                if(mask==IntPtr.Zero) throw new InvalidOperationException("Cannot create the Liz icon mask.");
                var info=new IconInfo {Icon=true,Mask=mask,Color=color};
                var result=CreateIconIndirect(ref info);
                if(result==IntPtr.Zero) throw new InvalidOperationException("Cannot create the Liz tray icon.");
                return result;
            }
            finally { if(color!=IntPtr.Zero) DeleteObject(color); if(mask!=IntPtr.Zero) DeleteObject(mask); }
        }
        public static void RestoreExisting()
        {
            uint message=RegisterWindowMessage("ErraticPu1se.Lizor.ShowSettings");
            EnumWindows((candidate,unused)=>
            {
                if(GetProp(candidate,WindowMarker)==IntPtr.Zero) return true;
                GetWindowThreadProcessId(candidate,out uint pid); AllowSetForegroundWindow(pid);
                PostMessage(candidate,message,IntPtr.Zero,IntPtr.Zero); return false;
            },IntPtr.Zero);
        }
        public void ShowSettings()
        {
            if(disposed || window==IntPtr.Zero) return;
            ShowWindow(window,9); SetForegroundWindow(window);
        }
        public void UpdateState(bool active,bool isPaused,bool sounds)
        {
            if(disposed) return;
            if(!Available && DateTime.UtcNow.Ticks>=retryAt)
            { retryAt=DateTime.UtcNow.AddSeconds(2).Ticks; Available=Shell_NotifyIcon(0,ref data); }
            petActive=active; paused=isPaused; sound=sounds;
            string tip=paused?"Lizor — paused":active?"Lizor — pet running":"Lizor";
            if(tip==data.Tip || !Available) return;
            data.Tip=tip; Shell_NotifyIcon(1,ref data);
        }
        public bool NotifyHidden()
        {
            if(!Available) return false;
            var notification=data; notification.Flags=0x10;
            notification.InfoTitle="Lizor is still running";
            notification.Info="Double-click Liz in the system tray to reopen settings. Right-click it to pause, change sound, or quit.";
            notification.InfoFlags=1;
            return Shell_NotifyIcon(1,ref notification);
        }
        public bool TryRead(out TrayAction action)
        {
            return actions.TryDequeue(out action);
        }
        IntPtr HandleMessage(IntPtr handle,uint message,IntPtr wParam,IntPtr lParam)
        {
            // Native callbacks only queue commands. Unity objects are touched in Update.
            try
            {
                if(message==0x11) endingSession=true; // WM_QUERYENDSESSION must never be hidden.
                if(message==0x16) endingSession=wParam!=IntPtr.Zero;
                if(message==0x10 && TrayState.HideOnClose(Available,AllowExit,endingSession))
                { ShowWindow(window,0); actions.Enqueue(TrayAction.Hidden); return IntPtr.Zero; }
                if(message==showMessage) { ShowSettings(); return IntPtr.Zero; }
                if(message==taskbarCreated)
                {
                    Available=Shell_NotifyIcon(0,ref data);
                    if(!Available) ShowSettings();
                    return IntPtr.Zero;
                }
                if(message==Callback)
                {
                    uint notification=unchecked((uint)lParam.ToInt64()) & 0xffff;
                    if(notification==0x203 || notification==0x401 || notification==0x405) ShowSettings();
                    else if(notification==0x205 || notification==0x7B) OpenMenu();
                    return IntPtr.Zero;
                }
            }
            catch { ShowSettings(); } // Do not let managed exceptions unwind into Unity's native message loop.
            return CallWindowProc(previousProc,handle,message,wParam,lParam);
        }
        void OpenMenu()
        {
            if(menuOpen || disposed) return;
            IntPtr menu=CreatePopupMenu(); if(menu==IntPtr.Zero) return;
            menuOpen=true;
            try
            {
                AppendMenu(menu,0,(UIntPtr)(uint)TrayAction.Settings,"Settings...");
                AppendMenu(menu,petActive?0u:1u,(UIntPtr)(uint)TrayAction.PauseResume,paused?"Resume pet":"Pause pet");
                AppendMenu(menu,sound?8u:0u,(UIntPtr)(uint)TrayAction.Sound,"Sound");
                AppendMenu(menu,petActive?0u:1u,(UIntPtr)(uint)TrayAction.StopPet,"Stop desktop pet");
                AppendMenu(menu,0x800,UIntPtr.Zero,null);
                AppendMenu(menu,0,(UIntPtr)(uint)TrayAction.Quit,"Quit Lizor");
                GetCursorPos(out var point); SetForegroundWindow(window);
                uint selected=TrackPopupMenu(menu,0x100|0x80|2,point.X,point.Y,0,window,IntPtr.Zero);
                if(selected!=0) actions.Enqueue((TrayAction)selected);
                PostMessage(window,0,IntPtr.Zero,IntPtr.Zero);
            }
            finally { DestroyMenu(menu); menuOpen=false; }
        }
        public void Dispose()
        {
            if(disposed) return; disposed=true; AllowExit=true;
            if(window!=IntPtr.Zero)
            {
                if(Available) Shell_NotifyIcon(2,ref data);
                if(IsWindow(window))
                {
                    RemoveProp(window,WindowMarker);
                    if(previousProc!=IntPtr.Zero) SetWindowLongPtr(window,-4,previousProc);
                    if(icon!=IntPtr.Zero) { SendMessage(window,0x80,IntPtr.Zero,oldSmallIcon); SendMessage(window,0x80,new IntPtr(1),oldLargeIcon); }
                }
            }
            Available=false;
            if(icon!=IntPtr.Zero) { DestroyIcon(icon); icon=IntPtr.Zero; }
            GC.KeepAlive(callback);
        }
    }
}
