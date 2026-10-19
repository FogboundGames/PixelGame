import ctypes
from ctypes import wintypes

user32 = ctypes.windll.user32

WNDENUMPROC = ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)

results = []

def enum_windows_callback(hwnd, lParam):
    if user32.IsWindowVisible(hwnd):
        length = user32.GetWindowTextLengthW(hwnd)
        if length > 0:
            buff = ctypes.create_unicode_buffer(length + 1)
            user32.GetWindowTextW(hwnd, buff, length + 1)
            pid = wintypes.DWORD()
            user32.GetWindowThreadProcessId(hwnd, ctypes.byref(pid))
            results.append((pid.value, buff.value, hwnd))
    return True

cb = WNDENUMPROC(enum_windows_callback)
user32.EnumWindows(cb, 0)

for pid, title, hwnd in results:
    if pid == 19892 or 'unity' in title.lower() or 'pixelgame' in title.lower() or 'gemi' in title.lower():
        print(f'PID {pid}: "{title}" (hwnd {hwnd})')
