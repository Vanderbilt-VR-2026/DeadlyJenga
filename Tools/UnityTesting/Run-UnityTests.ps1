<#
Runs DeadlyJenga Unity tests in batch mode and writes results.json.
Exit code: 0 = tests passed, 1 = tests failed, 2 = infrastructure/compile/timeout problem.
Example: .\Run-UnityTests.ps1 -ProjectPath C:\path\to\repo -ResultsDir C:\temp\results
#>
param(
    [Parameter(Mandatory)] [string] $ProjectPath,
    [Parameter(Mandatory)] [string] $ResultsDir,
    [ValidateSet('PlayMode', 'EditMode')] [string] $Platform = 'PlayMode',
    [string] $Assemblies = 'DeadlyJenga.DevTools.Tests',
    [string] $UnityPath = '',
    [int] $TimeoutMinutes = 90,
    [string] $TaskId = ''
)
$ErrorActionPreference = 'Stop'
$start = Get-Date
New-Item -ItemType Directory -Force $ResultsDir | Out-Null
$xml = Join-Path $ResultsDir 'results.xml'
$log = Join-Path $ResultsDir 'unity.log'
$json = Join-Path $ResultsDir 'results.json'
Remove-Item $xml, $log, $json -ErrorAction SilentlyContinue

$commit = (git -C $ProjectPath rev-parse HEAD 2>$null)
$branch = if ($env:DJ_BRANCH) { $env:DJ_BRANCH } else { (git -C $ProjectPath branch --show-current 2>$null) }
$result = [ordered]@{
    schema_version = 1; task_id = $(if ($TaskId) { $TaskId } else { "local-$($start.ToString('yyyyMMdd-HHmmss'))" })
    commit_sha = $commit; branch = $branch; platform = $Platform; unity_version = $null
    runner = $env:COMPUTERNAME; runner_status = 'infrastructure_failure'; test_status = 'not_run'
    failure_kind = $null; message = $null; unity_exit_code = $null
    tests_total = 0; tests_passed = 0; tests_failed = 0; tests_skipped = 0
    duration_seconds = 0; failed_tests = @(); log_excerpt = @()
    artifacts = [ordered]@{ test_results = 'results.xml'; editor_log = 'unity.log' }
}

function Finish([int] $code) {
    $result.duration_seconds = [int]((Get-Date) - $start).TotalSeconds
    $result | ConvertTo-Json -Depth 6 | Set-Content -Encoding utf8 $json
    Write-Host "runner_status=$($result.runner_status) test_status=$($result.test_status) passed=$($result.tests_passed) failed=$($result.tests_failed)"
    exit $code
}
function Fail([string] $kind, [string] $message) {
    $result.runner_status = $kind; $result.failure_kind = $kind; $result.message = $message
    if (Test-Path $log) {
        $result.log_excerpt = @(Select-String -Path $log -Pattern 'error CS\d+|Exception|Scripts have compiler errors|License|Aborting' |
            Select-Object -First 25 | ForEach-Object { $_.Line.Trim() })
    }
    Write-Host "::error::$kind - $message"
    Finish 2
}

try {
    $versionFile = Join-Path $ProjectPath 'ProjectSettings\ProjectVersion.txt'
    if (-not (Test-Path $versionFile)) { Fail 'infrastructure_failure' "Not a Unity project: $ProjectPath" }
    $version = ((Get-Content $versionFile | Select-String '^m_EditorVersion:') -split ':\s*')[1].Trim()
    $result.unity_version = $version
    if (-not $UnityPath) { $UnityPath = "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe" }
    if (-not (Test-Path $UnityPath)) { Fail 'infrastructure_failure' "Unity $version is not installed at $UnityPath" }

    # One Unity per project folder. Unity's own lock file means an editor already has it open.
    $unityLock = Join-Path $ProjectPath 'Temp\UnityLockfile'
    if (Test-Path $unityLock) {
        try { [IO.File]::Open($unityLock, 'Open', 'ReadWrite', 'None').Close() }
        catch { Fail 'infrastructure_failure' 'Another Unity Editor has this project open.' }
    }
    $lockPath = Join-Path $ProjectPath 'Temp\dj-test-runner.lock'
    New-Item -ItemType Directory -Force (Split-Path $lockPath) | Out-Null
    try { $lock = [IO.File]::Open($lockPath, 'OpenOrCreate', 'ReadWrite', 'None') }
    catch { Fail 'infrastructure_failure' 'Another test run is using this workspace.' }

    $unityArgs = @('-batchmode', '-nographics', '-projectPath', "`"$ProjectPath`"", '-runTests',
        '-testPlatform', $Platform, '-testResults', "`"$xml`"", '-logFile', "`"$log`"")
    if ($Assemblies) { $unityArgs += @('-assemblyNames', $Assemblies) }
    Write-Host "Running Unity $version ($Platform) on $commit"
    $proc = Start-Process -FilePath $UnityPath -ArgumentList $unityArgs -PassThru -WindowStyle Hidden
    if (-not $proc.WaitForExit($TimeoutMinutes * 60 * 1000)) {
        & taskkill /PID $proc.Id /T /F | Out-Null
        Fail 'timeout' "Unity did not finish within $TimeoutMinutes minutes."
    }
    $proc.WaitForExit()
    $result.unity_exit_code = $proc.ExitCode
    $lock.Close(); Remove-Item $lockPath -ErrorAction SilentlyContinue

    $logText = if (Test-Path $log) { Get-Content $log -Raw } else { '' }
    if ($logText -match 'Scripts have compiler errors|error CS\d{4}') {
        Fail 'compilation_failure' 'Unity reported C# compiler errors. See log_excerpt.'
    }
    if (-not (Test-Path $xml)) {
        Fail 'infrastructure_failure' "Unity exited with code $($proc.ExitCode) and wrote no test results."
    }

    [xml] $doc = Get-Content $xml -Raw
    $run = $doc.'test-run'
    $result.tests_total = [int]$run.total; $result.tests_passed = [int]$run.passed
    $result.tests_failed = [int]$run.failed; $result.tests_skipped = [int]$run.skipped
    $result.failed_tests = @($doc.SelectNodes("//test-case[@result='Failed']") | ForEach-Object {
        [ordered]@{ name = $_.fullname; message = ("$($_.failure.message.InnerText)").Trim() }
    })
    $result.runner_status = 'completed'
    if ($result.tests_total -eq 0) { Fail 'test_execution_failure' 'No tests were discovered.' }
    if ($result.tests_failed -gt 0 -or $run.result -like 'Failed*') {
        $result.test_status = 'failed'; $result.failure_kind = 'assertion_failure'; Finish 1
    }
    $result.test_status = 'passed'; Finish 0
}
catch { Fail 'infrastructure_failure' $_.Exception.Message }
