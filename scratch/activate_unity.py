import ctypes
from ctypes import wintypes
import time

user32 = ctypes.windll.user32
hDesk = user32.OpenInputDesktop(0, False, 0x01FF)

unity_hwnd = None
def enum_cb(hwnd, lparam):
    global unity_hwnd
    length = user32.GetWindowTextLengthW(hwnd)
    if length > 0:
        buff = ctypes.create_unicode_buffer(length + 1)
        user32.GetWindowTextW(hwnd, buff, length + 1)
        t = buff.value
        if 'Unity 6' in t and 'PixelGame' in t:
            unity_hwnd = hwnd
            return False
    return True

DESKTOPENUMPROC = ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)
user32.EnumDesktopWindows(hDesk, DESKTOPENUMPROC(enum_cb), 0)

if unity_hwnd:
    print(f"Found Unity window: {hex(unity_hwnd)}")
    # AttachThreadInput to ensure SetForegroundWindow works
    cur_thread = ctypes.windll.kernel32.GetCurrentThreadId()
    win_thread = user32.GetWindowThreadProcessId(unity_hwnd, None)
    user32.AttachThreadInput(cur_thread, win_thread, True)
    
    user32.ShowWindow(unity_hwnd, 9) # SW_RESTORE
    user32.SetForegroundWindow(unity_hwnd)
    user32.SetFocus(unity_hwnd)
    
    user32.AttachThreadInput(cur_thread, win_thread, False)
    print("Activated Unity window!")
else:
    print("Unity window not found")
