using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace vs_android.Build.CPPTasks.Android
{
    internal static class VCTaskNativeMethods
    {
        // Methods
        [DllImport("KERNEL32.DLL", SetLastError = true)]
        internal static extern bool CloseHandle(IntPtr hObject);
        [DllImport("KERNEL32.DLL", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern IntPtr CreateEventW(IntPtr lpEventAttributes, bool bManualReset, bool bInitialState, string lpName);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern uint SearchPath(string lpPath, string lpFileName, string lpExtension, int nBufferLength, [MarshalAs(UnmanagedType.LPTStr)] StringBuilder lpBuffer, out IntPtr lpFilePart);
        [DllImport("KERNEL32.DLL", SetLastError = true)]
        internal static extern bool SetEvent(IntPtr hEvent);
    }

 

}
