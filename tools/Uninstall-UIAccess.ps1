#Requires -RunAsAdministrator
param([string]$InstallDir = (Join-Path $env:ProgramFiles 'BetterFlyout'))
$ErrorActionPreference = 'Continue'
$subject = 'CN=BetterFlyout Local UIAccess Signing'

Get-Process BetterFlyout -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500
if (Test-Path $InstallDir) { Remove-Item $InstallDir -Recurse -Force }
foreach ($store in 'My', 'Root', 'TrustedPublisher') {
    Get-ChildItem "Cert:\LocalMachine\$store" | Where-Object Subject -eq $subject | Remove-Item -Force
}
Remove-Item (Join-Path $env:ProgramData 'Microsoft\Windows\Start Menu\Programs\BetterFlyout.lnk') -Force -ErrorAction SilentlyContinue
Remove-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name BetterFlyout -ErrorAction SilentlyContinue
Write-Host 'BetterFlyout (UIAccess) desinstalado. La configuración sigue en %APPDATA%\BetterFlyout.' -ForegroundColor Green
