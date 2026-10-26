import win32process, win32gui, win32con

hwnds = []
def enum_cb(hwnd, extra):
    _, pid = win32process.GetWindowThreadProcessId(hwnd)
    if pid == 5760:
        hwnds.append(hwnd)
        print(f'HWND {hwnd}: text="{win32gui.GetWindowText(hwnd)}", visible={win32gui.IsWindowVisible(hwnd)}')
    return True

win32gui.EnumWindows(enum_cb, None)
for h in hwnds:
    if win32gui.IsWindowVisible(h):
        win32gui.ShowWindow(h, win32con.SW_RESTORE)
        win32gui.SetForegroundWindow(h)
        print('Focused:', h)
