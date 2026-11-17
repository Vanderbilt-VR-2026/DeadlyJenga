<#
.SYNOPSIS
    DeadlyJenga Autonomous Testing Pipeline Trigger & Execution Daemon.
.DESCRIPTION
    Coordinates the end-to-end autonomous loop:
    1. Polling or target-dispatching for commits/PRs on GitHub.
    2. Posting GitHub Commit Status ('pending' -> 'success' / 'failure').
    3. Executing Unity batch-mode tests in an isolated worktree.
    4. Triggering Antigravity Desktop AI diagnostics on failure.
    5. Publishing structured markdown summaries and machine-readable JSON to GitHub.
#>
[CmdletBinding()]
param(
    [string]$ProjectPath = "C:\Users\evanp\DeadlyJenga-QuestBuilder\project",
    [string]$Repo = "Vanderbilt-VR-2026/DeadlyJenga",
    [string]$Branch = "",
    [string]$CommitSha = "",
    [int]$PullRequest = 0,
    [ValidateSet("EditMode", "PlayMode", "Both")]
    [string]$TestPlatform = "Both",
    [switch]$Poll,
    [int]$IntervalSeconds = 30,
    [int]$MaxRuns = 1,
    [string]$Trigger = "local-pipeline"
)

$ErrorActionPreference = "Stop"

# Function to get GitHub Token securely
function Get-GitHubToken {
    if ($env:GH_TOKEN) { return $env:GH_TOKEN }
    $userToken = [System.Environment]::GetEnvironmentVariable('GH_TOKEN', 'User')
    if ($userToken) { return $userToken }
    try {
        $cred = "protocol=https`nhost=github.com" | git credential fill 2>$null
        $token = ($cred | Select-String "password=").Line.Substring(9).Trim()
        if ($token) { return $token }
    } catch {}
    return $null
}

$token = Get-GitHubToken
if (-not $token) {
    Write-Warning "No GitHub API token found in GH_TOKEN or Git Credential Manager."
} else {
    $env:GH_TOKEN = $token
}

function Invoke-GitHubApi {
    param(
        [string]$Method = "GET",
        [string]$Path,
        [object]$Body = $null
    )
    if (-not $token) { return $null }
    $headers = @{
        "Authorization" = "token $token"
        "Accept" = "application/vnd.github.v3+json"
        "User-Agent" = "DeadlyJenga-Pipeline"
    }
    $uri = "https://api.github.com/repos/$Repo$Path"
    try {
        if ($Body) {
            $jsonBody = if ($Body -is [string]) { $Body } else { $Body | ConvertTo-Json -Depth 5 }
            return Invoke-RestMethod -Uri $uri -Method $Method -Headers $headers -Body $jsonBody -ContentType "application/json"
        } else {
            return Invoke-RestMethod -Uri $uri -Method $Method -Headers $headers
        }
    } catch {
        Write-Warning "GitHub API Error ($Method $uri): $_"
        return $null
    }
}

function Set-CommitStatus {
    param(
        [string]$Sha,
        [ValidateSet("pending", "success", "failure", "error")]
        [string]$State,
        [string]$Description,
        [string]$Context = "unity/tests",
        [string]$TargetUrl = ""
    )
    if (-not $token -or -not $Sha) { return }
    $body = @{
        state = $State
        description = $Description
        context = $Context
        target_url = $TargetUrl
    }
    Write-Host "Updating GitHub status for $Sha -> $($State): $Description"
    Invoke-GitHubApi -Method "POST" -Path ("/statuses/" + $Sha) -Body $body | Out-Null
}

function Post-PrComment {
    param(
        [int]$PrNumber,
        [string]$MarkdownBody
    )
    if (-not $token -or $PrNumber -le 0) { return }
    
    # Check for existing bot comment to update or post new
    $comments = Invoke-GitHubApi -Method "GET" -Path ("/issues/" + $PrNumber + "/comments")
    $existingComment = $null
    if ($comments) {
        $existingComment = $comments | Where-Object { $_.body -like "*<!-- deadlyjenga-pipeline-status -->*" } | Select-Object -Last 1
    }

    $taggedBody = "<!-- deadlyjenga-pipeline-status -->`n" + $MarkdownBody

    if ($existingComment) {
        Write-Host "Updating existing PR #$PrNumber comment $($existingComment.id)..."
        Invoke-GitHubApi -Method "PATCH" -Path ("/issues/comments/" + $existingComment.id) -Body @{ body = $taggedBody } | Out-Null
    } else {
        Write-Host "Posting new comment to PR #$PrNumber..."
        Invoke-GitHubApi -Method "POST" -Path ("/issues/" + $PrNumber + "/comments") -Body @{ body = $taggedBody } | Out-Null
    }
}

function Run-PipelineCycle {
    param(
        [string]$TargetBranch,
        [string]$TargetSha,
        [int]$TargetPr
    )

    Write-Host ""
    Write-Host "=================================================="
    Write-Host " STARTING DEADLYJENGA TEST PIPELINE CYCLE"
    Write-Host " Branch: $TargetBranch | Commit: $TargetSha | PR: $TargetPr"
    Write-Host "=================================================="

    # Set Commit Status to pending
    Set-CommitStatus -Sha $TargetSha -State "pending" -Description "Running Unity $TestPlatform tests in isolated worktree..."

    # Ensure isolated worktree is on the target commit/branch
    if ($TargetSha -and (Test-Path $ProjectPath)) {
        Write-Host "Syncing worktree '$ProjectPath' to $TargetSha..."
        try {
            git -C "$ProjectPath" fetch origin 2>$null
            git -C "$ProjectPath" checkout "$TargetSha" --force 2>$null
        } catch {
            Write-Warning "Could not checkout commit $TargetSha directly: $_"
        }
    }

    $runScript = Join-Path $PSScriptRoot "Run-UnityTests.ps1"
    $diagScript = Join-Path $PSScriptRoot "Invoke-AntigravityDiagnostic.ps1"
    $outputDir = Join-Path $ProjectPath ("TestResults\run_" + (Get-Date).ToString('yyyyMMdd_HHmmss'))

    $runArgs = @(
        "-ProjectPath", "`"$ProjectPath`"",
        "-TestPlatform", "$TestPlatform",
        "-OutputDir", "`"$outputDir`"",
        "-Trigger", "$Trigger",
        "-CommitSha", "$TargetSha",
        "-Branch", "$TargetBranch"
    )

    Write-Host "Executing test runner..."
    $proc = Start-Process -FilePath "powershell.exe" -ArgumentList (@("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "`"$runScript`"") + $runArgs) -PassThru -Wait -NoNewWindow
    $testExit = $proc.ExitCode

    $summaryJsonPath = Join-Path $outputDir "test-summary.json"
    $reportMdPath = Join-Path $outputDir "test-report.md"

    if (-not (Test-Path $summaryJsonPath)) {
        Set-CommitStatus -Sha $TargetSha -State "error" -Description "Unity test runner failed to start or produce results."
        return $false
    }

    $summary = Get-Content $summaryJsonPath -Raw -Encoding utf8 | ConvertFrom-Json

    # If failures occurred, invoke Antigravity Diagnostic Agent
    if ($summary.summary.failed -gt 0 -or $summary.summary.status -ne "PASS") {
        Write-Host "Tests failed! Invoking Antigravity diagnostic..."
        & $diagScript -SummaryJsonPath $summaryJsonPath -OutputMarkdownPath $reportMdPath
        # Re-read updated summary
        $summary = Get-Content $summaryJsonPath -Raw -Encoding utf8 | ConvertFrom-Json
    }

    # Determine final status
    $finalState = if ($summary.summary.status -eq "PASS") { "success" } else { "failure" }
    $desc = "Passed: " + $summary.summary.passed + "/" + $summary.summary.total + " (Duration: " + $summary.summary.duration + "s)"
    Set-CommitStatus -Sha $TargetSha -State $finalState -Description $desc

    # Format PR comment body
    if ($TargetPr -gt 0 -or $PullRequest -gt 0) {
        $prNum = if ($TargetPr -gt 0) { $TargetPr } else { $PullRequest }
        $reportMd = Get-Content $reportMdPath -Raw -Encoding utf8

        # Embed test-summary.json for ChatGPT consumption
        $summaryJson = Get-Content $summaryJsonPath -Raw -Encoding utf8
        $commentSb = [System.Text.StringBuilder]::new()
        [void]$commentSb.AppendLine($reportMd)
        [void]$commentSb.AppendLine("")
        [void]$commentSb.AppendLine("### 🤖 ChatGPT Machine-Readable Summary")
        [void]$commentSb.AppendLine("ChatGPT GitHub connector can parse the structured JSON payload below:")
        [void]$commentSb.AppendLine("<details><summary><b>Click to expand test-summary.json</b></summary>")
        [void]$commentSb.AppendLine("")
        [void]$commentSb.AppendLine('```json')
        [void]$commentSb.AppendLine($summaryJson)
        [void]$commentSb.AppendLine('```')
        [void]$commentSb.AppendLine("</details>")

        Post-PrComment -PrNumber $prNum -MarkdownBody $commentSb.ToString()
    }

    Write-Host "Pipeline cycle completed with status: $finalState"
    return ($finalState -eq "success")
}

# Main Execution Routing
if ($Poll) {
    Write-Host "Starting DeadlyJenga Pipeline Polling Daemon on $Repo..."
    Write-Host "Checking every $IntervalSeconds seconds (Max runs: $MaxRuns)..."
    $runCount = 0
    $testedShas = [System.Collections.Generic.HashSet[string]]::new()

    while ($runCount -lt $MaxRuns) {
        try {
            $prs = Invoke-GitHubApi -Method "GET" -Path "/pulls?state=open"
            if ($prs) {
                foreach ($pr in $prs) {
                    $prSha = $pr.head.sha
                    $prBranch = $pr.head.ref
                    $prNum = $pr.number

                    $statusResp = Invoke-GitHubApi -Method "GET" -Path ("/commits/" + $prSha + "/statuses")
                    $hasCompleted = $false
                    if ($statusResp) {
                        $match = $statusResp | Where-Object { $_.context -eq "unity/tests" -and ($_.state -eq "success" -or $_.state -eq "failure") }
                        if ($match) { $hasCompleted = $true }
                    }

                    if (-not $hasCompleted -and -not $testedShas.Contains($prSha)) {
                        Write-Host "Found un-tested commit on PR #$prNum ($prBranch @ $prSha)"
                        $testedShas.Add($prSha) | Out-Null
                        $null = Run-PipelineCycle -TargetBranch $prBranch -TargetSha $prSha -TargetPr $prNum
                        $runCount++
                        if ($runCount -ge $MaxRuns) { break }
                    }
                }
            }
        } catch {
            Write-Warning "Error during polling iteration: $_"
        }

        if ($runCount -lt $MaxRuns) {
            Write-Host "Sleeping $IntervalSeconds seconds until next poll..."
            Start-Sleep -Seconds $IntervalSeconds
        }
    }
} else {
    # Single run dispatch
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

    $ok = Run-PipelineCycle -TargetBranch $Branch -TargetSha $CommitSha -TargetPr $PullRequest
    if ($ok) { exit 0 } else { exit 1 }
}
