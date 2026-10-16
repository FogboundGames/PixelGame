Add-Type @'
using System;
using System.Runtime.InteropServices;
using System.Text;

public class WinHelper {
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);
    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll")]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
'@

[WinHelper]::EnumWindows({
    param($hWnd, $lParam)
    $pidOut = [uint32]0
    [WinHelper]::GetWindowThreadProcessId($hWnd, [ref]$pidOut)
    if ($pidOut -eq 7016) {
        $sb = New-Object System.Text.StringBuilder 256
        [WinHelper]::GetWindowText($hWnd, $sb, 256)
        $t = $sb.ToString()
        if ($t.Length -gt 0) {
            Write-Output "PID 7016 Window: $t (Handle: $hWnd)"
            [WinHelper]::ShowWindow($hWnd, 9)
            [WinHelper]::SetForegroundWindow($hWnd)
        }
    }
    return $true
}, [IntPtr]::Zero)
