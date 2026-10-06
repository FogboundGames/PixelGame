Add-Type @'
  using System;
  using System.Runtime.InteropServices;
  using System.Text;
  public class WinApiHelper {
    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    public static void FocusUnity() {
        EnumWindows((hWnd, lParam) => {
            StringBuilder sb = new StringBuilder(256);
            GetWindowText(hWnd, sb, 256);
            string title = sb.ToString();
            uint pid;
            GetWindowThreadProcessId(hWnd, out pid);
            if (!string.IsNullOrEmpty(title) && (title.Contains("Unity") || title.Contains("PixelGame"))) {
                Console.WriteLine("Found: " + pid + " - " + title);
                SetForegroundWindow(hWnd);
            }
            return true;
        }, IntPtr.Zero);
    }
  }
'@

[WinApiHelper]::FocusUnity()
