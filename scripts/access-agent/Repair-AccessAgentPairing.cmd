@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Repair-AccessAgentPairing.ps1" %*
exit /b %errorlevel%