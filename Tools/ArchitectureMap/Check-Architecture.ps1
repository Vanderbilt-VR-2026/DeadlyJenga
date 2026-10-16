param([string]$Base = 'HEAD')
$ErrorActionPreference = 'Stop'
$mapRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
& dotnet (Join-Path $PSScriptRoot 'bin/Debug/net10.0/ArchitectureMap.dll') --root $mapRoot --check --base $Base
exit $LASTEXITCODE
