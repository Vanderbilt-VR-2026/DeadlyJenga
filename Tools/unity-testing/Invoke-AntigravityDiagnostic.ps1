<#
.SYNOPSIS
    Invokes Antigravity Desktop via AgentAPI to diagnose DeadlyJenga test failures.
.DESCRIPTION
    Reads test-summary.json, formats a structured diagnostic request, invokes agentapi.bat,
    monitors the agent's transcript until completion, extracts the root-cause analysis and
    recommended fix, and incorporates the diagnostic report into test-summary.json and markdown reports.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [string]$SummaryJsonPath,
    [string]$OutputMarkdownPath = "",
    [int]$TimeoutSeconds = 180,
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

if (-not (Test-Path $AgentApiBat)) {
    Write-Warning "AgentAPI batch file not found at '$AgentApiBat'. Skipping AI diagnostic."
    exit 0
}

Write-Host "=================================================="
Write-Host " Launching Antigravity Diagnostic Agent..."
Write-Host "=================================================="

# Build prompt
$sb = [System.Text.StringBuilder]::new()
[void]$sb.AppendLine("You are an expert Unity engine and C# gameplay engineer diagnosing an automated test failure in DeadlyJenga.")
[void]$sb.AppendLine("Repository: Vanderbilt-VR-2026/DeadlyJenga")
[void]$sb.AppendLine("Engine: Unity 6000.3.23f1")
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

[void]$sb.AppendLine("Please analyze this failure and provide:")
[void]$sb.AppendLine("1. Root cause analysis: Why did this test fail?")
[void]$sb.AppendLine("2. Impacted files and methods: What exact source files are responsible?")
[void]$sb.AppendLine("3. Concrete fix: Provide the exact C# code correction so ChatGPT or the developer can immediately implement it.")
[void]$sb.AppendLine("Keep your response structured, concise, and directly actionable.")

$prompt = $sb.ToString()

# Execute agentapi.bat new-conversation
$titleSha = if ($summary.commitSha) { $summary.commitSha.Substring(0, [Math]::Min(7, $summary.commitSha.Length)) } else { "Run" }
$titleArg = "--title=DeadlyJenga Test Diagnostic - " + $titleSha
$sanitizedPrompt = $prompt.Replace('"', '\"')

$agentOutput = & $AgentApiBat "new-conversation" $titleArg $sanitizedPrompt 2>&1 | Out-String

Write-Host "AgentAPI Response:"
Write-Host $agentOutput

$convId = $null
if ($agentOutput -match "Conversation opened:\s*([a-f0-9\-]+)") {
    $convId = $matches[1].Trim()
}

if (-not $convId) {
    Write-Warning "Could not parse conversation ID from AgentAPI output."
    exit 1
}

Write-Host "Diagnosing with Antigravity Conversation: $convId"
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
