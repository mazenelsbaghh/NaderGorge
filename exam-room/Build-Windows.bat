@echo off
setlocal
cd /d "%~dp0"
where go >nul 2>nul
if errorlevel 1 (
  echo Go is required to build the local server. Install Go and retry.
  goto failed
)
where npm >nul 2>nul
if errorlevel 1 (
  echo Node.js and npm are required to build the React desktop interface.
  goto failed
)
py -3 -m venv .build-env
if errorlevel 1 goto failed
.build-env\Scripts\python.exe -m pip install -r requirements-desktop.txt
if errorlevel 1 goto failed
.build-env\Scripts\python.exe build_desktop.py
if errorlevel 1 goto failed
set "ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" set "ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" (
  echo Inno Setup 6 is required to create the Windows installer.
  goto failed
)
"%ISCC%" "packaging\Massar-Windows.iss"
if errorlevel 1 goto failed
echo Ready: dist\Massar-Exam-Room-Windows-Setup.exe
pause
exit /b 0
:failed
echo Build failed. Install Python 3.10+, Go, Node.js and Inno Setup 6, then check the error above.
pause
exit /b 1
