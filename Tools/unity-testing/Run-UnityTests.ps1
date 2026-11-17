<#
.SYNOPSIS
    Runs automated Unity EditMode and PlayMode tests in batch mode and produces structured reports.
.DESCRIPTION
    Executes Unity batchmode test runs against DeadlyJenga, parses NUnit XML results into
    test-summary.json (matching the Codex/ChatGPT pipeline schema), extracts stack traces and
    log context, and generates a markdown report.
#>
[CmdletBinding()]
param(
    [string]$ProjectPath = "C:\Users\evanp\DeadlyJenga-QuestBuilder\project",
    [ValidateSet("EditMode", "PlayMode", "Both")]
    [string]$TestPlatform = "Both",
    [string]$OutputDir = "",
    [string]$TestFilter = "",
    [string]$TestCategory = "",
    [int]$TimeoutSeconds = 300,
    [string]$UnityPath = "C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe",
    [string]$Trigger = "local-manual",
    [string]$CommitSha = "",
    [string]$Branch = ""
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $UnityPath)) {
    Write-Error "Unity Editor executable not found at '$UnityPath'"
    exit 2
}

if (-not (Test-Path $ProjectPath)) {
    Write-Error "Unity project path not found at '$ProjectPath'"
    exit 2
}

# Resolve git metadata if not supplied
if (-not $CommitSha) {
    try {
        $CommitSha = (git -C "$ProjectPath" rev-parse HEAD 2>$null).Trim()
    } catch {
        $CommitSha = "UNKNOWN"
    }
}
if (-not $Branch) {
    try {
        $Branch = (git -C "$ProjectPath" rev-parse --abbrev-ref HEAD 2>$null).Trim()
    } catch {
        $Branch = "UNKNOWN"
    }
}

$runId = "run_" + (Get-Date -Format "yyyyMMdd_HHmmss")
if (-not $OutputDir) {
    $OutputDir = Join-Path $ProjectPath "TestResults\$runId"
}
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

$platformsToRun = switch ($TestPlatform) {
    "Both" { @("EditMode", "PlayMode") }
    default { @($TestPlatform) }
}

$overallTotal = 0
$overallPassed = 0
$overallFailed = 0
$overallSkipped = 0
$overallDuration = 0.0
$failuresList = [System.Collections.Generic.List[PSObject]]::new()
$platformResults = @{}
$runnerExitCode = 0

foreach ($plat in $platformsToRun) {
    Write-Host "=================================================="
    Write-Host " Running Unity $plat Tests ($ProjectPath)..."
    Write-Host "=================================================="

    $xmlPath = Join-Path $OutputDir "test-results-$plat.xml"
    $logPath = Join-Path $OutputDir "editor-$plat.log"

    $argsList = @(
        "-batchmode",
        "-nographics",
        "-projectPath", "`"$ProjectPath`"",
        "-runTests",
        "-testPlatform", "$plat",
        "-testResults", "`"$xmlPath`"",
        "-logFile", "`"$logPath`""
    )

    if ($TestFilter) {
        $argsList += @("-testFilter", "`"$TestFilter`"")
    }
    if ($TestCategory) {
        $argsList += @("-testCategory", "`"$TestCategory`"")
    }

    $argString = $argsList -join " "
    $sw = [System.Diagnostics.Stopwatch]::StartNew()

    $pinfo = New-Object System.Diagnostics.ProcessStartInfo
    $pinfo.FileName = $UnityPath
    $pinfo.Arguments = $argString
    $pinfo.UseShellExecute = $false
    $pinfo.CreateNoWindow = $true

    $proc = [System.Diagnostics.Process]::Start($pinfo)
    $completed = $proc.WaitForExit($TimeoutSeconds * 1000)

    $sw.Stop()

    if (-not $completed) {
        Write-Warning "Unity process timed out after $TimeoutSeconds seconds! Killing process..."
        try { $proc.Kill() } catch {}
        $runnerExitCode = 2
        $overallFailed++
        $tail = (Get-Content $logPath -Tail 40 -ErrorAction SilentlyContinue) -join "`n"
        $failuresList.Add([PSCustomObject]@{
            testName = "$plat Suite"
            suiteName = "$plat"
            message = "Test execution timed out after $TimeoutSeconds seconds"
            stackTrace = ""
            logExcerpt = $tail
        })
        continue
    }

    $unityExit = $proc.ExitCode
    Write-Host "Unity exited with code: $unityExit (Duration: $([Math]::Round($sw.Elapsed.TotalSeconds, 2))s)"

    if (Test-Path $xmlPath) {
        try {
            [xml]$xml = Get-Content $xmlPath
            $root = $xml.'test-run'
            $platTotal = [int]$root.total
            $platPassed = [int]$root.passed
            $platFailed = [int]$root.failed
            $platSkipped = [int]$root.skipped
            $platDuration = [double]$root.duration

            $overallTotal += $platTotal
            $overallPassed += $platPassed
            $overallFailed += $platFailed
            $overallSkipped += $platSkipped
            $overallDuration += $platDuration

            # Inspect failures
            $failedNodes = $xml.SelectNodes("//test-case[@result='Failed' or @result='Error']")
            foreach ($fn in $failedNodes) {
                $testName = if ($fn.fullname) { $fn.fullname } else { $fn.name }
                $suiteName = if ($fn.classname) { $fn.classname } else { $plat }
                $msg = ""
                $st = ""
                if ($fn.failure) {
                    $msg = $fn.failure.message.'#cdata-section'
                    if (-not $msg) { $msg = $fn.failure.message.InnerText }
                    $st = $fn.failure.'stack-trace'.'#cdata-section'
                    if (-not $st) { $st = $fn.failure.'stack-trace'.InnerText }
                }

                # Find relevant log context around test name
                $logExcerpt = ""
                if (Test-Path $logPath) {
                    $logLines = Get-Content $logPath -ErrorAction SilentlyContinue
                    $matchIndex = -1
                    for ($i = 0; $i -lt $logLines.Length; $i++) {
                        if ($logLines[$i] -like "*$($fn.name)*") {
                            $matchIndex = $i
                            break
                        }
                    }
                    if ($matchIndex -ge 0) {
                        $start = [Math]::Max(0, $matchIndex - 10)
                        $count = [Math]::Min($logLines.Length - $start, 35)
                        $logExcerpt = ($logLines[$start..($start + $count)]) -join "`n"
                    } else {
                        $logExcerpt = ($logLines | Select-Object -Last 30) -join "`n"
                    }
                }

                $failuresList.Add([PSCustomObject]@{
                    testName = $testName
                    suiteName = $suiteName
                    message = ($msg.Trim() -replace "`r", "")
                    stackTrace = ($st.Trim() -replace "`r", "")
                    logExcerpt = $logExcerpt
                })
            }

            $platformResults[$plat] = @{
                total = $platTotal
                passed = $platPassed
                failed = $platFailed
                skipped = $platSkipped
                duration = $platDuration
                xmlPath = $xmlPath
                logPath = $logPath
            }
        } catch {
            Write-Warning "Failed to parse test XML: $_"
            $overallFailed++
            $runnerExitCode = 2
        }
    } else {
        Write-Warning "No test results XML was generated for $plat!"
        $tailLog = (Get-Content $logPath -Tail 40 -ErrorAction SilentlyContinue) -join "`n"
        $failuresList.Add([PSCustomObject]@{
            testName = "$plat Test Suite Startup"
            suiteName = "$plat"
            message = "Unity exited without producing test-results XML (Exit code: $unityExit)"
            stackTrace = ""
            logExcerpt = $tailLog
        })
        $overallFailed++
        $runnerExitCode = 2
    }
}

$statusStr = if ($runnerExitCode -eq 2) { "ERROR" } elseif ($overallFailed -gt 0) { "FAIL" } else { "PASS" }

$summaryObj = [PSCustomObject]@{
    runId = $runId
    timestamp = (Get-Date -Format "o")
    trigger = $Trigger
    branch = $Branch
    commitSha = $CommitSha
    environment = [PSCustomObject]@{
        unityVersion = "6000.3.23f1"
        platform = "Windows"
        testPlatform = $TestPlatform
        machineName = $env:COMPUTERNAME
    }
    summary = [PSCustomObject]@{
        total = $overallTotal
        passed = $overallPassed
        failed = $overallFailed
        skipped = $overallSkipped
        duration = [Math]::Round($overallDuration, 3)
        status = $statusStr
    }
    failures = $failuresList
    diagnostics = [PSCustomObject]@{
        antigravityInvoked = $false
        antigravitySessionId = $null
        antigravityReport = $null
    }
    artifacts = [PSCustomObject]@{
        outputDirectory = $OutputDir
        platforms = $platformResults
        testSummaryJson = (Join-Path $OutputDir "test-summary.json")
        reportMarkdown = (Join-Path $OutputDir "test-report.md")
    }
}

$jsonPath = Join-Path $OutputDir "test-summary.json"
$summaryObj | ConvertTo-Json -Depth 6 | Set-Content -Path $jsonPath -Encoding utf8
Write-Host "Summary written to: $jsonPath"

# Build Markdown Report
$md = [System.Text.StringBuilder]::new()
[void]$md.AppendLine("# Unity Test Run: $statusStr")
[void]$md.AppendLine("")
[void]$md.AppendLine("- **Commit:** ``" + $CommitSha + "``")
[void]$md.AppendLine("- **Branch:** ``" + $Branch + "``")
[void]$md.AppendLine("- **Trigger:** ``" + $Trigger + "``")
[void]$md.AppendLine("- **Platform:** ``" + $TestPlatform + "`` (Unity 6000.3.23f1)")
[void]$md.AppendLine("- **Timestamp:** " + ((Get-Date).ToString('yyyy-MM-dd HH:mm:ss UTC')))
[void]$md.AppendLine("")
[void]$md.AppendLine("### Summary")
[void]$md.AppendLine("| Metric | Value |")
[void]$md.AppendLine("|---|---|")
[void]$md.AppendLine("| **Status** | **$statusStr** |")
[void]$md.AppendLine("| **Total Tests** | $overallTotal |")
[void]$md.AppendLine("| **Passed** | $overallPassed |")
[void]$md.AppendLine("| **Failed** | $overallFailed |")
[void]$md.AppendLine("| **Skipped** | $overallSkipped |")
[void]$md.AppendLine("| **Duration** | " + [Math]::Round($overallDuration, 2) + "s |")
[void]$md.AppendLine("")

if ($failuresList.Count -gt 0) {
    [void]$md.AppendLine("### Failures ($($failuresList.Count))")
    foreach ($f in $failuresList) {
        [void]$md.AppendLine("#### ``" + $f.testName + "``")
        if ($f.message) {
            [void]$md.AppendLine('**Message:**')
            [void]$md.AppendLine('```')
            [void]$md.AppendLine($f.message)
            [void]$md.AppendLine('```')
        }
        if ($f.stackTrace) {
            [void]$md.AppendLine('<details><summary>Stack Trace</summary>')
            [void]$md.AppendLine('')
            [void]$md.AppendLine('```')
            [void]$md.AppendLine($f.stackTrace)
            [void]$md.AppendLine('```')
            [void]$md.AppendLine('</details>')
        }
        if ($f.logExcerpt) {
            [void]$md.AppendLine('<details><summary>Editor Log Excerpt</summary>')
            [void]$md.AppendLine('')
            [void]$md.AppendLine('```')
            [void]$md.AppendLine($f.logExcerpt)
            [void]$md.AppendLine('```')
            [void]$md.AppendLine('</details>')
        }
        [void]$md.AppendLine("")
    }
}

$mdPath = Join-Path $OutputDir "test-report.md"
$md.ToString() | Set-Content -Path $mdPath -Encoding utf8
Write-Host "Markdown report written to: $mdPath"

Write-Host "=================================================="
Write-Host " Test Execution Completed: $statusStr"
Write-Host " Total: $overallTotal | Passed: $overallPassed | Failed: $overallFailed | Skipped: $overallSkipped"
Write-Host "=================================================="

if ($statusStr -eq "PASS") {
    exit 0
} elseif ($statusStr -eq "FAIL") {
    exit 1
} else {
    exit 2
}
