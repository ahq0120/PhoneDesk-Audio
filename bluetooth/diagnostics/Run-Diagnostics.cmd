@echo off
cd /d "%~dp0"
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Collect-AudioDiagnostics.ps1"
if errorlevel 1 echo Diagnostic failed. Please copy the error shown above.
pause
