<#
Turns results.json into a short Markdown report, adds it to the GitHub job summary,
and posts it where the ChatGPT GitHub connector can read it:
  - an open PR for the branch gets ONE comment that is edited on every run;
  - otherwise the commit gets a comment.
Needs GITHUB_TOKEN, GITHUB_REPOSITORY, GITHUB_SERVER_URL and GITHUB_RUN_ID (set by Actions).
#>
param(
    [Parameter(Mandatory)] [string] $ResultsDir,
    [switch] $NoComment
)
$ErrorActionPreference = 'Stop'
$json = Join-Path $ResultsDir 'results.json'
if (-not (Test-Path $json)) {
    $r = [pscustomobject]@{ runner_status = 'infrastructure_failure'; test_status = 'not_run'
        message = 'results.json was not produced.'; failed_tests = @(); log_excerpt = @() }
} else { $r = Get-Content $json -Raw | ConvertFrom-Json }

$runUrl = "$env:GITHUB_SERVER_URL/$env:GITHUB_REPOSITORY/actions/runs/$env:GITHUB_RUN_ID"
$icon = switch ($r.test_status) { 'passed' { 'PASSED' } 'failed' { 'FAILED' } default { 'NOT RUN' } }
$marker = '<!-- deadlyjenga-unity-tests -->'
$lines = @(
    $marker,
    "## Unity tests: $icon",
    '',
    '| Field | Value |', '|---|---|',
    "| Commit | ``$($r.commit_sha)`` |",
    "| Branch | ``$($r.branch)`` |",
    "| Runner status | $($r.runner_status) |",
    "| Test status | $($r.test_status) |",
    "| Passed / Failed / Skipped | $($r.tests_passed) / $($r.tests_failed) / $($r.tests_skipped) |",
    "| Unity | $($r.unity_version) ($($r.platform)) |",
    "| Duration | $($r.duration_seconds) s |",
    "| Workflow run | $runUrl |"
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
$diag = Join-Path $ResultsDir 'antigravity-report.md'
if (Test-Path $diag) {
    $lines += @('', '### Antigravity diagnostic (advisory, not verified)', '')
    $lines += @(Get-Content $diag | Select-Object -First 60)
}
$lines += @('', "Full XML, editor log and results.json: artifact ``unity-test-results`` on the workflow run.")
$body = ($lines -join "`n")
# Never publish a token even if one leaked into a log line.
$body = $body -replace '(gh[pousr]_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,})', '[redacted]'
Set-Content -Encoding utf8 (Join-Path $ResultsDir 'report.md') $body
if ($env:GITHUB_STEP_SUMMARY) { Add-Content -Encoding utf8 $env:GITHUB_STEP_SUMMARY $body }
if ($NoComment -or -not $env:GITHUB_TOKEN) { return }

$api = "https://api.github.com/repos/$env:GITHUB_REPOSITORY"
$h = @{ Authorization = "Bearer $env:GITHUB_TOKEN"; Accept = 'application/vnd.github+json' }
$payload = @{ body = $body } | ConvertTo-Json
try {
    $owner = $env:GITHUB_REPOSITORY.Split('/')[0]
    $prs = @(Invoke-RestMethod -Headers $h "$api/pulls?state=open&head=${owner}:$($r.branch)")
    if ($prs.Count) {
        $n = $prs[0].number
        $old = @(Invoke-RestMethod -Headers $h "$api/issues/$n/comments?per_page=100") |
            Where-Object { $_.body -like "$marker*" } | Select-Object -First 1
        if ($old) { Invoke-RestMethod -Method Patch -Headers $h -Body $payload "$api/issues/comments/$($old.id)" | Out-Null }
        else { Invoke-RestMethod -Method Post -Headers $h -Body $payload "$api/issues/$n/comments" | Out-Null }
        Write-Host "Report posted on PR #$n"
    } else {
        Invoke-RestMethod -Method Post -Headers $h -Body $payload "$api/commits/$($r.commit_sha)/comments" | Out-Null
        Write-Host "Report posted on commit $($r.commit_sha)"
    }
} catch { Write-Host "::warning::Could not post the report comment: $($_.Exception.Message)" }
