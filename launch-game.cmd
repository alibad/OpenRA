@echo off
setlocal
title OpenRA
cd /d "%~dp0"

powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0launch-game.ps1" %*
set "openraExitCode=%ERRORLEVEL%"

if "%openraExitCode%" EQU "0" exit /b 0

set "logs=%AppData%\OpenRA\Logs"
if exist "%USERPROFILE%\Documents\OpenRA\Logs" set "logs=%USERPROFILE%\Documents\OpenRA\Logs"
if exist "%~dp0Support\Logs" set "logs=%~dp0Support\Logs"

echo ----------------------------------------
echo OpenRA has encountered a fatal error.
echo   * Log Files are available in %logs%
echo   * FAQ is available at https://github.com/OpenRA/OpenRA/wiki/FAQ
echo ----------------------------------------
pause
exit /b %openraExitCode%
