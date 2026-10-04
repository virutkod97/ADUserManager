@echo off
chcp 65001 >nul
:: ===== AD User Manager - cai dat / nang cap service =====
:: Tu dong xin quyen Administrator neu chua co
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo Dang xin quyen Administrator...
    powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install-service.ps1" %*
if %errorlevel% neq 0 (
    echo.
    echo *** CAI DAT THAT BAI - xem thong bao loi o tren ***
) else (
    echo.
    echo *** CAI DAT THANH CONG ***
)
pause
