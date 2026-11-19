param(
    [Parameter(Mandatory)][string] $Output,
    [Parameter(Mandatory)][string] $Adb,
    [string] $Serial
)
$ErrorActionPreference = 'Stop'
$adbPath = (Resolve-Path -LiteralPath $Adb).Path
$devices = @(& $adbPath devices | Select-String '^\S+\s+device$' | ForEach-Object { ($_.Line -split '\s+')[0] })
if ($LASTEXITCODE) { throw 'ADB device query failed.' }
if (!$Serial -and $devices.Count -eq 1) { $Serial = $devices[0] }
if (!$Serial -or $Serial -notin $devices) { throw 'Connect and authorize the Quest, or select an authorized device with -Serial.' }
if (Test-Path -LiteralPath $Output) { throw 'Choose a new output directory.' }
$folder = New-Item -ItemType Directory -Path $Output
function Snapshot([string] $File, [string[]] $AdbArguments) {
    & $adbPath -s $Serial @AdbArguments 2>&1 | Out-File -Encoding utf8 (Join-Path $folder.FullName $File)
    if ($LASTEXITCODE) { throw "ADB failed for $File." }
}
Snapshot 'device.txt' @('shell', 'getprop', 'ro.product.model')
Snapshot 'package.txt' @('shell', 'dumpsys', 'package', 'com.deadlyjenga.quest3')
Snapshot 'memory.txt' @('shell', 'dumpsys', 'meminfo', 'com.deadlyjenga.quest3')
Snapshot 'logcat.txt' @('logcat', '-d', '-t', '3000', '-v', 'threadtime')
Write-Output $folder.FullName
