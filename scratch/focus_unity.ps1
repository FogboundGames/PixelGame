Add-Type @"
using System;
using System.Runtime.InteropServices;
public class WinUtil {
    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);
}
"@

$procs = Get-Process Unity -ErrorAction SilentlyContinue
foreach ($p in $procs) {
    if ($p.MainWindowHandle -ne [IntPtr]::Zero) {
        Write-Host "Found Unity window handle: $($p.MainWindowHandle)"
        [WinUtil]::SetForegroundWindow($p.MainWindowHandle)
        Start-Sleep -Milliseconds 500
    }
}
