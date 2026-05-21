[CmdletBinding()]
param(
    [string]$ApkPath,
    [string]$AdbPath,
    [string]$DeviceSerial,
    [string]$PackageName,
    [switch]$BuildFirst,
    [switch]$Launch,
    [switch]$GrantPermissions,
    [switch]$NoPause
)

$ErrorActionPreference = 'Stop'

$ProjectRoot = $PSScriptRoot
$ProjectPath = Join-Path $ProjectRoot 'SMSForwarder.csproj'
$DefaultAdbPaths = @(
    'C:\Program Files\Netease\MuMuPlayer\nx_main\adb.exe',
    'C:\Program Files\Netease\MuMuPlayer\nx_device\12.0\shell\adb.exe',
    'C:\Program Files\platform-tools\adb.exe',
    'C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe',
    (Join-Path $env:LOCALAPPDATA 'Android\Sdk\platform-tools\adb.exe')
)
$MumuPorts = @('127.0.0.1:16384', '127.0.0.1:7555', '127.0.0.1:16416', '127.0.0.1:5555')

function Pause-Script {
    if (-not $NoPause) {
        Read-Host 'Pulsa Enter para continuar' | Out-Null
    }
}

function Exit-WithError {
    param([Parameter(Mandatory = $true)][string]$Message)

    Write-Host "ERROR: $Message" -ForegroundColor Red
    Pause-Script
    exit 1
}

function Invoke-Adb {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)

    Write-Host "> $AdbPath $($Arguments -join ' ')"
    $output = & $AdbPath @Arguments 2>&1
    $exitCode = $LASTEXITCODE
    if ($output) {
        $output | ForEach-Object { Write-Host $_ }
    }

    return $exitCode
}

function Invoke-AdbChecked {
    param(
        [Parameter(Mandatory = $true)][string]$ErrorMessage,
        [Parameter(Mandatory = $true)][string[]]$Arguments
    )

    $exitCode = Invoke-Adb $Arguments
    if ($exitCode -ne 0) {
        Exit-WithError $ErrorMessage
    }
}

function Get-ProjectValue {
    param(
        [Parameter(Mandatory = $true)][xml]$ProjectXml,
        [Parameter(Mandatory = $true)][string]$Name
    )

    $nodes = $ProjectXml.SelectNodes("//*[local-name()='$Name']")
    foreach ($node in $nodes) {
        if (-not [string]::IsNullOrWhiteSpace($node.InnerText)) {
            return $node.InnerText.Trim()
        }
    }

    return $null
}

function Resolve-AdbPath {
    if (-not [string]::IsNullOrWhiteSpace($AdbPath)) {
        if (Test-Path -LiteralPath $AdbPath) {
            return (Resolve-Path -LiteralPath $AdbPath).Path
        }

        Exit-WithError "No existe adb.exe en: $AdbPath"
    }

    foreach ($candidate in $DefaultAdbPaths) {
        if (-not [string]::IsNullOrWhiteSpace($candidate) -and (Test-Path -LiteralPath $candidate)) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    $command = Get-Command adb -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    Exit-WithError 'No se encontro adb.exe. Pasa la ruta con -AdbPath.'
}

function Get-AdbDevices {
    $raw = & $AdbPath devices
    if ($LASTEXITCODE -ne 0) {
        Exit-WithError 'No se pudo listar dispositivos ADB.'
    }

    return $raw |
        Where-Object { $_ -match '^\S+\s+device$' } |
        ForEach-Object { ($_ -split '\s+')[0] }
}

function Resolve-DeviceSerial {
    if (-not [string]::IsNullOrWhiteSpace($DeviceSerial)) {
        return $DeviceSerial
    }

    $devices = @(Get-AdbDevices)
    foreach ($port in $MumuPorts) {
        if ($devices -contains $port) {
            return $port
        }
    }

    foreach ($port in $MumuPorts) {
        Write-Host "Intentando conectar a MuMu en $port..."
        & $AdbPath connect $port | Out-Host
    }

    $devices = @(Get-AdbDevices)
    foreach ($port in $MumuPorts) {
        if ($devices -contains $port) {
            return $port
        }
    }

    if ($devices.Count -eq 1) {
        return $devices[0]
    }

    if ($devices.Count -gt 1) {
        Write-Host 'Dispositivos detectados:'
        $devices | ForEach-Object { Write-Host "  $_" }
        Exit-WithError 'Hay varios dispositivos ADB. Usa -DeviceSerial para elegir MuMu.'
    }

    Exit-WithError 'No se detecto MuMu por ADB. Abre MuMu Player y vuelve a ejecutar el script.'
}

function Get-LatestSignedApk {
    $items = Get-ChildItem -LiteralPath (Join-Path $ProjectRoot 'bin') -Filter '*-Signed.apk' -Recurse -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending

    return $items | Select-Object -First 1
}

function Build-Apk {
    $buildScript = Join-Path $ProjectRoot 'build_and_sign.ps1'
    if (Test-Path -LiteralPath $buildScript) {
        & $buildScript -NoPause
        if ($LASTEXITCODE -ne 0) {
            Exit-WithError 'Fallo el build del APK.'
        }
        return
    }

    Exit-WithError 'No existe build_and_sign.ps1 para compilar antes de instalar.'
}

if (-not (Test-Path -LiteralPath $ProjectPath)) {
    Exit-WithError "No existe el proyecto: $ProjectPath"
}

$projectXml = [xml](Get-Content -LiteralPath $ProjectPath -Raw)
if ([string]::IsNullOrWhiteSpace($PackageName)) {
    $PackageName = Get-ProjectValue $projectXml 'ApplicationId'
}
if ([string]::IsNullOrWhiteSpace($PackageName)) {
    Exit-WithError 'No se pudo resolver ApplicationId desde el csproj.'
}

$AdbPath = Resolve-AdbPath

Write-Host '========================================'
Write-Host '   SMS Forwarder - Instalar en MuMu'
Write-Host '========================================'
Write-Host
Write-Host "ADB: $AdbPath"
Write-Host "Package: $PackageName"

if ($BuildFirst) {
    Write-Host
    Write-Host 'Compilando APK antes de instalar...'
    Build-Apk
}

if ([string]::IsNullOrWhiteSpace($ApkPath)) {
    $latestApk = Get-LatestSignedApk
    if (-not $latestApk) {
        Exit-WithError 'No se encontro ningun *-Signed.apk. Ejecuta con -BuildFirst o compila primero.'
    }
    $ApkPath = $latestApk.FullName
}
else {
    $ApkPath = if ([System.IO.Path]::IsPathRooted($ApkPath)) { $ApkPath } else { Join-Path (Get-Location) $ApkPath }
}

if (-not (Test-Path -LiteralPath $ApkPath)) {
    Exit-WithError "No existe el APK: $ApkPath"
}
$ApkPath = (Resolve-Path -LiteralPath $ApkPath).Path
$apkItem = Get-Item -LiteralPath $ApkPath

$DeviceSerial = Resolve-DeviceSerial
Write-Host "Dispositivo MuMu: $DeviceSerial"
Write-Host "APK: $ApkPath"
Write-Host "Tamano: $($apkItem.Length) bytes"
Write-Host

Invoke-AdbChecked 'Fallo la instalacion del APK en MuMu.' @(
    '-s', $DeviceSerial,
    'install', '-r', '-d',
    $ApkPath
)

if ($GrantPermissions) {
    Write-Host
    Write-Host 'Concediendo permisos runtime utiles para pruebas...'
    $permissions = @(
        'android.permission.RECEIVE_SMS',
        'android.permission.SEND_SMS',
        'android.permission.READ_CONTACTS'
    )
    foreach ($permission in $permissions) {
        & $AdbPath -s $DeviceSerial shell pm grant $PackageName $permission | Out-Host
    }
}

if ($Launch) {
    Write-Host
    Write-Host 'Lanzando app...'
    Invoke-AdbChecked 'La app se instalo, pero no se pudo lanzar.' @(
        '-s', $DeviceSerial,
        'shell', 'monkey',
        '-p', $PackageName,
        '-c', 'android.intent.category.LAUNCHER',
        '1'
    )
}

Write-Host
Write-Host 'Instalacion completada en MuMu.'
Pause-Script
