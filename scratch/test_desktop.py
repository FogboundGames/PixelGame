import ctypes
from ctypes import wintypes

user32 = ctypes.windll.user32

# Open input desktop
hDesk = user32.OpenInputDesktop(0, False, 0x01FF) # GENERIC_ALL
print("OpenInputDesktop handle:", hDesk)

titles = []
def enum_cb(hwnd, lparam):
    length = user32.GetWindowTextLengthW(hwnd)
    if length > 0:
        buff = ctypes.create_unicode_buffer(length + 1)
        user32.GetWindowTextW(hwnd, buff, length + 1)
        titles.append((hwnd, buff.value))
    return True

DESKTOPENUMPROC = ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)

if hDesk:
    res = user32.EnumDesktopWindows(hDesk, DESKTOPENUMPROC(enum_cb), 0)
    print("EnumDesktopWindows result:", res, "Count:", len(titles))
    for h, t in titles:
        if any(k in t.lower() for k in ['unity', 'pixel', 'game']):
            print(f"HWND: {hex(h)}, Title: '{t}'")
            user32.SetForegroundWindow(h)
