import ctypes

user32 = ctypes.windll.user32
kernel32 = ctypes.windll.kernel32

def enum_cb(hwnd, extra):
    pid = ctypes.c_ulong()
    user32.GetWindowThreadProcessId(hwnd, ctypes.byref(pid))
    if pid.value == 5760:
        length = user32.GetWindowTextLengthW(hwnd)
        buff = ctypes.create_unicode_buffer(length + 1)
        user32.GetWindowTextW(hwnd, buff, length + 1)
        visible = user32.IsWindowVisible(hwnd)
        print(f'HWND {hwnd}: title="{buff.value}", visible={visible}')
        if visible and length > 0:
            user32.ShowWindow(hwnd, 9)
            user32.SetForegroundWindow(hwnd)
            print('SetForegroundWindow called on', hwnd)
    return True

WNDENUMPROC = ctypes.WINFUNCTYPE(ctypes.c_bool, ctypes.c_void_p, ctypes.c_void_p)
user32.EnumWindows(WNDENUMPROC(enum_cb), 0)
