@echo off
chcp 65001 >nul
:: ===== AD User Manager - go service (giu lai du lieu) =====
net session >nul 2>&1
if %errorlevel% neq 0 (
    powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0uninstall-service.ps1" %*
pause
