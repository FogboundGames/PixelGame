Add-Type -AssemblyName Microsoft.VisualBasic
Add-Type -AssemblyName System.Windows.Forms
$procs = Get-Process -Name Unity -ErrorAction SilentlyContinue
foreach ($p in $procs) {
    if ($p.MainWindowTitle -ne "") {
        Write-Output "Activating Unity ($($p.Id)): $($p.MainWindowTitle)"
        [Microsoft.VisualBasic.Interaction]::AppActivate($p.Id)
        Start-Sleep -Milliseconds 600
        [System.Windows.Forms.SendKeys]::SendWait("^r")
        Write-Output "Sent Ctrl+R to Unity window"
        break
    }
}
