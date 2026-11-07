@echo off
cd /d "%~dp0"
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Collect-Hardware.ps1"
if errorlevel 1 echo Collection failed. Please send a screenshot of the error.
pause
