@echo off
rem La Plata West Signal Watch v0.31
cd /d "%~dp0"
"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -STA -ExecutionPolicy Bypass -File "%~dp0WestSignalWatch.ps1"
if errorlevel 1 pause
