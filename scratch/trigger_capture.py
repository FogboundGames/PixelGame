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

# Get all Unity process PIDs
out = subprocess.check_output(['powershell', '-NoProfile', '-Command', '(Get-Process -Name Unity).Id'], text=True)
pids = [int(x.strip()) for x in out.split() if x.strip().isdigit()]
print('Unity PIDs:', pids)

unity_hwnd = None
def enum_cb(hwnd, lp):
    global unity_hwnd
    l = user32.GetWindowTextLengthW(hwnd)
    if l > 0:
        b = ctypes.create_unicode_buffer(l + 1)
        user32.GetWindowTextW(hwnd, b, l + 1)
        title = b.value
        if 'Unity' in title and 'PixelGame' in title:
            unity_hwnd = hwnd
            print(f'Found Unity Window HWND: {hwnd} - {title}')
    return True

c_enum_cb = WNDENUMPROC(enum_cb)
for pid in pids:
    out_threads = subprocess.check_output(['powershell', '-NoProfile', '-Command', f'(Get-Process -Id {pid}).Threads.Id'], text=True)
    thread_ids = [int(x.strip()) for x in out_threads.split() if x.strip().isdigit()]
    for tid in thread_ids:
        user32.EnumThreadWindows(tid, c_enum_cb, 0)
        if unity_hwnd:
            break
    if unity_hwnd:
        break

if not unity_hwnd:
    print('Unity window not found!')
    exit(1)

print(f'Using HWND: {unity_hwnd}')

# 1. Trigger Refresh
print('Triggering Assets -> Refresh (ID=40276)...')
user32.SendMessageW(unity_hwnd, 0x0111, 40276, 0)
time.sleep(6)

# 2. Find Menu item
hmenu = user32.GetMenu(unity_hwnd)
pixel_menu = None
if hmenu:
    count = user32.GetMenuItemCount(hmenu)
    for i in range(count):
        buf = ctypes.create_unicode_buffer(128)
        user32.GetMenuStringW(hmenu, i, buf, 128, 0x0400)
        if 'PixelGame' in buf.value:
            pixel_menu = user32.GetSubMenu(hmenu, i)
            break

capture_id = None
if pixel_menu:
    count = user32.GetMenuItemCount(pixel_menu)
    for i in range(count):
        buf = ctypes.create_unicode_buffer(128)
        user32.GetMenuStringW(pixel_menu, i, buf, 128, 0x0400)
        cmd_id = user32.GetMenuItemID(pixel_menu, i)
        val = buf.value.encode('ascii', 'replace').decode('ascii')
        if 'Gemi Ekran' in val:
            capture_id = cmd_id
            print(f'Found Capture item "{val}" ID={cmd_id}')

if capture_id:
    print(f'Sending command {capture_id}...')
    user32.SendMessageW(unity_hwnd, 0x0111, capture_id, 0)
    time.sleep(4)
    if os.path.exists('scratch/gameplay_view_9_16.png'):
        mtime = os.path.getmtime('scratch/gameplay_view_9_16.png')
        print(f'Screenshot scratch/gameplay_view_9_16.png modified at {time.ctime(mtime)}')
else:
    print('Could not find capture command ID!')
