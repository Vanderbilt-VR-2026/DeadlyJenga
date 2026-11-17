<#
Installs the test bridge on this PC (no admin needed).
  .\Install-Bridge.ps1            # copy scripts to C:\DeadlyJengaCI\bin, start at logon, start now
  .\Install-Bridge.ps1 -Remove    # stop and remove the scheduled task (keeps the clone and results)
Needs C:\DeadlyJengaCI\repo: a normal clone of DeadlyJenga used only for testing.
Re-run after changing the bridge scripts so the trusted copy in bin\ is updated.
#>
param([string] $Root = 'C:\DeadlyJengaCI', [switch] $Remove)
$ErrorActionPreference = 'Stop'
$task = 'DeadlyJenga Test Bridge'
Stop-ScheduledTask -TaskName $task -ErrorAction SilentlyContinue
if ($Remove) {
    Unregister-ScheduledTask -TaskName $task -Confirm:$false -ErrorAction SilentlyContinue
    Write-Host "Removed '$task'."; return
}
if (-not (Test-Path "$Root\repo\.git")) { throw "Clone DeadlyJenga into $Root\repo first." }
$bin = Join-Path $Root 'bin'
New-Item -ItemType Directory -Force $bin | Out-Null
foreach ($f in 'Watch-Branches.ps1', 'Run-UnityTests.ps1', 'Publish-TestSummary.ps1') {
    Copy-Item (Join-Path $PSScriptRoot $f) $bin -Force
}
$action = New-ScheduledTaskAction -Execute 'powershell.exe' -WorkingDirectory $Root `
    -Argument "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$bin\Watch-Branches.ps1`""
$trigger = New-ScheduledTaskTrigger -AtLogOn -User "$env:USERDOMAIN\$env:USERNAME"
$settings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -RestartCount 3 `
    -RestartInterval (New-TimeSpan -Minutes 5) -StartWhenAvailable -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries -MultipleInstances IgnoreNew
Register-ScheduledTask -TaskName $task -Action $action -Trigger $trigger -Settings $settings -Force | Out-Null
Start-ScheduledTask -TaskName $task
Write-Host "Installed and started '$task'. Log: $Root\bridge.log"
