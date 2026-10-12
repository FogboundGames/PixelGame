import ctypes
from ctypes import wintypes
import time
import os
import subprocess

user32 = ctypes.windll.user32
hwinsta = user32.OpenWindowStationW('WinSta0', False, 0x037F)
user32.SetProcessWindowStation(hwinsta)
hdesk = user32.OpenDesktopW('default', 0, False, 0x01FF)
user32.SetThreadDesktop(hdesk)

unity_hwnd = 68006
user32.SetForegroundWindow(unity_hwnd)
time.sleep(0.5)

print('Triggering Refresh (40276)...')
user32.SendMessageW(unity_hwnd, 0x0111, 40276, 0)
time.sleep(12)

# Find PixelGame menu and the command ID
hmenu = user32.GetMenu(unity_hwnd)
cmd_id = 40923
if hmenu:
    count = user32.GetMenuItemCount(hmenu)
    for i in range(count):
        buf = ctypes.create_unicode_buffer(128)
        user32.GetMenuStringW(hmenu, i, buf, 128, 0x0400)
        if 'PixelGame' in buf.value:
            sub = user32.GetSubMenu(hmenu, i)
            sub_count = user32.GetMenuItemCount(sub)
            for j in range(sub_count):
                buf2 = ctypes.create_unicode_buffer(128)
                user32.GetMenuStringW(sub, j, buf2, 128, 0x0400)
                clean_title = buf2.value.encode('ascii', 'replace').decode('ascii')
                if 'Straight' in clean_title:
                    cmd_id = user32.GetMenuItemID(sub, j)
                    print(f'Found Straight Pier command: {clean_title} ID={cmd_id}')

print(f'Sending Straight Pier command {cmd_id}...')
user32.SendMessageW(unity_hwnd, 0x0111, cmd_id, 0)
time.sleep(4)

if os.path.exists('scratch/gameplay_view_9_16.png'):
    mtime = os.path.getmtime('scratch/gameplay_view_9_16.png')
    print('Screenshot modified at:', time.ctime(mtime))
