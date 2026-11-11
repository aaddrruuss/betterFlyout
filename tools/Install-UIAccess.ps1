#Requires -RunAsAdministrator
<#
.SYNOPSIS
  Compila BetterFlyout con uiAccess=true, lo firma con un certificado local y lo instala en Program Files.

.DESCRIPTION
  Windows solo concede UIAccess (banda Z por encima de apps a pantalla completa, igual que el teclado en pantalla)
  si el ejecutable:
    1) declara uiAccess="true" en su manifiesto,
    2) está firmado con un certificado en el que confía la máquina,
    3) se ejecuta desde una ubicación segura (C:\Program Files, C:\Windows\System32...).

  Seguridad: se crea un certificado autofirmado NUEVO en cada instalación; tras firmar se BORRA su clave privada,
  de modo que nadie puede reutilizarlo para firmar otra cosa. Solo queda la parte pública en "Raíz de confianza"
  y "Editores de confianza". Los certificados de instalaciones anteriores se eliminan.
#>
param(
    [string]$InstallDir = (Join-Path $env:ProgramFiles 'BetterFlyout'),
    [switch]$NoLaunch   # no abrir la app al terminar
)
$ErrorActionPreference = 'Stop'
$root    = Split-Path $PSScriptRoot -Parent
$publish = Join-Path $env:TEMP 'BetterFlyout-publish-uiaccess'
$subject = 'CN=BetterFlyout Local UIAccess Signing'

Write-Host '==> Compilando (Release, UIAccess=true)...' -ForegroundColor Cyan
if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
dotnet publish (Join-Path $root 'BetterFlyout.csproj') -c Release -p:UIAccess=true -o $publish --nologo
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish falló' }

Write-Host '==> Cerrando instancias en ejecución...' -ForegroundColor Cyan
Get-Process BetterFlyout -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

Write-Host "==> Copiando a $InstallDir ..." -ForegroundColor Cyan
New-Item -ItemType Directory -Force $InstallDir | Out-Null
Copy-Item (Join-Path $publish '*') $InstallDir -Recurse -Force

Write-Host '==> Eliminando certificados antiguos de BetterFlyout...' -ForegroundColor Cyan
foreach ($store in 'My', 'Root', 'TrustedPublisher') {
    Get-ChildItem "Cert:\LocalMachine\$store" | Where-Object Subject -eq $subject | Remove-Item -Force -ErrorAction SilentlyContinue
}

Write-Host '==> Creando certificado de firma de código...' -ForegroundColor Cyan
$cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject $subject -CertStoreLocation 'Cert:\LocalMachine\My' `
        -KeyUsage DigitalSignature -KeyExportPolicy NonExportable -NotAfter (Get-Date).AddYears(20)

$pub = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2(,$cert.Export('Cert'))
foreach ($store in 'Root', 'TrustedPublisher') {
    $s = New-Object System.Security.Cryptography.X509Certificates.X509Store($store, 'LocalMachine')
    $s.Open('ReadWrite'); $s.Add($pub); $s.Close()
}

Write-Host '==> Firmando BetterFlyout.exe...' -ForegroundColor Cyan
$exe = Join-Path $InstallDir 'BetterFlyout.exe'
$sig = Set-AuthenticodeSignature -FilePath $exe -Certificate $cert -HashAlgorithm SHA256
if ($sig.Status -ne 'Valid') { throw "La firma falló: $($sig.StatusMessage)" }

Write-Host '==> Borrando la clave privada (ya no se necesita)...' -ForegroundColor Cyan
Remove-Item "Cert:\LocalMachine\My\$($cert.Thumbprint)" -DeleteKey -Force

# Si el usuario tenía activado "Iniciar con Windows" apuntando a otra ruta, actualízalo.
$run = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
if (Get-ItemProperty $run -Name BetterFlyout -ErrorAction SilentlyContinue) {
    Set-ItemProperty $run -Name BetterFlyout -Value "`"$exe`""
}

Remove-Item $publish -Recurse -Force -ErrorAction SilentlyContinue
# Restos de versiones anteriores del script (exe sin firmar que no arrancan)
Remove-Item (Join-Path $root 'publish-uiaccess') -Recurse -Force -ErrorAction SilentlyContinue

Write-Host '==> Creando acceso directo en el menu Inicio...' -ForegroundColor Cyan
$lnk = Join-Path $env:ProgramData 'Microsoft\Windows\Start Menu\Programs\BetterFlyout.lnk'
$sh = New-Object -ComObject WScript.Shell
$sc = $sh.CreateShortcut($lnk)
$sc.TargetPath = $exe
$sc.WorkingDirectory = $InstallDir
$sc.Description = 'BetterFlyout (UIAccess)'
$sc.Save()

Write-Host ''
Write-Host "Instalado con UIAccess en: $exe" -ForegroundColor Green
Write-Host 'IMPORTANTE: solo funciona ESE exe (firmado y en Program Files). Los de bin\ no arrancan con UIAccess.' -ForegroundColor Yellow

# Abrir BetterFlyout. Se lanza a través de explorer.exe para que arranque como usuario normal
# (este script corre como administrador y no queremos que la app herede esos permisos).
if (-not $NoLaunch) {
    Write-Host '==> Abriendo BetterFlyout...' -ForegroundColor Cyan
    Start-Process explorer.exe -ArgumentList "`"$exe`""
    Start-Sleep -Seconds 3
    if (Get-Process BetterFlyout -ErrorAction SilentlyContinue) {
        Write-Host 'BetterFlyout está en marcha (icono en la bandeja: "BetterFlyout (UIAccess)").' -ForegroundColor Green
    } else {
        Write-Host 'No se pudo comprobar que arrancara. Ábrelo desde el menú Inicio -> BetterFlyout.' -ForegroundColor Yellow
    }
}
