import ctypes
from ctypes import wintypes
import time

user32 = ctypes.windll.user32
unity_pids = [23024, 20008, 28808]

found_hwnds = []

def enum_cb(hwnd, lparam):
    pid = wintypes.DWORD()
    user32.GetWindowThreadProcessId(hwnd, ctypes.byref(pid))
    if pid.value in unity_pids:
        length = user32.GetWindowTextLengthW(hwnd)
        buff = ctypes.create_unicode_buffer(length + 1)
        user32.GetWindowTextW(hwnd, buff, length + 1)
        found_hwnds.append((hwnd, pid.value, buff.value))
    return True

CB_TYPE = ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)
user32.EnumWindows(CB_TYPE(enum_cb), 0)

for h, pid, t in found_hwnds:
    print(f"HWND: {hex(h)}, PID: {pid}, Title: '{t}'")
    if t:
        # Attempt to activate and bring to front
        user32.ShowWindow(h, 9) # SW_RESTORE
        user32.SetForegroundWindow(h)
        print("Restored & set foreground:", hex(h))
