Add-Type @'
  using System;
  using System.Runtime.InteropServices;
  using System.Text;
  public class WinApi {
    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);
  }
'@

[WinApi]::EnumWindows({
  param($hWnd, $lParam)
  $sb = New-Object System.Text.StringBuilder 256
  [WinApi]::GetWindowText($hWnd, $sb, 256) | Out-Null
  $title = $sb.ToString()
  $pid = 0
  [WinApi]::GetWindowThreadProcessId($hWnd, [ref]$pid) | Out-Null
  if ($title -like "*Unity*" -or $title -like "*PixelGame*") {
    Write-Host "Found: PID=$pid, HWND=$hWnd, Title=$title"
    [WinApi]::SetForegroundWindow($hWnd)
  }
  return $true
}, [IntPtr]::Zero)
