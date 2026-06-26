# Script para registrar componentes COM/WinRT necesarios
# Ejecutar como administrador si es necesario

Write-Host "Registrando componentes COM/WinRT para Hiker..." -ForegroundColor Green

try {
    # Registrar Windows Runtime
    $winrtPath = "$env:SystemRoot\System32\Windows.ApplicationModel.dll"
    if (Test-Path $winrtPath) {
        Write-Host "Registrando Windows.ApplicationModel.dll..." -ForegroundColor Yellow
        regsvr32 /s $winrtPath
    }

    # Registrar componentes de geolocalización
    $geoPath = "$env:SystemRoot\System32\Windows.Devices.Geolocation.dll"
    if (Test-Path $geoPath) {
        Write-Host "Registrando Windows.Devices.Geolocation.dll..." -ForegroundColor Yellow
        regsvr32 /s $geoPath
    }

    # Limpiar cache de registro
    Write-Host "Limpiando cache de registro..." -ForegroundColor Yellow
    Get-AppxPackage -AllUsers | Where-Object {$_.Name -like "*Microsoft.WindowsAppRuntime*"} | ForEach-Object {
        try {
            Add-AppxPackage -Register "$($_.InstallLocation)\AppxManifest.xml" -DisableDevelopmentMode
        } catch {
            Write-Warning "No se pudo registrar: $($_.Name)"
        }
    }

    Write-Host "Registro COM completado exitosamente!" -ForegroundColor Green
    
} catch {
    Write-Error "Error durante el registro COM: $($_.Exception.Message)"
    Write-Host "Intenta ejecutar este script como administrador" -ForegroundColor Red
}

Write-Host "Presiona cualquier tecla para continuar..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")