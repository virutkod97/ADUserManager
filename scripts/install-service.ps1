<#
.SYNOPSIS
  Cài đặt / nâng cấp ADUserManager thành Windows Service trên máy chủ AD.

.DESCRIPTION
  - Copy chương trình vào thư mục cài đặt (giữ nguyên cấu hình và dữ liệu khi nâng cấp)
  - Tạo chứng chỉ HTTPS tự ký (nếu chưa có) trong LocalMachine\My với Subject "CN=ADUserManager"
  - Phân quyền thư mục data và file cấu hình chỉ cho SYSTEM + Administrators
  - Tạo service "ADUserManager" (khởi động tự động, tự khởi động lại khi lỗi)
  - Mở firewall cổng HTTPS cho Domain profile

  Cách dễ nhất: chạy install.bat (tự xin quyền Administrator).

.EXAMPLE
  .\install-service.ps1
  .\install-service.ps1 -Port 8443 -InstallDir "D:\Apps\ADUserManager"
#>
param(
    [string]$SourceDir = $PSScriptRoot,
    [string]$InstallDir = "$env:ProgramFiles\ADUserManager",
    [int]$Port = 5443,
    [string]$CertSubject = "ADUserManager",
    [switch]$SkipCertificate,
    [switch]$SkipFirewall
)
$ErrorActionPreference = "Stop"
$ServiceName = "ADUserManager"

$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Hãy chạy script bằng PowerShell 'Run as Administrator'."
}
if (-not (Test-Path (Join-Path $SourceDir "ADUserManager.exe"))) {
    throw "Không thấy ADUserManager.exe trong '$SourceDir'. Hãy chạy publish.ps1 trước hoặc chỉ định -SourceDir."
}

# ---------------------------------------------------------------- dừng service cũ
$svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($svc -and $svc.Status -ne "Stopped") {
    Write-Host "Dừng service hiện tại..."
    Stop-Service $ServiceName -Force
    $svc.WaitForStatus("Stopped", "00:00:30")
}

# ---------------------------------------------------------------- copy file
New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
$srcFull = (Resolve-Path $SourceDir).Path.TrimEnd('\')
$dstFull = (Resolve-Path $InstallDir).Path.TrimEnd('\')
if ($srcFull -ieq $dstFull) {
    Write-Host "Đang chạy ngay trong thư mục cài đặt, bỏ qua bước copy."
}
else {
    Write-Host "Copy chương trình vào $InstallDir ..."
    $keepIfExists = @("appsettings.json", "appsettings.Production.json")
    Get-ChildItem -Path $SourceDir -Force | Where-Object { $_.Name -ne "data" } | ForEach-Object {
        $dest = Join-Path $InstallDir $_.Name
        if ($keepIfExists -contains $_.Name -and (Test-Path $dest)) {
            Write-Host "  Giữ nguyên cấu hình hiện có: $($_.Name)"
            return
        }
        Copy-Item $_.FullName $dest -Recurse -Force
    }
}

# ---------------------------------------------------------------- cổng HTTPS
$prodCfg = Join-Path $InstallDir "appsettings.Production.json"
$cfg = Get-Content $prodCfg -Raw | ConvertFrom-Json
$cfg.Kestrel.Endpoints.Https.Url = "https://0.0.0.0:$Port"
$cfg.Kestrel.Endpoints.Https.Certificate.Subject = $CertSubject
$cfg | ConvertTo-Json -Depth 10 | Set-Content $prodCfg -Encoding UTF8

# ---------------------------------------------------------------- chứng chỉ
if (-not $SkipCertificate) {
    $cert = Get-ChildItem Cert:\LocalMachine\My |
        Where-Object { $_.Subject -eq "CN=$CertSubject" -and $_.NotAfter -gt (Get-Date) -and $_.HasPrivateKey } |
        Sort-Object NotAfter -Descending | Select-Object -First 1
    if (-not $cert) {
        $fqdn = [System.Net.Dns]::GetHostEntry($env:COMPUTERNAME).HostName
        Write-Host "Tạo chứng chỉ HTTPS tự ký cho $fqdn ..."
        $cert = New-SelfSignedCertificate -Subject "CN=$CertSubject" `
            -DnsName @($fqdn, $env:COMPUTERNAME, "localhost") `
            -CertStoreLocation Cert:\LocalMachine\My `
            -NotAfter (Get-Date).AddYears(5) `
            -KeyExportPolicy NonExportable -KeyAlgorithm RSA -KeyLength 2048 `
            -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.1") `
            -FriendlyName "AD User Manager"
    }
    Write-Host "Chứng chỉ: $($cert.Subject)  Thumbprint: $($cert.Thumbprint)  Hết hạn: $($cert.NotAfter)"
    Write-Host "  (Có thể thay bằng chứng chỉ do CA nội bộ cấp: đặt Subject trong appsettings.Production.json)"
}

# ---------------------------------------------------------------- phân quyền
$dataDir = Join-Path $InstallDir "data"
New-Item -ItemType Directory -Force -Path $dataDir | Out-Null
# Chỉ SYSTEM (S-1-5-18) và Administrators (S-1-5-32-544) được truy cập dữ liệu và cấu hình
icacls $dataDir /inheritance:r /grant:r "*S-1-5-18:(OI)(CI)F" "*S-1-5-32-544:(OI)(CI)F" | Out-Null
foreach ($f in @("appsettings.json", "appsettings.Production.json")) {
    icacls (Join-Path $InstallDir $f) /inheritance:r /grant:r "*S-1-5-18:F" "*S-1-5-32-544:F" | Out-Null
}

# ---------------------------------------------------------------- service
$exe = Join-Path $InstallDir "ADUserManager.exe"
if (-not (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue)) {
    Write-Host "Tạo service $ServiceName ..."
    New-Service -Name $ServiceName -BinaryPathName "`"$exe`"" -DisplayName "AD User Manager" `
        -Description "Website hỗ trợ quản trị tài khoản người dùng Active Directory" -StartupType Automatic | Out-Null
}
else {
    sc.exe config $ServiceName binPath= "`"$exe`"" start= auto | Out-Null
}
sc.exe failure $ServiceName reset= 86400 actions= restart/60000/restart/60000/restart/60000 | Out-Null

# ---------------------------------------------------------------- firewall
if (-not $SkipFirewall) {
    $ruleName = "AD User Manager (HTTPS)"
    Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue | Remove-NetFirewallRule
    New-NetFirewallRule -DisplayName $ruleName -Direction Inbound -Protocol TCP -LocalPort $Port `
        -Action Allow -Profile Domain | Out-Null
    Write-Host "Đã mở firewall cổng $Port (Domain profile)."
}

Start-Service $ServiceName
Start-Sleep -Seconds 3
Get-Service $ServiceName | Format-Table -AutoSize Status, Name, DisplayName
$fqdn = [System.Net.Dns]::GetHostEntry($env:COMPUTERNAME).HostName
Write-Host "Truy cập: https://$($fqdn):$Port" -ForegroundColor Green
