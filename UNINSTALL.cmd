@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0ModTools.ps1" -Action Uninstall %*
set "mod_exit=%errorlevel%"
pause
exit /b %mod_exit%
