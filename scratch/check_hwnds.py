import ctypes
from ctypes import wintypes
import psutil

unity_pids = set()
for p in psutil.process_iter(['pid', 'name']):
    if p.info['name'] and 'unity' in p.info['name'].lower():
        unity_pids.add(p.info['pid'])

user32 = ctypes.windll.user32
WNDENUMPROC = ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)

found = []
def enum_cb(hwnd, lParam):
    pid = wintypes.DWORD()
    user32.GetWindowThreadProcessId(hwnd, ctypes.byref(pid))
    if pid.value in unity_pids:
        length = user32.GetWindowTextLengthW(hwnd)
        buff = ctypes.create_unicode_buffer(length + 1)
        user32.GetWindowTextW(hwnd, buff, length + 1)
        vis = user32.IsWindowVisible(hwnd)
        found.append((pid.value, hwnd, vis, buff.value))
    return True

user32.EnumWindows(WNDENUMPROC(enum_cb), 0)
for pid, hwnd, vis, title in found:
    print(f'PID {pid} Hwnd {hwnd}: visible={vis}, title="{title}"')
