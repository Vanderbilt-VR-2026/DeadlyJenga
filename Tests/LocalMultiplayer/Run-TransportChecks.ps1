$ErrorActionPreference = 'Stop'
$lanRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$lanOutput = Join-Path $lanRoot 'Temp/LanValidation/Transport'
New-Item -ItemType Directory -Force $lanOutput | Out-Null
$lanDotnet = Split-Path (Get-Command dotnet).Source
$lanSdk = Get-ChildItem (Join-Path $lanDotnet 'sdk') -Directory | Sort-Object Name -Descending | Select-Object -First 1
$lanRefs = Get-ChildItem (Join-Path $lanDotnet 'packs/Microsoft.NETCore.App.Ref') -Directory | Where-Object Name -Like '8.*' | Sort-Object Name -Descending | Select-Object -First 1
if (!$lanRefs) { throw 'Install the .NET 8 SDK to run the transport checks.' }
$lanArguments = [Collections.Generic.List[string]]::new()
$lanArguments.Add('/target:exe'); $lanArguments.Add('/langversion:9'); $lanArguments.Add('/nostdlib+')
$lanArguments.Add('/out:"' + (Join-Path $lanOutput 'TransportChecks.dll') + '"')
Get-ChildItem (Join-Path $lanRefs.FullName 'ref/net8.0') -Filter '*.dll' | ForEach-Object { $lanArguments.Add('/reference:"' + $_.FullName + '"') }
$lanArguments.Add('"' + (Join-Path $PSScriptRoot 'TransportChecks.cs') + '"')
$lanArguments.Add('"' + (Join-Path $lanRoot 'Assets/CastlePrototype/Scripts/CastleLanTransport.cs') + '"')
$lanResponse = Join-Path $lanOutput 'compile.rsp'
[IO.File]::WriteAllLines($lanResponse, $lanArguments)
& dotnet (Join-Path $lanSdk.FullName 'Roslyn/bincore/csc.dll') "@$lanResponse"
if ($LASTEXITCODE -ne 0) { throw 'Transport test compilation failed.' }
Set-Content (Join-Path $lanOutput 'TransportChecks.runtimeconfig.json') '{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"8.0.0"}}}'
& dotnet (Join-Path $lanOutput 'TransportChecks.dll')
if ($LASTEXITCODE -ne 0) { throw 'Transport checks failed.' }
