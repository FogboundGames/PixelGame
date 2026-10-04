Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Select-Object ProcessId, CommandLine | Format-List
