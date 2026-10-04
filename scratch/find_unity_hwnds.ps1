Add-Type @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

public class WinFinder {
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    public static List<IntPtr> FindWindowsForPid(uint targetPid) {
        var result = new List<IntPtr>();
        EnumWindows((hWnd, lParam) => {
            uint pid;
            GetWindowThreadProcessId(hWnd, out pid);
            if (pid == targetPid) {
                result.Add(hWnd);
            }
            return true;
        }, IntPtr.Zero);
        return result;
    }
}
'@

$hwnds = [WinFinder]::FindWindowsForPid(18544)
Write-Output "Found $($hwnds.Count) windows for PID 18544"
foreach ($h in $hwnds) {
    $sb = New-Object System.Text.StringBuilder 256
    [WinFinder]::GetWindowText($h, $sb, 256)
    $title = $sb.ToString()
    $visible = [WinFinder]::IsWindowVisible($h)
    Write-Output "HWND: $h, Visible: $visible, Title: $title"
    if ($visible -or $title.Length -gt 0) {
        [WinFinder]::ShowWindow($h, 9)
        [WinFinder]::SetForegroundWindow($h)
        Write-Output "Set foreground on $h"
    }
}
