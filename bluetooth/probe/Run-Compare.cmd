@echo off
cd /d "%~dp0"
SwitchServiceProbe.exe --compare-phone
echo.
echo Send logs\switch2-service-probe\current.log
pause
