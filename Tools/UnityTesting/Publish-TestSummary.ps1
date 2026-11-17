<#
Turns results.json into a short Markdown report (report.md) next to it.
Watch-Branches.ps1 copies it to the test-results branch as latest.md.
#>
param([Parameter(Mandatory)] [string] $ResultsDir)
$ErrorActionPreference = 'Stop'
$json = Join-Path $ResultsDir 'results.json'
if (-not (Test-Path $json)) {
    $r = [pscustomobject]@{ runner_status = 'infrastructure_failure'; test_status = 'not_run'
        message = 'results.json was not produced.'; failed_tests = @(); log_excerpt = @() }
    $r | ConvertTo-Json | Set-Content -Encoding utf8 $json
} else { $r = Get-Content $json -Raw | ConvertFrom-Json }

$title = switch ($r.test_status) { 'passed' { 'PASSED' } 'failed' { 'FAILED' } default { 'NOT RUN' } }
$lines = @(
    "## Unity tests: $title",
    '',
    '| Field | Value |', '|---|---|',
    "| Commit | ``$($r.commit_sha)`` |",
    "| Branch | ``$($r.branch)`` |",
    "| Runner status | $($r.runner_status) |",
    "| Test status | $($r.test_status) |",
    "| Passed / Failed / Skipped | $($r.tests_passed) / $($r.tests_failed) / $($r.tests_skipped) |",
    "| Unity | $($r.unity_version) ($($r.platform)) |",
    "| Duration | $($r.duration_seconds) s |",
    "| Tested at | $((Get-Date).ToString('yyyy-MM-dd HH:mm zzz')) on $($r.runner) |"
)
if ($r.message) { $lines += @('', "**Problem:** $($r.message)") }
if ($r.failed_tests.Count) {
    $lines += @('', '### Failed tests')
    foreach ($t in $r.failed_tests) { $lines += "- ``$($t.name)``: $(($t.message -replace '\s+', ' ').Trim())" }
}
if ($r.log_excerpt.Count) {
    $lines += @('', '### Log excerpt', '```')
    $lines += @($r.log_excerpt | Select-Object -First 25)
    $lines += '```'
}
$lines += @('', 'Full NUnit XML: latest.xml next to this file. The full editor log stays on the test PC.')
$body = ($lines -join "`n")
# Never publish a token even if one leaked into a log line.
$body = $body -replace '(gh[pousr]_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,})', '[redacted]'
Set-Content -Encoding utf8 (Join-Path $ResultsDir 'report.md') $body
