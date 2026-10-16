Add-Type @'
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

public class Win32Thread {
    public delegate bool EnumThreadDelegate(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumThreadWindows(int dwThreadId, EnumThreadDelegate lpfn, IntPtr lParam);
    [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
'@

$p = Get-Process -Name Unity -ErrorAction SilentlyContinue
if ($p) {
    foreach ($t in $p.Threads) {
        [Win32Thread]::EnumThreadWindows($t.Id, {
            param($hWnd, $lParam)
            $sb = New-Object System.Text.StringBuilder 512
            [Win32Thread]::GetWindowText($hWnd, $sb, 512)
            $title = $sb.ToString()
            $vis = [Win32Thread]::IsWindowVisible($hWnd)
            if ($title.Length -gt 0) {
                Write-Output "Thread $($t.Id) Window: '$title' Vis: $vis Hwnd: $hWnd"
                [Win32Thread]::ShowWindow($hWnd, 9)
                [Win32Thread]::SetForegroundWindow($hWnd)
            }
            return $true
        }, [IntPtr]::Zero)
    }
}
