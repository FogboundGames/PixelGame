$code = @"
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
public class AllWin {
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder text, int count);
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    public static void List() {
        EnumWindows((hWnd, lParam) => {
            uint pid;
            GetWindowThreadProcessId(hWnd, out pid);
            var sb = new System.Text.StringBuilder(256);
            GetWindowText(hWnd, sb, 256);
            string t = sb.ToString();
            if (t.Length > 0) {
                try {
                    var proc = Process.GetProcessById((int)pid);
                    if (proc.ProcessName.ToLower().Contains("unity")) {
                        Console.WriteLine("PID: " + pid + " HWND: " + hWnd + " Title: " + t);
                    }
                } catch {}
            }
            return true;
        }, IntPtr.Zero);
    }
}
"@
Add-Type -TypeDefinition $code
[AllWin]::List()
