@echo off
title Massar host LAN folder repair
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0repair-lan-firewall.ps1"
set "MassarRepairExit=%ERRORLEVEL%"
if not "%MassarRepairExit%"=="0" echo Repair did not finish. Send a photo showing the reason above. This tool needs Run as administrator.
pause
exit /b %MassarRepairExit%
