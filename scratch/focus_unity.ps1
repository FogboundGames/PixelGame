$source = @"
using System;
using System.Runtime.InteropServices;
public class FocusUnity2 {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);
    [DllImport("user32.dll")] public static extern bool EnumThreadWindows(int dwThreadId, EnumThreadDelegate lpfn, IntPtr lParam);
    public delegate bool EnumThreadDelegate(IntPtr hWnd, IntPtr lParam);
    public static void BringToFront(int pid) {
        keybd_event(0x12, 0, 0, 0); // Alt down
        keybd_event(0x12, 0, 2, 0); // Alt up
        var p = System.Diagnostics.Process.GetProcessById(pid);
        foreach (System.Diagnostics.ProcessThread t in p.Threads) {
            EnumThreadWindows(t.Id, (h, l) => {
                ShowWindow(h, 9);
                SetForegroundWindow(h);
                return true;
            }, IntPtr.Zero);
        }
    }
}
"@
Add-Type -TypeDefinition $source
[FocusUnity2]::BringToFront(22776)
Write-Output "Brought to front"
