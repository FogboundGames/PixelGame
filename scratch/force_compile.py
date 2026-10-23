import ctypes
from ctypes import wintypes
import time
import subprocess
import os

user32 = ctypes.windll.user32
kernel32 = ctypes.windll.kernel32

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

WNDENUMPROC = ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)
for pid in pids:
    out_threads = subprocess.check_output(['powershell', '-NoProfile', '-Command', f'(Get-Process -Id {pid}).Threads.Id'], text=True)
    thread_ids = [int(x.strip()) for x in out_threads.split() if x.strip().isdigit()]
    for tid in thread_ids:
        user32.EnumThreadWindows(tid, WNDENUMPROC(enum_cb), 0)
        if unity_hwnd: break
    if unity_hwnd: break

print('Unity HWND:', unity_hwnd)

# Get current foreground window thread
fg_hwnd = user32.GetForegroundWindow()
fg_thread = user32.GetWindowThreadProcessId(fg_hwnd, None)
cur_thread = kernel32.GetCurrentThreadId()
target_thread = user32.GetWindowThreadProcessId(unity_hwnd, None)

print(f'Attaching thread input: cur={cur_thread}, fg={fg_thread}, target={target_thread}')
user32.AttachThreadInput(cur_thread, fg_thread, True)
user32.AttachThreadInput(cur_thread, target_thread, True)

# Windows bypass foreground lock
VK_MENU = 0x12
user32.keybd_event(VK_MENU, 0, 0, 0)
user32.keybd_event(VK_MENU, 0, 2, 0)

user32.ShowWindow(unity_hwnd, 9)
user32.SetForegroundWindow(unity_hwnd)
user32.BringWindowToTop(unity_hwnd)
user32.SetFocus(unity_hwnd)

user32.AttachThreadInput(cur_thread, fg_thread, False)
user32.AttachThreadInput(cur_thread, target_thread, False)

time.sleep(1)

dll_path = 'Library/ScriptAssemblies/Assembly-CSharp.dll'
prev_mtime = os.path.getmtime(dll_path) if os.path.exists(dll_path) else 0

# Send Assets -> Refresh command (ID=40280)
user32.SendMessageW(unity_hwnd, 0x0111, 40280, 0)
print('Sent WM_COMMAND 40280 (Assets -> Refresh)')

# Send Ctrl+R
VK_CONTROL = 0x11
VK_R = 0x52
user32.keybd_event(VK_CONTROL, 0, 0, 0)
time.sleep(0.1)
user32.keybd_event(VK_R, 0, 0, 0)
time.sleep(0.1)
user32.keybd_event(VK_R, 0, 2, 0)
time.sleep(0.1)
user32.keybd_event(VK_CONTROL, 0, 2, 0)

print(f'Dispatched Ctrl+R (prev mtime: {time.ctime(prev_mtime)}). Waiting for recompile...')
for i in range(20):
    time.sleep(1)
    if os.path.exists(dll_path):
        mtime = os.path.getmtime(dll_path)
        if mtime > prev_mtime:
            print(f'[{i}] Compilation detected! New mtime: {time.ctime(mtime)}')
            break
    print(f'[{i}] Waiting...')

