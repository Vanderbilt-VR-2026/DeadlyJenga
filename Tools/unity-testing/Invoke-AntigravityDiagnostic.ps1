<#
.SYNOPSIS
    Invokes Antigravity Desktop via AgentAPI to diagnose DeadlyJenga test failures.
.DESCRIPTION
    Reads test-summary.json, formats a structured diagnostic request, invokes language_server.exe agentapi,
    monitors the agent's transcript until completion, extracts the root-cause analysis and
    recommended fix, and incorporates the diagnostic report into test-summary.json and markdown reports.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [string]$SummaryJsonPath,
    [string]$OutputMarkdownPath = "",
    [int]$TimeoutSeconds = 180,
    [string]$LanguageServerExe = "C:\Users\evanp\AppData\Local\Programs\antigravity\resources\bin\language_server.exe",
    [string]$AgentApiBat = "C:\Users\evanp\.gemini\antigravity\bin\agentapi.bat"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $SummaryJsonPath)) {
    Write-Error "Summary JSON not found at '$SummaryJsonPath'"
    exit 1
}

$summary = Get-Content $SummaryJsonPath -Raw -Encoding utf8 | ConvertFrom-Json

if ($summary.summary.failed -eq 0 -and $summary.summary.status -eq "PASS") {
    Write-Host "All tests passed. Antigravity diagnostic not required."
    exit 0
}

Write-Host "=================================================="
Write-Host " Launching Antigravity Diagnostic Agent..."
Write-Host "=================================================="

# Build detailed technical prompt
$sb = [System.Text.StringBuilder]::new()
[void]$sb.AppendLine("You are an expert Unity engine and C# gameplay engineer diagnosing an automated test failure in DeadlyJenga.")
[void]$sb.AppendLine("Repository: Vanderbilt-VR-2026/DeadlyJenga (Unity 6000.3.23f1)")
[void]$sb.AppendLine("Branch: " + $summary.branch)
[void]$sb.AppendLine("Commit: " + $summary.commitSha)
[void]$sb.AppendLine("")
[void]$sb.AppendLine("The following automated tests failed in this run:")
[void]$sb.AppendLine("")

foreach ($f in $summary.failures) {
    [void]$sb.AppendLine("### Test: " + $f.testName)
    [void]$sb.AppendLine("**Suite:** " + $f.suiteName)
    if ($f.message) {
        [void]$sb.AppendLine('**Failure Message:**')
        [void]$sb.AppendLine('```')
        [void]$sb.AppendLine($f.message)
        [void]$sb.AppendLine('```')
    }
    if ($f.stackTrace) {
        [void]$sb.AppendLine('**Stack Trace:**')
        [void]$sb.AppendLine('```')
        [void]$sb.AppendLine($f.stackTrace)
        [void]$sb.AppendLine('```')
    }
    if ($f.logExcerpt) {
        [void]$sb.AppendLine('**Log Excerpt:**')
        [void]$sb.AppendLine('```')
        [void]$sb.AppendLine($f.logExcerpt)
        [void]$sb.AppendLine('```')
    }
    [void]$sb.AppendLine("")
}

[void]$sb.AppendLine("Please provide a concise, technical diagnosis formatted in Markdown:")
[void]$sb.AppendLine("### Root Cause Analysis")
[void]$sb.AppendLine("Explain exactly what caused the failure based on the assertion message and stack trace.")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("### Affected Files & Methods")
[void]$sb.AppendLine("List the specific C# files and line numbers.")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("### Recommended Fix")
[void]$sb.AppendLine("Provide the exact code change required so ChatGPT or the developer can immediately implement it.")

$prompt = $sb.ToString()
$titleSha = if ($summary.commitSha) { $summary.commitSha.Substring(0, [Math]::Min(7, $summary.commitSha.Length)) } else { "Run" }
$title = "DeadlyJenga Test Diagnostic ($titleSha)"

$convId = $null

if (Test-Path $LanguageServerExe) {
    Write-Host "Invoking language_server.exe agentapi directly..."
    $rawOutput = & $LanguageServerExe "agentapi" "new-conversation" ("--title=" + $title) $prompt 2>&1 | Out-String
    try {
        $json = $rawOutput | ConvertFrom-Json
        $convId = $json.response.newConversation.conversationId
    } catch {
        if ($rawOutput -match '"conversationId":\s*"([^"]+)"') {
            $convId = $matches[1].Trim()
        }
    }
} elseif (Test-Path $AgentApiBat) {
    Write-Host "Invoking agentapi.bat fallback..."
    $flatPrompt = ($prompt -replace "`r", "") -replace "`n", " "
    $rawOutput = & $AgentApiBat "new-conversation" ("--title=" + $title) $flatPrompt 2>&1 | Out-String
    if ($rawOutput -match '"conversationId":\s*"([^"]+)"') {
        $convId = $matches[1].Trim()
    } elseif ($rawOutput -match "Conversation opened:\s*([a-f0-9\-]+)") {
        $convId = $matches[1].Trim()
    }
} else {
    Write-Warning "Neither language_server.exe nor agentapi.bat found. Skipping diagnostic."
    exit 0
}

if (-not $convId) {
    Write-Warning "Failed to obtain conversation ID from Antigravity agent output:`n$rawOutput"
    exit 1
}

Write-Host "Antigravity Agent Session opened: $convId"
$appData = [System.Environment]::GetFolderPath('UserProfile')
$transcriptPath = Join-Path $appData ".gemini\antigravity\brain\$convId\.system_generated\logs\transcript.jsonl"

$sw = [System.Diagnostics.Stopwatch]::StartNew()
$diagnosticText = $null

while ($sw.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
    Start-Sleep -Seconds 3

    if (Test-Path $transcriptPath) {
        $lines = Get-Content $transcriptPath -Encoding utf8 -ErrorAction SilentlyContinue
        foreach ($line in $lines) {
            if (-not $line) { continue }
            try {
                $obj = $line | ConvertFrom-Json
                if ($obj.source -eq "MODEL" -and $obj.type -eq "PLANNER_RESPONSE" -and $obj.status -eq "DONE" -and $obj.content) {
                    $diagnosticText = $obj.content
                    break
                }
            } catch {}
        }
        if ($diagnosticText) {
            break
        }
    }
}

if (-not $diagnosticText) {
    Write-Warning "Timed out waiting for Antigravity diagnostic report ($TimeoutSeconds s)."
    $diagnosticText = "Antigravity diagnostic timed out after $TimeoutSeconds seconds."
}

Write-Host "Antigravity diagnostic generated successfully!"

# Update summary JSON
$summary.diagnostics.antigravityInvoked = $true
$summary.diagnostics.antigravitySessionId = $convId
$summary.diagnostics.antigravityReport = $diagnosticText
$summary | ConvertTo-Json -Depth 6 | Set-Content -Path $SummaryJsonPath -Encoding utf8

# Format Markdown Section
$diagMd = [System.Text.StringBuilder]::new()
[void]$diagMd.AppendLine("")
[void]$diagMd.AppendLine("---")
[void]$diagMd.AppendLine("## 🤖 Antigravity Intelligent Diagnostic Report")
[void]$diagMd.AppendLine("> **Agent Session:** ``" + $convId + "`` | **Analyzed:** " + ((Get-Date).ToString('yyyy-MM-dd HH:mm:ss UTC')))
[void]$diagMd.AppendLine("")
[void]$diagMd.AppendLine($diagnosticText)
[void]$diagMd.AppendLine("")

$diagSection = $diagMd.ToString()

if ($OutputMarkdownPath -and (Test-Path $OutputMarkdownPath)) {
    Add-Content -Path $OutputMarkdownPath -Value $diagSection -Encoding utf8
}

$reportArtifact = Join-Path (Split-Path $SummaryJsonPath -Parent) "antigravity-diagnostic.md"
$diagSection | Set-Content -Path $reportArtifact -Encoding utf8
Write-Host "Diagnostic report saved to: $reportArtifact"

exit 0
