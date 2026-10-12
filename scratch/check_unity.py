import subprocess

output = subprocess.check_output('wmic process where "name=\'Unity.exe\'" get ProcessId,CommandLine', shell=True, text=True)
print(output)
