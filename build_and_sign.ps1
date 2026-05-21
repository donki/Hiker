[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$TargetFramework,
    [string]$DotnetPath,
    [string]$KeystorePath,
    [string]$KeyAlias,
    [string]$StorePass,
    [string]$KeyPass,
    [string]$IntermediateOutputRoot,
    [switch]$AllowInRepoSecrets,
    [switch]$Clean,
    [switch]$SkipApk,
    [switch]$SkipClean,
    [switch]$NoPause
)

$ErrorActionPreference = 'Stop'

$ProjectRoot = $PSScriptRoot
$ProjectPath = Join-Path $ProjectRoot 'Hiker.csproj'

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

function Invoke-NativeCommand {
    param(
        [Parameter(Mandatory = $true)][string]$ErrorMessage,
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string[]]$Arguments
    )

    Write-Host "> $FilePath $($Arguments -join ' ')"
    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
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

function Resolve-ToolPath {
    param(
        [string]$ConfiguredPath,
        [Parameter(Mandatory = $true)][string]$CommandName,
        [string]$FallbackPath
    )

    if (-not [string]::IsNullOrWhiteSpace($ConfiguredPath)) {
        if (Test-Path -LiteralPath $ConfiguredPath) {
            return (Resolve-Path -LiteralPath $ConfiguredPath).Path
        }

        Exit-WithError "No existe $CommandName en: $ConfiguredPath"
    }

    $command = Get-Command $CommandName -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    if (-not [string]::IsNullOrWhiteSpace($FallbackPath) -and (Test-Path -LiteralPath $FallbackPath)) {
        return $FallbackPath
    }

    Exit-WithError "No se encontro $CommandName. Pasalo con -DotnetPath."
}

function Get-LatestPackage {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$Filter,
        [switch]$PreferPublishFolder
    )

    $items = Get-ChildItem -LiteralPath $Root -Filter $Filter -Recurse -ErrorAction SilentlyContinue |
        Sort-Object @{ Expression = { if ($PreferPublishFolder -and $_.DirectoryName -like '*\publish') { 0 } else { 1 } } }, LastWriteTime -Descending

    return $items | Select-Object -First 1
}

function Test-IsPathUnderRoot {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Root
    )

    $resolvedPath = (Resolve-Path -LiteralPath $Path).Path
    $resolvedRoot = (Resolve-Path -LiteralPath $Root).Path
    return $resolvedPath.StartsWith($resolvedRoot, [StringComparison]::OrdinalIgnoreCase)
}

function Test-ConstitutionRules {
    param(
        [Parameter(Mandatory = $true)][xml]$ProjectXml,
        [Parameter(Mandatory = $true)][string]$ProjectPathToCheck,
        [Parameter(Mandatory = $true)][string]$PackageNameToCheck,
        [Parameter(Mandatory = $true)][string]$KeystorePathToCheck,
        [switch]$AllowInRepoSecretsCheck
    )

    if ($PackageNameToCheck -notmatch '^com\.socratic\.[a-z0-9.]+$') {
        Exit-WithError "ApplicationId invalido en $ProjectPathToCheck. Debe cumplir com.socratic.[app]."
    }

    $displayVersion = Get-ProjectValue $ProjectXml 'ApplicationDisplayVersion'
    $appVersion = Get-ProjectValue $ProjectXml 'ApplicationVersion'
    if ([string]::IsNullOrWhiteSpace($displayVersion) -or [string]::IsNullOrWhiteSpace($appVersion)) {
        Exit-WithError 'ApplicationDisplayVersion y ApplicationVersion son obligatorios antes de publicar.'
    }

    $versionInt = 0
    if (-not [int]::TryParse($appVersion, [ref]$versionInt) -or $versionInt -le 0) {
        Exit-WithError "ApplicationVersion debe ser entero incremental > 0. Actual: $appVersion"
    }

    if (-not $AllowInRepoSecretsCheck -and (Test-IsPathUnderRoot -Path $KeystorePathToCheck -Root $ProjectRoot)) {
        Exit-WithError 'El keystore esta dentro del repositorio. Muevelo fuera del repo o usa -AllowInRepoSecrets.'
    }
}

if (-not (Test-Path -LiteralPath $ProjectPath)) {
    Exit-WithError "No existe el proyecto: $ProjectPath"
}

$projectXml = [xml](Get-Content -LiteralPath $ProjectPath -Raw)
$packageName = Get-ProjectValue $projectXml 'ApplicationId'
if ([string]::IsNullOrWhiteSpace($packageName)) {
    Exit-WithError 'No se encontro ApplicationId en el csproj.'
}

if ([string]::IsNullOrWhiteSpace($TargetFramework)) {
    $targetFrameworks = Get-ProjectValue $projectXml 'TargetFrameworks'
    $targetFramework = Get-ProjectValue $projectXml 'TargetFramework'
    $candidates = @()
    if (-not [string]::IsNullOrWhiteSpace($targetFrameworks)) {
        $candidates += $targetFrameworks.Split(';') | ForEach-Object { $_.Trim() }
    }
    if (-not [string]::IsNullOrWhiteSpace($targetFramework)) {
        $candidates += $targetFramework.Trim()
    }

    $TargetFramework = $candidates | Where-Object { $_ -like '*-android*' } | Select-Object -First 1
}

if ([string]::IsNullOrWhiteSpace($TargetFramework)) {
    Exit-WithError 'No se encontro un TargetFramework Android en el csproj.'
}

$DotnetPath = Resolve-ToolPath $DotnetPath 'dotnet' 'C:\Program Files\dotnet\dotnet.exe'

if ([string]::IsNullOrWhiteSpace($KeystorePath)) {
    $KeystorePath = if (-not [string]::IsNullOrWhiteSpace($env:ANDROID_KEYSTORE_PATH)) {
        $env:ANDROID_KEYSTORE_PATH
    }
    else {
        Join-Path $ProjectRoot 'socratic.keystore'
    }
}
$KeystorePath = if ([System.IO.Path]::IsPathRooted($KeystorePath)) { $KeystorePath } else { Join-Path $ProjectRoot $KeystorePath }
if (-not (Test-Path -LiteralPath $KeystorePath)) {
    Exit-WithError "No existe el keystore: $KeystorePath"
}
$KeystorePath = (Resolve-Path -LiteralPath $KeystorePath).Path

if ([string]::IsNullOrWhiteSpace($KeyAlias)) {
    if (-not [string]::IsNullOrWhiteSpace($env:ANDROID_KEY_ALIAS)) {
        $KeyAlias = $env:ANDROID_KEY_ALIAS
    }
    else {
        $KeyAlias = 'hiker'
    }
}

if ([string]::IsNullOrWhiteSpace($StorePass)) {
    $StorePass = if (-not [string]::IsNullOrWhiteSpace($env:ANDROID_KEYSTORE_PASSWORD)) {
        $env:ANDROID_KEYSTORE_PASSWORD
    }
    elseif (Test-Path -LiteralPath (Join-Path $ProjectRoot 'keystore.password.local.txt')) {
        (Get-Content -LiteralPath (Join-Path $ProjectRoot 'keystore.password.local.txt') -Raw).Trim()
    }
    else {
        Exit-WithError 'No se encontro ANDROID_KEYSTORE_PASSWORD ni keystore.password.local.txt'
    }
}

if ([string]::IsNullOrWhiteSpace($KeyPass)) {
    $KeyPass = if (-not [string]::IsNullOrWhiteSpace($env:ANDROID_KEY_PASSWORD)) {
        $env:ANDROID_KEY_PASSWORD
    }
    else {
        $StorePass
    }
}

Test-ConstitutionRules -ProjectXml $projectXml -ProjectPathToCheck $ProjectPath -PackageNameToCheck $packageName -KeystorePathToCheck $KeystorePath -AllowInRepoSecretsCheck:$AllowInRepoSecrets

$releaseOutputPath = Join-Path $ProjectRoot "bin\$Configuration\$TargetFramework"
$debugOutputPath = Join-Path $ProjectRoot "bin\Debug\$TargetFramework"
if ([string]::IsNullOrWhiteSpace($IntermediateOutputRoot)) {
    $IntermediateOutputRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'Hiker-msbuild-obj'
}
$IntermediateOutputRoot = if ([System.IO.Path]::IsPathRooted($IntermediateOutputRoot)) { $IntermediateOutputRoot } else { Join-Path $ProjectRoot $IntermediateOutputRoot }
New-Item -ItemType Directory -Path $IntermediateOutputRoot -Force | Out-Null
$IntermediateOutputRoot = (Resolve-Path -LiteralPath $IntermediateOutputRoot).Path
if (-not $IntermediateOutputRoot.EndsWith([System.IO.Path]::DirectorySeparatorChar) -and -not $IntermediateOutputRoot.EndsWith([System.IO.Path]::AltDirectorySeparatorChar)) {
    $IntermediateOutputRoot = "$IntermediateOutputRoot$([System.IO.Path]::DirectorySeparatorChar)"
}

$intermediateArgs = @(
    "-p:BaseIntermediateOutputPath=$IntermediateOutputRoot",
    "-p:MSBuildProjectExtensionsPath=$IntermediateOutputRoot",
    '-p:DefaultItemExcludes="$(DefaultItemExcludes);bin\**;obj\**"'
)
$signingArgs = @(
    '-p:AndroidKeyStore=true',
    "-p:AndroidSigningKeyStore=$KeystorePath",
    "-p:AndroidSigningKeyAlias=$KeyAlias",
    "-p:AndroidSigningKeyPass=$KeyPass",
    "-p:AndroidSigningStorePass=$StorePass"
)

Write-Host '========================================'
Write-Host '        Hiker - Build and Sign Script'
Write-Host '========================================'
Write-Host
Write-Host "Proyecto: $ProjectPath"
Write-Host "TargetFramework: $TargetFramework"
Write-Host "Package: $packageName"
Write-Host "Keystore: $KeystorePath"
Write-Host "Obj temporal: $IntermediateOutputRoot"
Write-Host

Push-Location $ProjectRoot
try {
    if ($Clean -and -not $SkipClean) {
        Write-Host '[1/4] Limpiando proyecto...'
        Invoke-NativeCommand 'Fallo al limpiar el proyecto' $DotnetPath (@('clean', $ProjectPath, '-c', $Configuration, '-f', $TargetFramework) + $intermediateArgs)
    }
    else {
        Write-Host '[1/4] Limpieza omitida. Usa -Clean si quieres forzar dotnet clean.'
    }

    if (-not $SkipApk) {
        Write-Host
        Write-Host '[2/4] Compilando APK firmado para testing...'
        Invoke-NativeCommand 'Fallo al compilar APK' $DotnetPath (@(
            'publish', $ProjectPath,
            '-f', $TargetFramework,
            '-c', 'Debug',
            '-p:AndroidPackageFormat=apk'
        ) + $intermediateArgs + $signingArgs)
    }
    else {
        Write-Host
        Write-Host '[2/4] APK omitido.'
    }

    Write-Host
    Write-Host '[3/4] Compilando AAB firmado para Google Play...'
    Invoke-NativeCommand 'Fallo al compilar AAB' $DotnetPath (@(
        'publish', $ProjectPath,
        '-f', $TargetFramework,
        '-c', $Configuration,
        '-p:AndroidPackageFormat=aab'
    ) + $intermediateArgs + $signingArgs)

    Write-Host
    Write-Host '[4/4] Verificando archivos generados...'

    $signedApk = $null
    if (-not $SkipApk -and (Test-Path -LiteralPath $debugOutputPath)) {
        $signedApk = Get-LatestPackage $debugOutputPath '*-Signed.apk' -PreferPublishFolder
    }

    $signedAab = $null
    if (Test-Path -LiteralPath $releaseOutputPath) {
        $signedAab = Get-LatestPackage $releaseOutputPath '*-Signed.aab' -PreferPublishFolder
    }

    if ($signedApk) {
        Write-Host "OK APK firmado: $($signedApk.FullName)"
        Write-Host "  Tamano: $($signedApk.Length) bytes"
    }
    elseif (-not $SkipApk) {
        Write-Host 'WARNING: APK firmado no encontrado.'
    }

    if ($signedAab) {
        Write-Host "OK AAB firmado: $($signedAab.FullName)"
        Write-Host "  Tamano: $($signedAab.Length) bytes"
    }
    else {
        Exit-WithError 'AAB firmado no encontrado.'
    }

    Write-Host
    Write-Host '========================================'
    Write-Host '          COMPILACION COMPLETA'
    Write-Host '========================================'
    if ($signedApk) {
        Write-Host "APK para testing: $($signedApk.FullName)"
    }
    Write-Host "AAB para Google Play: $($signedAab.FullName)"
    Write-Host
    Write-Host 'Para publicar:'
    Write-Host ".\publish_aab_to_play.ps1 -AabPath `"$($signedAab.FullName)`""
    Pause-Script
}
finally {
    Pop-Location
}
