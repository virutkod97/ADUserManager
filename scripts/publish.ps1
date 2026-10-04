<#
.SYNOPSIS
  Build bản phát hành self-contained cho Windows x64 (không cần cài .NET trên máy chủ AD).
.EXAMPLE
  .\scripts\publish.ps1
  -> kết quả nằm ở thư mục .\publish
#>
param(
    [string]$Output = (Join-Path $PSScriptRoot "..\publish"),
    [string]$Runtime = "win-x64"
)
$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "..\src\ADUserManager\ADUserManager.csproj"

dotnet publish $project -c Release -r $Runtime --self-contained true -o $Output
if ($LASTEXITCODE -ne 0) { throw "dotnet publish thất bại" }

Copy-Item (Join-Path $PSScriptRoot "install-service.ps1") $Output -Force
Copy-Item (Join-Path $PSScriptRoot "uninstall-service.ps1") $Output -Force
Write-Host "Đã publish ra: $((Resolve-Path $Output).Path)" -ForegroundColor Green
Write-Host "Copy thư mục này lên máy chủ AD và chạy install-service.ps1 (Run as Administrator)."
