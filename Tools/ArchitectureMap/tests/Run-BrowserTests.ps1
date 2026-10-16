param([int]$Port = 47832)
$ErrorActionPreference = 'Stop'
$mapRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$mapDll = Join-Path $mapRoot 'Tools/ArchitectureMap/bin/Debug/net10.0/ArchitectureMap.dll'
$mapProcess = Start-Process dotnet -ArgumentList @(('"' + $mapDll + '"'), '--root', ('"' + $mapRoot + '"'), '--port', $Port) -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $mapRoot '.architecture/local/browser-server.log') -RedirectStandardError (Join-Path $mapRoot '.architecture/local/browser-server-error.log')
try {
    $mapReady = $false
    for ($mapAttempt = 0; $mapAttempt -lt 120; $mapAttempt++) {
        try { Invoke-RestMethod "http://127.0.0.1:$Port/api/heartbeat" -TimeoutSec 1 | Out-Null; $mapReady = $true; break } catch { Start-Sleep -Seconds 1 }
    }
    if (!$mapReady) { throw 'Dashboard server did not start.' }
    $env:ARCHITECTURE_URL = "http://127.0.0.1:$Port"
    & node (Join-Path $PSScriptRoot 'browser.cjs')
    $mapExit = $LASTEXITCODE
} finally {
    try { Invoke-RestMethod "http://127.0.0.1:$Port/api/stop" -Method Post -Headers @{'X-Architecture-Client'='local'} | Out-Null } catch { }
    if (!$mapProcess.HasExited) { Stop-Process -Id $mapProcess.Id -ErrorAction SilentlyContinue }
}
exit $mapExit
