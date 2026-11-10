using System;
using System.Collections.Generic;
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
        [DllImport("user32.dll")] static extern bool GetCursorPos(out Point point);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern bool SystemParametersInfo(uint action, uint param, out NativeRect rect, uint flags);
        delegate IntPtr MouseProc(int code, IntPtr message, IntPtr data);
        [StructLayout(LayoutKind.Sequential)] struct MouseData { public Point Position; public uint Mouse, Flags, Time; public UIntPtr Extra; }
        [DllImport("user32.dll", SetLastError = true)] static extern IntPtr SetWindowsHookEx(int kind, MouseProc callback, IntPtr module, uint thread);
        [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern IntPtr GetModuleHandle(string name);
        public struct MouseButtonEvent { public Vector2 Position; public bool Down, ControllerFocused; public uint Time; }
        readonly Queue<MouseButtonEvent> buttons = new Queue<MouseButtonEvent>();
        MouseProc mouseCallback;
        IntPtr mouseHook;
        bool capturedButton;
        public Func<Vector2, bool> HitTest { private get; set; }
        public bool TryReadButton(out MouseButtonEvent value)
        {
            if (buttons.Count == 0) { value = default; return false; }
            value = buttons.Dequeue(); return true;
        }
        // Keep this callback short: queue button events only; simulate in Update.
        IntPtr MouseMessage(int code, IntPtr message, IntPtr data)
        {
            if (code >= 0 && (message.ToInt64() == 0x201 || message.ToInt64() == 0x202))
            {
                var mouse = Marshal.PtrToStructure<MouseData>(data);
                var point = new Vector2(mouse.Position.X, mouse.Position.Y);
                bool down = message.ToInt64() == 0x201;
                bool consume = capturedButton;
                try
                {
                    if (down) consume = capturedButton = WorkArea.Contains(point) && (HitTest?.Invoke(point) ?? false);
                    buttons.Enqueue(new MouseButtonEvent { Position = point, Down = down, Time = mouse.Time, ControllerFocused = ControllerFocused });
                }
                catch { capturedButton = false; consume = false; }
                if (!down) capturedButton = false;
                if (consume) return new IntPtr(1);
            }
            return CallNextHookEx(mouseHook, code, message, data);
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
            mouseCallback = MouseMessage;
            mouseHook = SetWindowsHookEx(14, mouseCallback, GetModuleHandle(null), 0);
            if (mouseHook == IntPtr.Zero) throw new InvalidOperationException("Cannot capture desktop pet mouse presses.");
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
            if (mouseHook != IntPtr.Zero) { UnhookWindowsHookEx(mouseHook); mouseHook = IntPtr.Zero; }
            HitTest = null; buttons.Clear();
            if (handle == IntPtr.Zero) return;
            SetWindowLong(handle, -16, originalStyle); SetWindowLong(handle, -20, originalExStyle); handle = IntPtr.Zero;
        }
    }
}
