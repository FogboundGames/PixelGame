$wshell = New-Object -ComObject WScript.Shell
$result = $wshell.AppActivate(23068)
Write-Host "AppActivate 23068 returned: $result"
Start-Sleep -Seconds 6
Get-Item scratch/gameplay_view_9_16.png | Select-Object LastWriteTime
