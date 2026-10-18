@echo off
where pwsh.exe >nul 2>nul
if errorlevel 1 (
  echo Install PowerShell 7 x64 from Microsoft first:
  echo https://learn.microsoft.com/powershell/scripting/install/installing-powershell-on-windows
  pause
  exit /b 1
)
start "" pwsh.exe -NoProfile -STA -WindowStyle Hidden -File "%~dp0app\Listen.ps1"
