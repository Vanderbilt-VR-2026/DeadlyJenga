@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Tools\ArchitectureMap\Start-ArchitectureMap.ps1"
if errorlevel 1 pause
