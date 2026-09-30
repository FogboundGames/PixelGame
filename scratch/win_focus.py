import ctypes
import time

user32 = ctypes.windll.user32
WNDENUMPROC = ctypes.WINFUNCTYPE(ctypes.c_bool, ctypes.c_int, ctypes.c_int)

def enum_cb(hwnd, lparam):
    length = user32.GetWindowTextLengthW(hwnd)
    if length > 0:
        buff = ctypes.create_unicode_buffer(length + 1)
        user32.GetWindowTextW(hwnd, buff, length + 1)
        title = buff.value
        if "Unity" in title or "PixelGame" in title:
            print(f"Found Window: HWND={hex(hwnd)} Title='{title}'")
            # Restore and focus
            user32.ShowWindow(hwnd, 9) # SW_RESTORE
            user32.SetForegroundWindow(hwnd)
            time.sleep(0.3)
            # Send Ctrl+R (0x11, 0x52)
            user32.keybd_event(0x11, 0, 0, 0)
            user32.keybd_event(0x52, 0, 0, 0)
            user32.keybd_event(0x52, 0, 2, 0)
            user32.keybd_event(0x11, 0, 2, 0)
            print("Sent Ctrl+R to Unity window!")
            return False # Stop enumerating
    return True

user32.EnumWindows(WNDENUMPROC(enum_cb), 0)
