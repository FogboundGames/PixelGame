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

    public static List<IntPtr> FindWindowsForPids(uint[] targetPids) {
        var set = new HashSet<uint>(targetPids);
        var result = new List<IntPtr>();
        EnumWindows((hWnd, lParam) => {
            uint pid;
            GetWindowThreadProcessId(hWnd, out pid);
            if (set.Contains(pid)) {
                result.Add(hWnd);
            }
            return true;
        }, IntPtr.Zero);
        return result;
    }
}
'@

$pids = [uint32[]](Get-Process Unity | ForEach-Object { $_.Id })
$hwnds = [WinFinder]::FindWindowsForPids($pids)
Write-Output "Found $($hwnds.Count) windows for Unity processes"
foreach ($h in $hwnds) {
    $sb = New-Object System.Text.StringBuilder 256
    [WinFinder]::GetWindowText($h, $sb, 256)
    $title = $sb.ToString()
    $visible = [WinFinder]::IsWindowVisible($h)
    if ($title.Length -gt 0 -and $visible) {
        Write-Output "Activating HWND: $h, Title: $title"
        [WinFinder]::ShowWindow($h, 9)
        [WinFinder]::SetForegroundWindow($h)
    }
}
