<#
Registers this PC as the DeadlyJenga self-hosted runner and starts it at logon.
Run it yourself in a normal (not admin) PowerShell window. It asks for a registration
token; get one from a repo admin: GitHub > DeadlyJenga > Settings > Actions > Runners >
New self-hosted runner (the token after --token, valid for 1 hour). Never commit it.

  .\Install-Runner.ps1            # register + scheduled task + start
  .\Install-Runner.ps1 -Remove    # stop, delete the task and unregister (asks for a removal token)
#>
param(
    [string] $RunnerDir = 'C:\actions-runner\deadlyjenga',
    [string] $RepoUrl = 'https://github.com/Vanderbilt-VR-2026/DeadlyJenga',
    [string] $RunnerName = "$env:COMPUTERNAME-unity",
    [switch] $Remove
)
$ErrorActionPreference = 'Stop'
$task = 'DeadlyJenga Unity Runner'
if (-not (Test-Path "$RunnerDir\config.cmd")) { throw "Runner files not found in $RunnerDir" }
Push-Location $RunnerDir
try {
    $secure = Read-Host -AsSecureString ($(if ($Remove) { 'Removal token' } else { 'Registration token' }))
    $token = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure))
    if ($Remove) {
        Stop-ScheduledTask -TaskName $task -ErrorAction SilentlyContinue
        Unregister-ScheduledTask -TaskName $task -Confirm:$false -ErrorAction SilentlyContinue
        Get-Process Runner.Listener -ErrorAction SilentlyContinue | Stop-Process -Force
        & .\config.cmd remove --token $token
        return
    }
    & .\config.cmd --unattended --url $RepoUrl --token $token --name $RunnerName `
        --labels deadlyjenga-unity --work _work
    if ($LASTEXITCODE -ne 0) { throw 'config.cmd failed.' }
    # Runs as you (Unity's license lives in your profile), only while you are logged on.
    $action = New-ScheduledTaskAction -Execute "$RunnerDir\run.cmd" -WorkingDirectory $RunnerDir
    $trigger = New-ScheduledTaskTrigger -AtLogOn -User "$env:USERDOMAIN\$env:USERNAME"
    $settings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -RestartCount 3 `
        -RestartInterval (New-TimeSpan -Minutes 5) -StartWhenAvailable -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
    Register-ScheduledTask -TaskName $task -Action $action -Trigger $trigger -Settings $settings -Force | Out-Null
    Start-ScheduledTask -TaskName $task
    Write-Host "Runner '$RunnerName' registered and started (scheduled task '$task')."
}
finally { $token = $null; Pop-Location }
