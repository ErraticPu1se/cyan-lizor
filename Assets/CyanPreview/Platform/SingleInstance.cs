using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
namespace CyanPreview
{
    public sealed class SingleInstance : IDisposable
    {
        [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
        static extern IntPtr CreateMutex(IntPtr attributes, bool owner, string name);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
        IntPtr mutex;
        public bool IsPrimary { get; }
        public SingleInstance(string name = @"Local\ErraticPu1se.Lizor.Controller")
        {
            // Unity's standalone Mono profile does not support named managed mutexes.
            // The handle's lifetime, rather than thread ownership, identifies the instance.
            mutex = CreateMutex(IntPtr.Zero, false, name);
            int error = Marshal.GetLastWin32Error();
            if (mutex == IntPtr.Zero) throw new Win32Exception(error);
            IsPrimary = error != 183; // ERROR_ALREADY_EXISTS
        }
        public void Dispose()
        {
            if (mutex == IntPtr.Zero) return;
            CloseHandle(mutex); mutex = IntPtr.Zero;
        }
    }
}
