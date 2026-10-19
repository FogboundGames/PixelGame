import ctypes
from ctypes import wintypes
import subprocess
import time
import os

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

c_enum_cb = WNDENUMPROC(enum_cb)
for pid in pids:
    out_threads = subprocess.check_output(['powershell', '-NoProfile', '-Command', f'(Get-Process -Id {pid}).Threads.Id'], text=True)
    for tid in [int(x.strip()) for x in out_threads.split() if x.strip().isdigit()]:
        user32.EnumThreadWindows(tid, c_enum_cb, 0)
        if unity_hwnd: break
    if unity_hwnd: break

print('Unity HWND:', unity_hwnd)
if not unity_hwnd:
    print('Unity window not found!')
    exit(1)

user32.ShowWindow(unity_hwnd, 9)
user32.SetForegroundWindow(unity_hwnd)
user32.SendMessageW(unity_hwnd, 0x0006, 1, 0)
user32.SendMessageW(unity_hwnd, 0x0007, 0, 0)

# Refresh to trigger recompile if needed
user32.SendMessageW(unity_hwnd, 0x0111, 40280, 0)
time.sleep(5)

hmenu = user32.GetMenu(unity_hwnd)
pixel_menu = None
if hmenu:
    for i in range(user32.GetMenuItemCount(hmenu)):
        buf = ctypes.create_unicode_buffer(128)
        user32.GetMenuStringW(hmenu, i, buf, 128, 0x0400)
        if 'PixelGame' in buf.value:
            pixel_menu = user32.GetSubMenu(hmenu, i)
            break

target_id = None
if pixel_menu:
    for i in range(user32.GetMenuItemCount(pixel_menu)):
        buf = ctypes.create_unicode_buffer(128)
        user32.GetMenuStringW(pixel_menu, i, buf, 128, 0x0400)
        cmd_id = user32.GetMenuItemID(pixel_menu, i)
        val = buf.value.encode('ascii', 'replace').decode('ascii')
        print(f'[{i}] ID={cmd_id}: {val}')
        if 'Linked' in val:
            target_id = cmd_id

if target_id:
    print(f'Triggering command ID={target_id}...')
    user32.SendMessageW(unity_hwnd, 0x0111, target_id, 0)
    time.sleep(4)
    if os.path.exists('scratch/gameplay_view_9_16.png'):
        mtime = os.path.getmtime('scratch/gameplay_view_9_16.png')
        print(f'Screenshot saved! Modified: {time.ctime(mtime)}')
else:
    print('Linked menu item not found in PixelGame menu!')
