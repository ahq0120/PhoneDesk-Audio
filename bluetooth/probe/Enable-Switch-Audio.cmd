@echo off
cd /d "%~dp0"
SwitchServiceProbe.exe --enable-audio
echo.
echo Return to PhoneDesk and refresh. Send logs\switch2-service-probe\current.log
pause
