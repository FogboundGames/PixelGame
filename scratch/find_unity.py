import ctypes
import ctypes.wintypes
import time

user32 = ctypes.windll.user32
WNDENUMPROC = ctypes.WINFUNCTYPE(ctypes.wintypes.BOOL, ctypes.wintypes.HWND, ctypes.wintypes.LPARAM)

count = 0
found_unity = []

def enum_cb(hwnd, lparam):
    global count
    count += 1
    pid = ctypes.c_ulong()
    user32.GetWindowThreadProcessId(hwnd, ctypes.byref(pid))
    length = user32.GetWindowTextLengthW(hwnd)
    if length > 0:
        buff = ctypes.create_unicode_buffer(length + 1)
        user32.GetWindowTextW(hwnd, buff, length + 1)
        vis = user32.IsWindowVisible(hwnd)
        val = buff.value
        if "PixelGame" in val or "Unity" in val:
            print(f"PID={pid.value} HWND={hex(hwnd)} Vis={vis} Title='{val}'")
            found_unity.append((hwnd, pid.value, val))
    return True

user32.EnumWindows(WNDENUMPROC(enum_cb), 0)
print(f"Total top-level windows checked: {count}")

for hwnd, pid, title in found_unity:
    if "PixelGame" in title and "Unity" in title:
        print(f"Activating Unity window {hex(hwnd)}: {title}")
        user32.ShowWindow(hwnd, 9) # SW_RESTORE
        user32.SetForegroundWindow(hwnd)
        time.sleep(0.5)
        # Send Ctrl+R
        user32.keybd_event(0x11, 0, 0, 0)
        user32.keybd_event(0x52, 0, 0, 0)
        user32.keybd_event(0x52, 0, 2, 0)
        user32.keybd_event(0x11, 0, 2, 0)
        print("Sent Ctrl+R to Unity!")
