import ctypes
import psutil

user32 = ctypes.windll.user32

pids = {p.info['pid']: p.info['name'] for p in psutil.process_iter(['name', 'pid'])}

def enum_cb(hwnd, extra):
    pid = ctypes.c_ulong()
    user32.GetWindowThreadProcessId(hwnd, ctypes.byref(pid))
    pname = pids.get(pid.value, 'unknown')
    if 'unity' in pname.lower():
        length = user32.GetWindowTextLengthW(hwnd)
        buff = ctypes.create_unicode_buffer(length + 1)
        user32.GetWindowTextW(hwnd, buff, length + 1)
        visible = user32.IsWindowVisible(hwnd)
        print(f'PID {pid.value} ({pname}) HWND {hwnd}: visible={visible}, title="{buff.value}"')
    return True

WNDENUMPROC = ctypes.WINFUNCTYPE(ctypes.c_bool, ctypes.c_void_p, ctypes.c_void_p)
user32.EnumWindows(WNDENUMPROC(enum_cb), 0)
