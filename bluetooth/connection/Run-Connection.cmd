@echo off
cd /d "%~dp0"
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Collect-Connection.ps1"
if errorlevel 1 echo Collection failed. Please send the error shown above.
pause
