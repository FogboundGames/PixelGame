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
    for tid in [int(x.strip()) for x in subprocess.check_output(['powershell', '-NoProfile', '-Command', f'(Get-Process -Id {pid}).Threads.Id'], text=True).split() if x.strip().isdigit()]:
        user32.EnumThreadWindows(tid, c_enum_cb, 0)
        if unity_hwnd:
            break
    if unity_hwnd:
        break

if not unity_hwnd:
    print('Unity window not found!')
    exit(1)

# Trigger Refresh
print('Triggering Refresh...')
user32.SendMessageW(unity_hwnd, 0x0111, 40276, 0)
time.sleep(7)

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
    count = user32.GetMenuItemCount(pixel_menu)
    for i in range(count):
        buf = ctypes.create_unicode_buffer(128)
        user32.GetMenuStringW(pixel_menu, i, buf, 128, 0x0400)
        cmd_id = user32.GetMenuItemID(pixel_menu, i)
        val = buf.value.encode('ascii', 'ignore').decode('ascii')
        if 'Apply Complete Cargo Container' in val:
            target_id = cmd_id
            print(f'Found menu item: [{i}] ID={cmd_id}: {val}')
            break

if target_id:
    print(f'Sending command {target_id}...')
    user32.SendMessageW(unity_hwnd, 0x0111, target_id, 0)
    time.sleep(4)
    print('Command executed and layout applied!')
