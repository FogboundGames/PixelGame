Add-Type @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

public class WinFinderUnity {
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
}
'@

$unityProcs = Get-Process Unity -ErrorAction SilentlyContinue
foreach ($p in $unityProcs) {
    [WinFinderUnity]::EnumWindows({
        param($hWnd, $lParam)
        $pidOut = [uint32]0
        [WinFinderUnity]::GetWindowThreadProcessId($hWnd, [ref]$pidOut)
        if ($pidOut -eq $p.Id) {
            $sb = New-Object System.Text.StringBuilder 256
            [WinFinderUnity]::GetWindowText($hWnd, $sb, 256)
            $title = $sb.ToString()
            if ($title.Length -gt 0 -and ($title.Contains("Unity") -or $title.Contains("PixelGame") -or $title.Contains("Gemi"))) {
                Write-Output "Found Unity window: $title ($hWnd)"
                [WinFinderUnity]::ShowWindow($hWnd, 9)
                [WinFinderUnity]::SetForegroundWindow($hWnd)
            }
        }
        return $true
    }, [IntPtr]::Zero)
}
