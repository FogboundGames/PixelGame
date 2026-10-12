import subprocess

for pid in [4524, 23132, 30668]:
    try:
        cmd = subprocess.check_output(['powershell', '-NoProfile', '-Command', f'(Get-CimInstance Win32_Process -Filter "ProcessId = {pid}").CommandLine'], errors='ignore')
        print(pid, '-->', cmd.strip()[:100])
    except Exception as e:
        print(pid, 'error', e)
