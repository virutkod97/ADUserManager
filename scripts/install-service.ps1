<#
.SYNOPSIS
  Cài đặt / nâng cấp ADUserManager thành Windows Service. Cách dễ nhất: chạy install.bat.
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
    [switch]$SkipFirewall,
    [switch]$Upgrade,
    [string]$LogFile
)
$ErrorActionPreference = "Stop"
$ServiceName = "ADUserManager"
$FirewallRule = "AD User Manager (HTTPS)"

if ($LogFile) {
    New-Item -ItemType Directory -Force -Path (Split-Path $LogFile) | Out-Null
    Start-Transcript -Path $LogFile -Force | Out-Null
}

try {
    $principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw "Hãy chạy script bằng quyền Administrator (hoặc chạy install.bat)."
    }
    if (-not (Test-Path (Join-Path $SourceDir "ADUserManager.exe"))) {
        throw "Không thấy ADUserManager.exe trong '$SourceDir'."
    }

    $svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($svc -and $svc.Status -ne "Stopped") {
        Write-Host "Dừng service hiện tại..."
        Stop-Service $ServiceName -Force
        $svc.WaitForStatus("Stopped", "00:01:00")
    }

    New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
    $srcFull = (Resolve-Path $SourceDir).Path.TrimEnd('\')
    $dstFull = (Resolve-Path $InstallDir).Path.TrimEnd('\')
    if ($srcFull -ieq $dstFull) {
        Write-Host "Đang chạy ngay trong thư mục cài đặt, bỏ qua bước copy."
    }
    else {
        Write-Host "Copy chương trình vào $InstallDir ..."
        $keepIfExists = @("appsettings.json", "appsettings.Production.json")
        foreach ($item in Get-ChildItem -Path $SourceDir -Force | Where-Object { $_.Name -ne "data" }) {
            $dest = Join-Path $InstallDir $item.Name
            if ($keepIfExists -contains $item.Name -and (Test-Path $dest)) {
                Write-Host "  Giữ nguyên cấu hình hiện có: $($item.Name)"
                continue
            }
            # exe có thể còn bị khoá vài giây sau khi service dừng
            for ($i = 1; ; $i++) {
                try {
                    # Copy-Item thư mục vào thư mục đã tồn tại sẽ tạo thư mục lồng (wwwroot\wwwroot)
                    if ($item.PSIsContainer -and (Test-Path $dest)) { Remove-Item $dest -Recurse -Force }
                    Copy-Item $item.FullName $dest -Recurse -Force
                    break
                }
                catch { if ($i -ge 15) { throw }; Start-Sleep -Seconds 2 }
            }
        }
    }

    $prodCfg = Join-Path $InstallDir "appsettings.Production.json"
    $cfg = Get-Content $prodCfg -Raw | ConvertFrom-Json
    $https = $cfg.Kestrel.Endpoints.Https
    if ($PSBoundParameters.ContainsKey("Port") -or $PSBoundParameters.ContainsKey("CertSubject")) {
        if ($PSBoundParameters.ContainsKey("Port")) { $https.Url = "https://0.0.0.0:$Port" }
        if ($PSBoundParameters.ContainsKey("CertSubject")) { $https.Certificate.Subject = $CertSubject }
        $cfg | ConvertTo-Json -Depth 10 | Set-Content $prodCfg -Encoding UTF8
    }
    if ($https.Url -match ':(\d+)\s*$') { $Port = [int]$Matches[1] }
    $CertSubject = $https.Certificate.Subject

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
    }

    $dataDir = Join-Path $InstallDir "data"
    New-Item -ItemType Directory -Force -Path $dataDir | Out-Null
    # Chỉ SYSTEM và Administrators được đọc dữ liệu và cấu hình
    icacls $dataDir /inheritance:r /grant:r "*S-1-5-18:(OI)(CI)F" "*S-1-5-32-544:(OI)(CI)F" | Out-Null
    foreach ($f in @("appsettings.json", "appsettings.Production.json")) {
        icacls (Join-Path $InstallDir $f) /inheritance:r /grant:r "*S-1-5-18:F" "*S-1-5-32-544:F" | Out-Null
    }

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

    if (-not $SkipFirewall) {
        $existing = Get-NetFirewallRule -DisplayName $FirewallRule -ErrorAction SilentlyContinue
        $needRule = -not $existing
        if ($existing) {
            $currentPort = ($existing | Get-NetFirewallPortFilter).LocalPort
            if ("$currentPort" -ne "$Port") { $existing | Remove-NetFirewallRule; $needRule = $true }
        }
        if ($needRule) {
            New-NetFirewallRule -DisplayName $FirewallRule -Direction Inbound -Protocol TCP -LocalPort $Port `
                -Action Allow -Profile Domain | Out-Null
            Write-Host "Đã mở firewall cổng $Port (Domain profile)."
        }
    }

    Start-Service $ServiceName
    Start-Sleep -Seconds 3
    Get-Service $ServiceName | Format-Table -AutoSize Status, Name, DisplayName | Out-String | Write-Host
    $fqdn = [System.Net.Dns]::GetHostEntry($env:COMPUTERNAME).HostName
    Write-Host "Truy cập: https://$($fqdn):$Port" -ForegroundColor Green
    if ($Upgrade) { Write-Host "Cập nhật hoàn tất lúc $(Get-Date -Format 'dd/MM/yyyy HH:mm:ss')." }
}
catch {
    Write-Host "LỖI: $($_.Exception.Message)" -ForegroundColor Red
    if ($Upgrade) {
        # Không để service nằm chết sau khi cập nhật hỏng
        Start-Service $ServiceName -ErrorAction SilentlyContinue
    }
    if ($LogFile) { Stop-Transcript | Out-Null }
    exit 1
}
if ($LogFile) { Stop-Transcript | Out-Null }
