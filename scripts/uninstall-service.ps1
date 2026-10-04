<#
.SYNOPSIS
  Gỡ service ADUserManager, mặc định giữ lại thư mục cài đặt. Thêm -RemoveFiles để xoá toàn bộ.
#>
param(
    [string]$InstallDir = "$env:ProgramFiles\ADUserManager",
    [switch]$RemoveFiles
)
$ErrorActionPreference = "Stop"
$ServiceName = "ADUserManager"

if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
    Stop-Service $ServiceName -Force -ErrorAction SilentlyContinue
    sc.exe delete $ServiceName | Out-Null
    Write-Host "Đã gỡ service $ServiceName."
}
Get-NetFirewallRule -DisplayName "AD User Manager (HTTPS)" -ErrorAction SilentlyContinue | Remove-NetFirewallRule
if ($RemoveFiles -and (Test-Path $InstallDir)) {
    Remove-Item $InstallDir -Recurse -Force
    Write-Host "Đã xoá $InstallDir."
}
