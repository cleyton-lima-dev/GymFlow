@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-AccessAgent.ps1" %*
exit /b %errorlevel%