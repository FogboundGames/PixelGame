$code = @"
using System;
using System.Runtime.InteropServices;
public class WinSwitch {
    [DllImport("user32.dll")] public static extern void SwitchToThisWindow(IntPtr hWnd, bool fAltTab);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll")] public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    public static void FocusAndRefresh(uint targetPid) {
        EnumWindows((hWnd, lParam) => {
            uint pid;
            GetWindowThreadProcessId(hWnd, out pid);
            if (pid == targetPid) {
                SwitchToThisWindow(hWnd, true);
                Console.WriteLine("Switched to HWND: " + hWnd);
            }
            return true;
        }, IntPtr.Zero);

        System.Threading.Thread.Sleep(400);
        keybd_event(0x11, 0, 0, 0); // Ctrl down
        keybd_event(0x52, 0, 0, 0); // R down
        keybd_event(0x52, 0, 2, 0); // R up
        keybd_event(0x11, 0, 2, 0); // Ctrl up
        Console.WriteLine("Sent Ctrl+R");
    }
}
"@
Add-Type -TypeDefinition $code
[WinSwitch]::FocusAndRefresh(21784)
