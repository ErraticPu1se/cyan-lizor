using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CyanPreview
{
    public static class WindowsFolderPicker
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct BrowseInfo
        {
            public IntPtr owner, root, displayName;
            [MarshalAs(UnmanagedType.LPWStr)] public string title;
            public uint flags;
            public IntPtr callback, data;
            public int image;
        }
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SHBrowseForFolder(ref BrowseInfo info);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern bool SHGetPathFromIDList(IntPtr item, StringBuilder path);
        [DllImport("ole32.dll")] static extern int OleInitialize(IntPtr reserved);
        [DllImport("ole32.dll")] static extern void OleUninitialize();
        [DllImport("user32.dll")] static extern IntPtr GetActiveWindow();

        public static Task<string> Browse()
        {
            var completion = new TaskCompletionSource<string>();
            IntPtr owner = GetActiveWindow();
            var thread = new Thread(() =>
            {
                bool initialized = false;
                IntPtr item = IntPtr.Zero, display = IntPtr.Zero;
                try
                {
                    int hr = OleInitialize(IntPtr.Zero);
                    if (hr < 0) Marshal.ThrowExceptionForHR(hr);
                    initialized = true; display = Marshal.AllocCoTaskMem(520);
                    var info = new BrowseInfo { owner = owner, displayName = display, title = "Select Rain World (the folder containing RainWorld_Data)", flags = 0x41 };
                    item = SHBrowseForFolder(ref info);
                    if (item == IntPtr.Zero) { completion.SetResult(null); return; }
                    var path = new StringBuilder(260);
                    if (!SHGetPathFromIDList(item, path)) throw new InvalidOperationException("Choose a local game installation folder.");
                    completion.SetResult(path.ToString());
                }
                catch (Exception e) { completion.SetException(e); }
                finally
                {
                    if (item != IntPtr.Zero) Marshal.FreeCoTaskMem(item);
                    if (display != IntPtr.Zero) Marshal.FreeCoTaskMem(display);
                    if (initialized) OleUninitialize();
                }
            });
            thread.IsBackground = true; thread.SetApartmentState(ApartmentState.STA); thread.Start();
            return completion.Task;
        }
    }
}
