Add-Type @'
using System;
using System.Runtime.InteropServices;
public class WinUtil {
    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
'@

$p = Get-Process -Name 'Unity' -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 }
if ($p) {
    [WinUtil]::ShowWindow($p[0].MainWindowHandle, 9)
    [WinUtil]::SetForegroundWindow($p[0].MainWindowHandle)
    Write-Output "Brought Unity window to front: $($p[0].Id)"
} else {
    Write-Output 'No Unity window handle found'
}
