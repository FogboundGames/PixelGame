import ctypes
from ctypes import wintypes
import time
import subprocess

user32 = ctypes.windll.user32

hwinsta = user32.OpenWindowStationW('WinSta0', False, 0x037F)
user32.SetProcessWindowStation(hwinsta)
hdesk = user32.OpenDesktopW('default', 0, False, 0x01FF)
user32.SetThreadDesktop(hdesk)

out = subprocess.check_output(['powershell', '-NoProfile', '-Command', '(Get-Process -Name Unity).Id'], text=True)
pids = [int(x.strip()) for x in out.split() if x.strip().isdigit()]

unity_hwnd = None
def enum_cb(hwnd, lp):
    global unity_hwnd
    l = user32.GetWindowTextLengthW(hwnd)
    if l > 0:
        b = ctypes.create_unicode_buffer(l + 1)
        user32.GetWindowTextW(hwnd, b, l + 1)
        if 'Unity' in b.value and 'PixelGame' in b.value:
            unity_hwnd = hwnd
    return True

WNDENUMPROC = ctypes.WINFUNCTYPE(ctypes.wintypes.BOOL, ctypes.wintypes.HWND, ctypes.wintypes.LPARAM)
for pid in pids:
    out_threads = subprocess.check_output(['powershell', '-NoProfile', '-Command', f'(Get-Process -Id {pid}).Threads.Id'], text=True)
    thread_ids = [int(x.strip()) for x in out_threads.split() if x.strip().isdigit()]
    for tid in thread_ids:
        user32.EnumThreadWindows(tid, WNDENUMPROC(enum_cb), 0)
        if unity_hwnd: break
    if unity_hwnd: break

print('Unity HWND:', unity_hwnd)

# Bring window to front
user32.ShowWindow(unity_hwnd, 9) # SW_RESTORE
user32.SetForegroundWindow(unity_hwnd)
time.sleep(0.5)

# Simulate Ctrl+R
VK_CONTROL = 0x11
VK_R = 0x52
user32.keybd_event(VK_CONTROL, 0, 0, 0)
time.sleep(0.05)
user32.keybd_event(VK_R, 0, 0, 0)
time.sleep(0.05)
user32.keybd_event(VK_R, 0, 2, 0) # KEYEVENTF_KEYUP
time.sleep(0.05)
user32.keybd_event(VK_CONTROL, 0, 2, 0)

print('Sent Ctrl+R. Waiting 10 seconds...')
time.sleep(10)

import os
mtime = os.path.getmtime('Library/ScriptAssemblies/Assembly-CSharp.dll')
print('Assembly-CSharp.dll mtime now:', time.ctime(mtime))
