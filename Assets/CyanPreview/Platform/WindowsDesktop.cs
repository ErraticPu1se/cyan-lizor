using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace CyanPreview
{
    // Only the owned Unity window is modified. Other windows are read as rectangles.
    public sealed class WindowsDesktop : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)] struct NativeRect { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] struct Point { public int X, Y; }
        delegate bool EnumProc(IntPtr hwnd, IntPtr data);
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc callback, IntPtr data);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr hwnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr hwnd, StringBuilder text, int size);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);
        [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")] static extern int GetFrame(IntPtr hwnd, int attribute, out NativeRect rect, int size);
        [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")] static extern int GetCloaked(IntPtr hwnd, int attribute, out int value, int size);
        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hwnd, int index, int value);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
        public bool EscapePressed => (GetAsyncKeyState(0x1B) & 0x8000)!=0;
        [DllImport("user32.dll")] static extern bool GetCursorPos(out Point point);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern bool SystemParametersInfo(uint action, uint param, out NativeRect rect, uint flags);
        delegate IntPtr MouseProc(int code, IntPtr message, IntPtr data);
        [StructLayout(LayoutKind.Sequential)] struct MouseData { public Point Position; public uint Mouse, Flags, Time; public UIntPtr Extra; }
        [DllImport("user32.dll", SetLastError = true)] static extern IntPtr SetWindowsHookEx(int kind, MouseProc callback, IntPtr module, uint thread);
        [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern IntPtr GetModuleHandle(string name);
        [DllImport("user32.dll")] static extern IntPtr CreatePopupMenu();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool AppendMenu(IntPtr menu, uint flags, UIntPtr id, string text);
        [DllImport("user32.dll")] static extern bool DestroyMenu(IntPtr menu);
        [DllImport("user32.dll")] static extern uint TrackPopupMenu(IntPtr menu, uint flags, int x, int y, int reserved, IntPtr owner, IntPtr rect);
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr window);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr window, int command);
        [DllImport("user32.dll")] static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
        bool capturedRight;
        volatile bool showingMenu;
        readonly ConcurrentQueue<Vector2> menuRequests = new ConcurrentQueue<Vector2>();
        public bool TryReadMenuRequest(out Vector2 point)
        { return menuRequests.TryDequeue(out point); }
        public void ClearButtonEvents()
        {
            while (buttons.TryDequeue(out _)) { }
            while (menuRequests.TryDequeue(out _)) { }
            // Capture belongs to the hook until its matching release, even after pause/cancel.
        }
        public DesktopMenuAction ShowPetMenu(Vector2 point, DesktopSettings settings)
        {
            IntPtr menu = CreatePopupMenu();
            if (menu == IntPtr.Zero) throw new InvalidOperationException("Cannot create pet menu.");
            int style = GetWindowLong(handle, -20);
            var previousWindow = GetForegroundWindow();
            showingMenu = true;
            try
            {
                AddMenu(menu, DesktopMenuAction.Settings, "Settings...");
                AddMenu(menu, DesktopMenuAction.Feed, "Feed: place a morsel...");
                AppendMenu(menu, 0x800, UIntPtr.Zero, null);
                AddMenu(menu, DesktopMenuAction.Small, "Small (1.0x)", Mathf.Approximately(settings.scale, 1));
                AddMenu(menu, DesktopMenuAction.Medium, "Medium (1.2x)", Mathf.Approximately(settings.scale, DesktopSettings.DefaultScale));
                AddMenu(menu, DesktopMenuAction.Large, "Large (1.5x)", Mathf.Approximately(settings.scale, 1.5f));
                AddMenu(menu, DesktopMenuAction.ExtraLarge, "Extra large (2.0x)", Mathf.Approximately(settings.scale, 2));
                AppendMenu(menu, 0x800, UIntPtr.Zero, null);
                AddMenu(menu, DesktopMenuAction.Roaming, "Roaming", settings.roam);
                AddMenu(menu, DesktopMenuAction.EdgesOnly, "Climbing: Edges only", settings.climbingMode == ClimbingMode.EdgesOnly);
                AddMenu(menu, DesktopMenuAction.EdgesAndBackgrounds, "Climbing: Edges + backgrounds", settings.climbingMode == ClimbingMode.EdgesAndBackgrounds);
                AddMenu(menu, DesktopMenuAction.ExploreClicks, "Explore clicks", settings.clickToExplore);
                AddMenu(menu, DesktopMenuAction.Sound, "Sound", settings.sound);
                AppendMenu(menu, 0x800, UIntPtr.Zero, null);
                AddMenu(menu, DesktopMenuAction.Stop, "Stop desktop pet");
                SetWindowLong(handle, -20, style & ~(ExNoActivate | ExTransparent));
                SetForegroundWindow(handle);
                return (DesktopMenuAction)TrackPopupMenu(menu, 0x100 | 0x80 | 0x2, (int)point.x, (int)point.y, 0, handle, IntPtr.Zero);
            }
            finally
            {
                PostMessage(handle, 0, IntPtr.Zero, IntPtr.Zero);
                SetWindowLong(handle, -20, style); DestroyMenu(menu); showingMenu = false;
                if (GetForegroundWindow() == handle && previousWindow != IntPtr.Zero) SetForegroundWindow(previousWindow);
            }
        }
        static void AddMenu(IntPtr menu, DesktopMenuAction action, string text, bool check = false)
        {
            if (!AppendMenu(menu, check ? 8u : 0u, new UIntPtr((uint)action), text))
                throw new InvalidOperationException("Cannot populate pet menu.");
        }
        public void ShowController()
        {
            EnumWindows((window, unused) =>
            {
                GetWindowThreadProcessId(window, out var pid);
                if (pid != controllerPid) return true;
                var name = new StringBuilder(64); GetClassName(window, name, name.Capacity);
                if (name.ToString() != "UnityWndClass") return true;
                ShowWindow(window, IsIconic(window) ? 9 : 5); SetForegroundWindow(window); return false;
            }, IntPtr.Zero);
        }
        public struct MouseButtonEvent { public Vector2 Position; public bool Down, ControllerFocused, Captured; public uint Time; }
        readonly ConcurrentQueue<MouseButtonEvent> buttons = new ConcurrentQueue<MouseButtonEvent>();
        int inputOverflow;
        MouseProc mouseCallback;
        IntPtr mouseHook;
        bool capturedButton;
        volatile DesktopHitSnapshot hitSnapshot = DesktopHitSnapshot.Empty;
        public void PublishHitSnapshot(DesktopHitSnapshot snapshot) { hitSnapshot = snapshot ?? DesktopHitSnapshot.Empty; }
        public bool ConsumeInputOverflow() => Interlocked.Exchange(ref inputOverflow, 0) != 0;
        public bool TryReadButton(out MouseButtonEvent value)
        {
            return buttons.TryDequeue(out value);
        }
        // No Unity, game reflection, rendering, or waits on the input thread.
        IntPtr MouseMessage(int code, IntPtr message, IntPtr data)
        {
            if(inputProbeEnabled && code>=0) Interlocked.Increment(ref inputHookProbeCount);
            long kind=message.ToInt64();
            if(code>=0 && !showingMenu && (kind==0x201 || kind==0x202 || kind==0x204 || kind==0x205))
            {
                try
                {
                    var mouse=Marshal.PtrToStructure<MouseData>(data);
                    if(QueueMouseButton((uint)kind,mouse.Position.X,mouse.Position.Y,mouse.Time,ControllerFocused)) return new IntPtr(1);
                }
                catch { Interlocked.Exchange(ref inputOverflow,1); }
            }
            return CallNextHookEx(mouseHook,code,message,data);
        }
        bool QueueMouseButton(uint kind,int x,int y,uint time,bool controllerFocused)
        {
            bool down=kind==0x201 || kind==0x204;
            bool right=kind==0x204 || kind==0x205;
            bool consume=right?capturedRight:capturedButton;
            if(down)
            {
                consume=(!right || !capturedButton) && hitSnapshot.Contains(x,y);
                if(right) capturedRight=consume; else capturedButton=consume;
            }
            if(right)
            {
                if(!down && capturedRight && menuRequests.IsEmpty) menuRequests.Enqueue(new Vector2(x,y));
                if(!down) capturedRight=false;
            }
            else
            {
                if(buttons.Count<128) buttons.Enqueue(new MouseButtonEvent { Position=new Vector2(x,y), Down=down, Time=time, ControllerFocused=controllerFocused, Captured=consume });
                else Interlocked.Exchange(ref inputOverflow,1);
                if(!down) capturedButton=false;
            }
            return consume;
        }
        [StructLayout(LayoutKind.Sequential)] struct NativeMessage
        { public IntPtr Window; public uint Message; public UIntPtr WParam; public IntPtr LParam; public uint Time; public Point Position; public uint Private; }
        [DllImport("user32.dll")] static extern int GetMessage(out NativeMessage message, IntPtr window, uint first, uint last);
        [DllImport("user32.dll")] static extern bool PeekMessage(out NativeMessage message, IntPtr window, uint first, uint last, uint remove);
        [DllImport("user32.dll")] static extern bool PostThreadMessage(uint thread, uint message, UIntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
        Thread mouseThread;
        uint mouseThreadId;
        readonly ManualResetEventSlim mouseReady = new ManualResetEventSlim(false);
        Exception mouseFailure;
        int inputProbeCount, inputHookProbeCount;
        bool inputProbeEnabled;
        public bool InputHealthy => mouseThread != null && mouseThread.IsAlive;
        volatile bool stopMouse;
        void StartMouseThread()
        {
            if (mouseThread != null) throw new InvalidOperationException("Mouse input already started.");
            stopMouse=false; mouseFailure=null; mouseReady.Reset();
            mouseThread = new Thread(MouseLoop) { IsBackground=true, Name="Lizor mouse input" };
            mouseThread.Start();
            if (!mouseReady.Wait(3000) || mouseFailure != null)
            {
                StopMouseThread();
                throw new InvalidOperationException("Cannot start desktop pet mouse input.", mouseFailure);
            }
        }
        void MouseLoop()
        {
            try
            {
                mouseThreadId=GetCurrentThreadId();
                PeekMessage(out _,IntPtr.Zero,0,0,0); // Create the queue before exposing readiness.
                mouseCallback=MouseMessage;
                mouseHook=SetWindowsHookEx(14,mouseCallback,GetModuleHandle(null),0);
                if(mouseHook==IntPtr.Zero) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                mouseReady.Set();
                while(!stopMouse)
                {
                    int result=GetMessage(out var message,IntPtr.Zero,0,0);
                    if(result<=0) break;
                    if(message.Message==0x8001) Interlocked.Increment(ref inputProbeCount);
                }
            }
            catch(Exception error) { mouseFailure=error; }
            finally
            {
                if(mouseHook!=IntPtr.Zero) { UnhookWindowsHookEx(mouseHook); mouseHook=IntPtr.Zero; }
                mouseReady.Set();
            }
        }
        void StopMouseThread()
        {
            if(mouseThread==null) return;
            stopMouse=true;
            if(mouseThreadId!=0) PostThreadMessage(mouseThreadId,0x12,UIntPtr.Zero,IntPtr.Zero);
            if(!mouseThread.Join(3000)) throw new InvalidOperationException("Mouse input thread did not stop.");
            mouseThread=null; mouseThreadId=0;
        }
        const int ExLayered = 0x80000, ExTransparent = 0x20, ExNoActivate = 0x08000000, ExToolWindow = 0x80;
        readonly int ownPid = Process.GetCurrentProcess().Id;
        readonly int controllerPid;
        IntPtr handle;
        int originalStyle, originalExStyle;
        bool clickThrough = true;
        public Rect WorkArea { get; private set; }
        public IntPtr WindowHandle => handle;
        static Rect Convert(NativeRect r) => Rect.MinMaxRect(r.Left, r.Top, r.Right, r.Bottom);
        public WindowsDesktop(int controllerPid)
        {
            this.controllerPid = controllerPid;
            if (!SystemParametersInfo(0x30, 0, out var area, 0)) throw new InvalidOperationException("Cannot read the primary monitor work area.");
            WorkArea = Convert(area);
        }
        public bool Initialize()
        {
            EnumWindows((window, unused) =>
            {
                GetWindowThreadProcessId(window, out var pid);
                var name = new StringBuilder(64); GetClassName(window, name, name.Capacity);
                if (pid != ownPid || name.ToString() != "UnityWndClass") return true;
                handle = window; return false;
            }, IntPtr.Zero);
            if (handle == IntPtr.Zero) return false;
            originalStyle = GetWindowLong(handle, -16); originalExStyle = GetWindowLong(handle, -20);
            SetWindowLong(handle, -16, unchecked((int)0x80000000));
            SetWindowLong(handle, -20, originalExStyle | ExLayered | ExTransparent | ExNoActivate | ExToolWindow);
            Resize();
            StartMouseThread();
            return true;
        }
        public bool RefreshWorkArea()
        {
            if (!SystemParametersInfo(0x30, 0, out var area, 0)) return false;
            var next = Convert(area); if (next == WorkArea) return false;
            WorkArea = next; Resize(); return true;
        }
        void Resize()
        {
            SetWindowPos(handle, new IntPtr(-1), (int)WorkArea.x, (int)WorkArea.y, (int)WorkArea.width, (int)WorkArea.height, 0x10 | 0x20 | 0x40);
        }
        public void SetClickThrough(bool value)
        {
            if (handle == IntPtr.Zero || value == clickThrough) return;
            clickThrough = value;
            int style = GetWindowLong(handle, -20);
            SetWindowLong(handle, -20, value ? style | ExTransparent : style & ~ExTransparent);
        }
        [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(Point point);
        [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr window, uint flags);
        [DllImport("user32.dll", SetLastError = true)] static extern IntPtr SendMessageTimeout(IntPtr window, uint message, IntPtr wParam, IntPtr lParam, uint flags, uint timeout, out IntPtr result);
        public bool IsWindowControl(Vector2 point)
        {
            var window = GetAncestor(WindowFromPoint(new Point { X = (int)point.x, Y = (int)point.y }), 2);
            if (window == IntPtr.Zero || window == handle) return false;
            int packed = ((int)point.x & 0xffff) | (((int)point.y & 0xffff) << 16);
            if (SendMessageTimeout(window, 0x84, IntPtr.Zero, new IntPtr(packed), 2, 20, out var hit) == IntPtr.Zero) return true;
            return hit.ToInt64() != 1; // HTCLIENT; caption, borders and system buttons are not invitations.
        }
        public Vector2 Cursor { get { GetCursorPos(out var p); return new Vector2(p.X, p.Y); } }
        public bool ControllerFocused
        { get { GetWindowThreadProcessId(GetForegroundWindow(), out var pid); return pid == controllerPid; } }
        public bool ControllerAlive
        { get { try { using (var process = Process.GetProcessById(controllerPid)) return !process.HasExited; } catch (ArgumentException) { return false; } } }
        public List<DesktopWindow> Snapshot(float scale)
        {
            var result = new List<DesktopWindow>();
            EnumWindows((window, unused) =>
            {
                GetWindowThreadProcessId(window, out var pid);
                if (pid == ownPid || pid == controllerPid || !IsWindowVisible(window) || IsIconic(window)) return true;
                if (GetCloaked(window, 14, out int cloaked, 4) == 0 && cloaked != 0) return true;
                if ((GetWindowLong(window, -20) & ExToolWindow) != 0) return true;
                var name = new StringBuilder(64); GetClassName(window, name, name.Capacity);
                if (name.ToString() == "Progman" || name.ToString() == "WorkerW" || name.ToString() == "Shell_TrayWnd") return true;
                if (GetFrame(window, 9, out var rect, 16) != 0 && !GetWindowRect(window, out rect)) return true;
                var bounds = Convert(rect);
                if (bounds.width >= 100 && bounds.height >= 60 && bounds.Overlaps(WorkArea))
                    result.Add(new DesktopWindow(window.ToInt64(), DesktopCoordinates.ToWorld(bounds, WorkArea, scale)));
                return true;
            }, IntPtr.Zero);
            return result;
        }
        public void Dispose()
        {
            StopMouseThread();
            hitSnapshot = DesktopHitSnapshot.Empty; ClearButtonEvents();
            if (handle == IntPtr.Zero) return;
            SetWindowLong(handle, -16, originalStyle); SetWindowLong(handle, -20, originalExStyle); handle = IntPtr.Zero;
        }
    }
}
