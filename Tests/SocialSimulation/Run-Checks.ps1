$ErrorActionPreference = 'Stop'
$socialRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$socialOutput = Join-Path $socialRoot 'Temp/SocialSimulation'
New-Item -ItemType Directory -Force $socialOutput | Out-Null
$socialDotnet = 'C:/Program Files/dotnet/dotnet.exe'
$socialRuntime = Split-Path $socialDotnet
$socialSdk = Get-ChildItem (Join-Path $socialRuntime 'sdk') -Directory | Sort-Object Name -Descending | Select-Object -First 1
$socialRefs = Get-ChildItem (Join-Path $socialRuntime 'packs/Microsoft.NETCore.App.Ref') -Directory | Where-Object Name -Like '8.*' | Sort-Object Name -Descending | Select-Object -First 1
if (!$socialRefs) { throw 'Install the .NET 8 SDK.' }
$socialArguments = [Collections.Generic.List[string]]::new()
@('/target:exe','/langversion:9','/nostdlib+',('/out:"'+(Join-Path $socialOutput 'SocialChecks.dll')+'"')) | ForEach-Object { $socialArguments.Add($_) }
Get-ChildItem (Join-Path $socialRefs.FullName 'ref/net8.0') -Filter '*.dll' | ForEach-Object { $socialArguments.Add('/reference:"'+$_.FullName+'"') }
Get-ChildItem (Join-Path $socialRoot 'Assets/_DeadlyJenga/GameSession/Session/CastleSocial/Core') -Filter '*.cs' | ForEach-Object { $socialArguments.Add('"'+$_.FullName+'"') }
$socialArguments.Add('"'+(Join-Path $socialRoot 'Assets/_DeadlyJenga/GameSession/Session/Scripts/CastleSocialChannel.cs')+'"')
$socialArguments.Add('"'+(Join-Path $PSScriptRoot 'Program.cs')+'"')
$socialResponse = Join-Path $socialOutput 'compile.rsp'
[IO.File]::WriteAllLines($socialResponse,$socialArguments)
& $socialDotnet (Join-Path $socialSdk.FullName 'Roslyn/bincore/csc.dll') "@$socialResponse"
if ($LASTEXITCODE -ne 0) { throw 'Social checks compilation failed.' }
Set-Content (Join-Path $socialOutput 'SocialChecks.runtimeconfig.json') '{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"8.0.0"}}}'
& $socialDotnet (Join-Path $socialOutput 'SocialChecks.dll')
if ($LASTEXITCODE -ne 0) { throw 'Social checks failed.' }
