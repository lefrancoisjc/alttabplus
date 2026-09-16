@echo off
setlocal
cd /d "%~dp0"
where pwsh >nul 2>&1
if %ERRORLEVEL%==0 (
  pwsh -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-release.ps1" %*
) else (
  powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-release.ps1" %*
)
