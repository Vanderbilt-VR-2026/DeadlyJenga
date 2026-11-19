param(
    [string]$Output = (Join-Path ([System.IO.Path]::GetTempPath()) ('DeadlyJenga-Banter-' + [guid]::NewGuid()))
)
$ErrorActionPreference = 'Stop'
$lane = Split-Path $PSScriptRoot -Parent
New-Item -Path $Output -ItemType Directory -Force | Out-Null
$enum = [regex]::Match((Get-Content "$lane/Core/SocialSimulation.cs" -Raw), 'public enum SocialEventKind \{[^}]+\}').Value
if (!$enum) { throw 'Could not read the game event enum.' }
@"
namespace DeadlyJenga.CastleSocial
{
    $enum
    sealed class ObservedSpeech
    {
        internal SocialEventKind Kind;
        internal string Fact, HeardText;
        internal bool HeardOnly;
    }
}
"@ | Set-Content "$Output/EventShim.cs" -Encoding utf8
$sdk = dotnet --list-sdks | Select-Object -Last 1
if ($sdk -notmatch '^([^\s]+)\s+\[(.*)\]$') { throw 'Install the .NET SDK to run the standalone corpus.' }
$sdkRoot = $Matches[2]
$compiler = Join-Path (Join-Path $sdkRoot $Matches[1]) 'Roslyn/bincore/csc.dll'
$frameworks = Join-Path (Split-Path $sdkRoot -Parent) 'packs/Microsoft.NETCore.App.Ref'
$framework = Get-ChildItem $frameworks -Directory | Sort-Object {[version]$_.Name} | Select-Object -Last 1
$target = Get-ChildItem "$($framework.FullName)/ref" -Directory | Select-Object -First 1
$references = @('System.Runtime.dll','System.Console.dll','System.Linq.dll','System.Collections.dll') |
    ForEach-Object { '-r:'+(Join-Path $target.FullName $_) }
dotnet $compiler -nologo -target:exe "-out:$Output/Checks.dll" $references "$PSScriptRoot/MatchBanterCorpus.cs" "$Output/EventShim.cs" "$lane/Runtime/MatchBanter.cs" "$lane/Runtime/DialogueRecency.cs"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$runtime = (dotnet --list-runtimes | Select-String '^Microsoft.NETCore.App ' | Select-Object -Last 1).Line.Split(' ')[1]
@{ runtimeOptions = @{ tfm = $target.Name; framework = @{name='Microsoft.NETCore.App';version=$runtime} } } |
    ConvertTo-Json -Depth 5 | Set-Content "$Output/Checks.runtimeconfig.json" -Encoding utf8
dotnet "$Output/Checks.dll" | Tee-Object "$Output/results.txt"
exit $LASTEXITCODE
