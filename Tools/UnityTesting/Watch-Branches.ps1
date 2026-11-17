<#
Polling bridge: watches GitHub for new commits on test branches, runs the Unity tests
on this PC, and pushes the results to the `test-results` branch, where people and
ChatGPT's GitHub connector can read them.

  .\Watch-Branches.ps1 -Once     # check once, test anything new, exit
  .\Watch-Branches.ps1           # keep checking every -IntervalSeconds

It runs from C:\DeadlyJengaCI\bin (a copy made by Install-Bridge.ps1), never from the
checkout it tests, so a pushed commit cannot change the bridge itself.
Uses your normal git login. Opens no ports.
#>
param(
    [string] $Root = 'C:\DeadlyJengaCI',
    [string[]] $Patterns = @('chatgpt/*', 'ci/*', 'test/*'),
    [int] $IntervalSeconds = 90,
    [int] $TimeoutMinutes = 120,
    [switch] $Once
)
# Continue: git writes progress to stderr, which PowerShell 5.1 would treat as an error.
# Failures are caught through exit codes instead.
$ErrorActionPreference = 'Continue'
$repo = Join-Path $Root 'repo'
$resultsRepo = Join-Path $Root 'results'
$stateFile = Join-Path $Root 'state.json'
$logFile = Join-Path $Root 'bridge.log'
$bin = $PSScriptRoot
$resultsBranch = 'test-results'

function Log($text) {
    $line = "$((Get-Date).ToString('s')) $text"
    Write-Host $line; Add-Content -Encoding utf8 $logFile $line
}
function Git([string] $dir) {
    # Runs git with the remaining arguments; throws on failure.
    $out = & git.exe -C $dir @args 2>&1
    if ($LASTEXITCODE -ne 0) { throw "git $($args -join ' ') failed: $out" }
    $out
}

# Only one bridge at a time.
$lock = $null
try { $lock = [IO.File]::Open((Join-Path $Root 'bridge.lock'), 'OpenOrCreate', 'ReadWrite', 'None') }
catch { Write-Host 'Another bridge is already running.'; exit 0 }

function Load-State {
    if (Test-Path $stateFile) { Get-Content $stateFile -Raw | ConvertFrom-Json } else { [pscustomobject]@{} }
}
function Save-State($state) { $state | ConvertTo-Json | Set-Content -Encoding utf8 $stateFile }

function Ensure-ResultsRepo {
    if (Test-Path (Join-Path $resultsRepo '.git')) { return }
    Git $repo worktree prune | Out-Null
    $remote = & git.exe -C $repo ls-remote --heads origin $resultsBranch
    if ($remote) {
        Git $repo fetch origin "+refs/heads/${resultsBranch}:refs/remotes/origin/$resultsBranch" | Out-Null
        Git $repo worktree add --force -B $resultsBranch $resultsRepo "origin/$resultsBranch" | Out-Null
    } else {
        # --no-checkout: the results branch holds only reports, never the game files.
        Git $repo worktree add --no-checkout --detach $resultsRepo | Out-Null
        Git $resultsRepo switch --orphan $resultsBranch | Out-Null
        Git $resultsRepo rm -r -q --cached --ignore-unmatch . | Out-Null
        Set-Content -Encoding utf8 (Join-Path $resultsRepo 'README.md') @"
# Unity test results

Written automatically by the DeadlyJenga test bridge (Tools/UnityTesting/Watch-Branches.ps1).
- results/<branch>/latest.md   readable report for the newest tested commit
- results/<branch>/latest.json machine-readable result (schema in Docs/automated-testing.md)
- results/<branch>/history/<commit>.json one file per tested commit
Branch names use '__' instead of '/'. Do not edit by hand.
"@
        Git $resultsRepo add README.md | Out-Null
        Git $resultsRepo commit -q -m 'Start test results branch' | Out-Null
    }
}

function Publish($branch, $sha, $dir) {
    $safe = $branch -replace '[^A-Za-z0-9._-]', '__'
    $target = Join-Path $resultsRepo "results\$safe"
    New-Item -ItemType Directory -Force (Join-Path $target 'history') | Out-Null
    Copy-Item (Join-Path $dir 'results.json') (Join-Path $target 'latest.json') -Force -ErrorAction Stop
    Copy-Item (Join-Path $dir 'results.json') (Join-Path $target "history\$sha.json") -Force -ErrorAction Stop
    Copy-Item (Join-Path $dir 'report.md') (Join-Path $target 'latest.md') -Force -ErrorAction Stop
    if (Test-Path (Join-Path $dir 'results.xml')) { Copy-Item (Join-Path $dir 'results.xml') (Join-Path $target 'latest.xml') -Force }
    $r = Get-Content (Join-Path $dir 'results.json') -Raw | ConvertFrom-Json
    for ($try = 1; $try -le 4; $try++) {
        try {
            Git $resultsRepo add -A | Out-Null
            & git.exe -C $resultsRepo diff --cached --quiet
            if ($LASTEXITCODE -ne 0) {
                Git $resultsRepo commit -q -m "Unity tests $($r.test_status) for $branch @ $($sha.Substring(0,8))" | Out-Null
            }
            Git $resultsRepo -c push.followTags=false push origin "HEAD:refs/heads/$resultsBranch" | Out-Null
            Log "published $branch $sha -> results/$safe/latest.md"
            return
        } catch {
            Log "push attempt $try failed: $($_.Exception.Message)"
            & git.exe -C $resultsRepo pull -q --rebase origin $resultsBranch 2>&1 | Out-Null
            Start-Sleep (15 * $try)
        }
    }
    Log "could not publish $branch $sha; will retry next cycle"
    throw 'publish failed'
}

function Test-Commit($branch, $sha) {
    Log "testing $branch $sha"
    Git $repo checkout -q --force --detach $sha | Out-Null
    Git $repo clean -q -fd | Out-Null     # keeps ignored folders such as Library/
    $dir = Join-Path $Root "runs\$($sha.Substring(0,12))"
    $env:DJ_BRANCH = $branch
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $bin 'Run-UnityTests.ps1') `
        -ProjectPath $repo -ResultsDir $dir -TimeoutMinutes $TimeoutMinutes -TaskId "bridge-$($sha.Substring(0,12))"
    $code = $LASTEXITCODE
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $bin 'Publish-TestSummary.ps1') -ResultsDir $dir
    # Unity may touch settings files while importing; drop that so the next checkout is clean.
    & git.exe -C $repo checkout -q --force -- . 2>&1 | Out-Null
    Publish $branch $sha $dir
    $code
}

function Cycle {
    Ensure-ResultsRepo
    $state = Load-State
    $refs = @(); foreach ($p in $Patterns) { $refs += "+refs/heads/${p}:refs/remotes/origin/$p" }
    Git $repo fetch -q --prune origin @refs | Out-Null
    $heads = foreach ($p in $Patterns) {
        & git.exe -C $repo for-each-ref --format='%(refname:lstrip=3) %(objectname)' "refs/remotes/origin/$($p.TrimEnd('*'))"
    }
    foreach ($line in $heads) {
        if (-not $line) { continue }
        $branch, $sha = $line -split ' '
        if ($state.$branch -eq $sha) { continue }
        $null = Test-Commit $branch $sha
        $state | Add-Member -NotePropertyName $branch -NotePropertyValue $sha -Force
        Save-State $state
    }
}

try {
    Log "bridge started (patterns: $($Patterns -join ', '))"
    while ($true) {
        try { Cycle } catch { Log "cycle error: $($_.Exception.Message)" }
        if ($Once) { break }
        Start-Sleep $IntervalSeconds
    }
} finally { $lock.Close() }
