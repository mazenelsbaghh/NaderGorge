@echo off
cd /d "%~dp0"
where py >nul 2>nul
if %errorlevel% equ 0 (
  py -3 run.py
) else (
  python run.py
)
echo Source mode requires Python 3.10+ and Go.
pause
