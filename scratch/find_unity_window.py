import ctypes
from ctypes import wintypes
import subprocess
import time

user32 = ctypes.windll.user32
WNDENUMPROC = ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)

out = subprocess.check_output(['powershell', '-NoProfile', '-Command', '(Get-Process -Id 23132).Threads.Id'], text=True)
thread_ids = [int(x.strip()) for x in out.split() if x.strip().isdigit()]
print('Thread count:', len(thread_ids))

found = []
def th_cb(hwnd, lp):
    l = user32.GetWindowTextLengthW(hwnd)
    b = ctypes.create_unicode_buffer(l + 1)
    user32.GetWindowTextW(hwnd, b, l + 1)
    found.append((hwnd, b.value))
    return True

c_th_cb = WNDENUMPROC(th_cb)
for tid in thread_ids:
    user32.EnumThreadWindows(tid, c_th_cb, 0)

for h, title in found:
    if title:
        print(f'HWND {h}: {title}')
        user32.ShowWindow(h, 9) # SW_RESTORE
        user32.SetForegroundWindow(h)

time.sleep(1)
