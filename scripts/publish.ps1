<#
.SYNOPSIS
  Build bản self-contained cho Windows x64 (máy chủ AD không cần cài .NET).
.EXAMPLE
  .\scripts\publish.ps1
  .\scripts\publish.ps1 -Version 1.2.0 -Package   # thêm file zip + .sha256 để đưa lên GitHub Release
#>
param(
    [string]$Output = (Join-Path $PSScriptRoot "..\publish\ADUserManager-win-x64"),
    [string]$Runtime = "win-x64",
    [string]$Version,
    [switch]$Package
)
$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "..\src\ADUserManager\ADUserManager.csproj"

$publishArgs = @(
    "publish", $project, "-c", "Release", "-r", $Runtime, "--self-contained", "true", "-o", $Output,
    "-p:PublishSingleFile=true", "-p:EnableCompressionInSingleFile=true",
    "-p:IncludeNativeLibrariesForSelfExtract=true", "-p:DebugType=none"
)
if ($Version) { $publishArgs += "-p:Version=$Version" }
dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { throw "dotnet publish thất bại" }

foreach ($f in "install-service.ps1", "uninstall-service.ps1", "install.bat", "uninstall.bat", "HUONG-DAN.txt") {
    Copy-Item (Join-Path $PSScriptRoot $f) $Output -Force
}
Remove-Item (Join-Path $Output "web.config") -ErrorAction SilentlyContinue
$outFull = (Resolve-Path $Output).Path
Write-Host "Đã publish ra: $outFull" -ForegroundColor Green

if ($Package) {
    $ver = if ($Version) { $Version } else { "dev" }
    $zip = Join-Path (Split-Path $outFull) "ADUserManager-$ver-$Runtime.zip"
    Remove-Item $zip, "$zip.sha256" -ErrorAction SilentlyContinue
    Compress-Archive -Path $outFull -DestinationPath $zip -CompressionLevel Optimal
    $hash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $(Split-Path $zip -Leaf)" | Set-Content "$zip.sha256" -Encoding ascii -NoNewline
    Write-Host "Gói: $zip"
    Write-Host "SHA-256: $hash"
}
