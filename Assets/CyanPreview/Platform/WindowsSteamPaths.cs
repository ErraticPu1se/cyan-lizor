using System;
using System.Runtime.InteropServices;
using System.Text;

namespace CyanPreview
{
    public static class WindowsSteamPaths
    {
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        static extern int RegGetValue(IntPtr key, string subKey, string value, uint flags, out uint type, StringBuilder data, ref uint size);
        static string Read(IntPtr hive, string key, string name)
        {
            var value = new StringBuilder(32768);
            uint bytes = (uint)value.Capacity * 2;
            return RegGetValue(hive, key, name, 2, out _, value, ref bytes) == 0 ? value.ToString() : null;
        }
        public static string CurrentUser() => Read(new IntPtr(unchecked((int)0x80000001)), @"Software\Valve\Steam", "SteamPath");
        public static string Machine() => Read(new IntPtr(unchecked((int)0x80000002)), @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath");
    }
}
