Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;

public class WinFind {
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll")]
    public static extern IntPtr GetMenu(IntPtr hWnd);
    [DllImport("user32.dll")]
    public static extern IntPtr GetSubMenu(IntPtr hMenu, int nPos);
    [DllImport("user32.dll")]
    public static extern int GetMenuItemCount(IntPtr hMenu);
    [DllImport("user32.dll")]
    public static extern uint GetMenuItemID(IntPtr hMenu, int nPos);
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern int GetMenuString(IntPtr hMenu, uint uIDItem, StringBuilder lpString, int nMaxCount, uint uFlag);
    [DllImport("user32.dll")]
    public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    public static IntPtr FoundHwnd = IntPtr.Zero;
    public static IntPtr FoundMenu = IntPtr.Zero;

    public static bool FindUnityAndTrigger() {
        FoundHwnd = IntPtr.Zero;
        FoundMenu = IntPtr.Zero;

        EnumWindows((hWnd, lParam) => {
            uint pid;
            GetWindowThreadProcessId(hWnd, out pid);
            if (pid == 23068) {
                IntPtr menu = GetMenu(hWnd);
                if (menu != IntPtr.Zero) {
                    FoundHwnd = hWnd;
                    FoundMenu = menu;
                    return false;
                }
            }
            return true;
        }, IntPtr.Zero);

        if (FoundHwnd == IntPtr.Zero || FoundMenu == IntPtr.Zero) {
            Console.WriteLine("Could not find Unity window with menu.");
            return false;
        }

        Console.WriteLine("Found Unity HWND: " + FoundHwnd + " Menu: " + FoundMenu);
        int count = GetMenuItemCount(FoundMenu);
        for (int i = 0; i < count; i++) {
            StringBuilder sb = new StringBuilder(256);
            GetMenuString(FoundMenu, (uint)i, sb, 256, 0x400);
            string title = sb.ToString();
            if (title.Contains("PixelGame")) {
                IntPtr sub = GetSubMenu(FoundMenu, i);
                int subCount = GetMenuItemCount(sub);
                for (int j = 0; j < subCount; j++) {
                    StringBuilder sbSub = new StringBuilder(256);
                    GetMenuString(sub, (uint)j, sbSub, 256, 0x400);
                    uint cmdId = GetMenuItemID(sub, j);
                    string itemText = sbSub.ToString();
                    if (itemText.Contains("Gemi Ekran")) {
                        Console.WriteLine("Posting command " + cmdId + " (" + itemText + ") to HWND " + FoundHwnd);
                        PostMessage(FoundHwnd, 0x0111, (IntPtr)cmdId, IntPtr.Zero);
                        return true;
                    }
                }
            }
        }
        return false;
    }
}
'@

[WinFind]::FindUnityAndTrigger()
Start-Sleep -Seconds 3
Get-Item scratch/gameplay_view_9_16.png | Select-Object LastWriteTime
