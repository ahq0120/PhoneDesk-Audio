@echo off
cd /d "%~dp0"
SwitchServiceProbe.exe
echo.
echo Send logs\switch2-service-probe\current.log after the probe finishes.
pause
