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

dotnet publish $project -c Release -r $Runtime --self-contained true -o $Output `
    -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none
if ($LASTEXITCODE -ne 0) { throw "dotnet publish thất bại" }

Copy-Item (Join-Path $PSScriptRoot "install-service.ps1") $Output -Force
Copy-Item (Join-Path $PSScriptRoot "uninstall-service.ps1") $Output -Force
Copy-Item (Join-Path $PSScriptRoot "install.bat") $Output -Force
Copy-Item (Join-Path $PSScriptRoot "uninstall.bat") $Output -Force
Copy-Item (Join-Path $PSScriptRoot "HUONG-DAN.txt") $Output -Force
Remove-Item (Join-Path $Output "web.config") -ErrorAction SilentlyContinue
Write-Host "Đã publish ra: $((Resolve-Path $Output).Path)" -ForegroundColor Green
Write-Host "Copy thư mục này lên máy chủ AD và chạy install.bat."
