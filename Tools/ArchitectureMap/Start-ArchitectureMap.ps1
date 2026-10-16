param([int]$Port = 47831, [switch]$NoBrowser)
$ErrorActionPreference = 'Stop'
$mapRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$mapProject = Join-Path $PSScriptRoot 'ArchitectureMap.csproj'
if (!(Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'Install the free .NET 10 SDK, then run this launcher again: https://dotnet.microsoft.com/download/dotnet/10.0' }
try { $mapExisting = Invoke-RestMethod "http://127.0.0.1:$Port/api/heartbeat" -TimeoutSec 1 } catch { $mapExisting = $null }
if ($mapExisting) { if (!$NoBrowser) { Start-Process "http://127.0.0.1:$Port" }; exit 0 }
$env:DOTNET_CLI_HOME = Join-Path $mapRoot '.architecture/local/dotnet'
$env:APPDATA = Join-Path $mapRoot '.architecture/local/appdata'
& dotnet restore $mapProject --configfile (Join-Path $PSScriptRoot 'NuGet.Config')
if ($LASTEXITCODE) { throw 'Architecture Map restore failed.' }
& dotnet build $mapProject --no-restore --nologo
if ($LASTEXITCODE) { throw 'Architecture Map build failed.' }
$mapArgs = @((Join-Path $PSScriptRoot 'bin/Debug/net10.0/ArchitectureMap.dll'), '--root', $mapRoot, '--port', $Port)
if (!$NoBrowser) { $mapArgs += '--open' }
& dotnet @mapArgs
