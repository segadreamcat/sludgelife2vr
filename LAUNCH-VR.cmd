@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0ModTools.ps1" -Action Launch %*
set "mod_exit=%errorlevel%"
if not "%mod_exit%"=="0" pause
exit /b %mod_exit%
