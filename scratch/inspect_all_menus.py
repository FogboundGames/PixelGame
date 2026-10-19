import ctypes
from ctypes import wintypes
import subprocess

user32 = ctypes.windll.user32
WNDENUMPROC = ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)

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

for pid in pids:
    out_threads = subprocess.check_output(['powershell', '-NoProfile', '-Command', f'(Get-Process -Id {pid}).Threads.Id'], text=True)
    thread_ids = [int(x.strip()) for x in out_threads.split() if x.strip().isdigit()]
    for tid in thread_ids:
        user32.EnumThreadWindows(tid, WNDENUMPROC(enum_cb), 0)
        if unity_hwnd: break
    if unity_hwnd: break

hmenu = user32.GetMenu(unity_hwnd)
count = user32.GetMenuItemCount(hmenu)
for i in range(count):
    buf = ctypes.create_unicode_buffer(128)
    user32.GetMenuStringW(hmenu, i, buf, 128, 0x0400)
    name = buf.value
    sub = user32.GetSubMenu(hmenu, i)
    if sub and name in ['File', 'Edit', 'Tools', 'Window']:
        print(f"=== {name} ===")
        sub_count = user32.GetMenuItemCount(sub)
        for j in range(sub_count):
            sub_buf = ctypes.create_unicode_buffer(128)
            user32.GetMenuStringW(sub, j, sub_buf, 128, 0x0400)
            cmd_id = user32.GetMenuItemID(sub, j)
            val = sub_buf.value.encode('ascii', 'replace').decode('ascii')
            print(f"  [{j}] ID={cmd_id}: {val}")
