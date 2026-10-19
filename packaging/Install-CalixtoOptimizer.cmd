@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-CalixtoOptimizer.ps1"
if errorlevel 1 (
  echo.
  echo A instalacao falhou. Consulte a mensagem acima.
  pause
  exit /b 1
)
echo.
pause
