using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
namespace CyanPreview
{
    public static class LegacyPreferences
    {
        [DllImport("advapi32.dll", CharSet=CharSet.Unicode)] static extern int RegOpenKeyEx(IntPtr hive,string path,uint options,uint access,out IntPtr key);
        [DllImport("advapi32.dll", CharSet=CharSet.Unicode)] static extern int RegEnumValue(IntPtr key,uint index,StringBuilder name,ref uint length,IntPtr reserved,out uint type,IntPtr data,IntPtr size);
        [DllImport("advapi32.dll", CharSet=CharSet.Unicode)] static extern int RegQueryValueEx(IntPtr key,string name,IntPtr reserved,out uint type,byte[] data,ref uint size);
        [DllImport("advapi32.dll")] static extern int RegCloseKey(IntPtr key);
        public static string Read(string preference)
        {
            int result=RegOpenKeyEx(new IntPtr(unchecked((int)0x80000001)),@"Software\ErraticPu1se\Cyan Lizor Preview",0,1,out var key);
            if(result==2) return null;
            if(result!=0) throw new Win32Exception(result,"Cannot read previous Lizor settings.");
            try
            {
                for(uint index=0;;index++)
                {
                    var name=new StringBuilder(512); uint length=512;
                    result=RegEnumValue(key,index,name,ref length,IntPtr.Zero,out _,IntPtr.Zero,IntPtr.Zero);
                    if(result==259) return null;
                    if(result!=0) throw new Win32Exception(result);
                    if(!name.ToString().StartsWith(preference+"_h",StringComparison.Ordinal)) continue;
                    uint size=0;
                    result=RegQueryValueEx(key,name.ToString(),IntPtr.Zero,out uint type,null,ref size);
                    if(result!=0 || size>1024*1024) throw new InvalidOperationException("Previous preference cannot be read safely.");
                    var bytes=new byte[size];
                    result=RegQueryValueEx(key,name.ToString(),IntPtr.Zero,out type,bytes,ref size);
                    if(result!=0) throw new Win32Exception(result);
                    if(type!=1 && type!=3) throw new InvalidOperationException("Unexpected preference format.");
                    return (type==1?Encoding.Unicode:Encoding.UTF8).GetString(bytes,0,(int)size).TrimEnd('\0');
                }
            }
            finally { RegCloseKey(key); }
        }
    }
}
