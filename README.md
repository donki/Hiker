# Hiker

Aplicacion Android en .NET MAUI para grabar y consultar rutas GPS. Optimizada para uso offline: los datos del usuario se mantienen en el dispositivo.

Cumple la [constitucion del proyecto](constitucion.md): privacidad primero, minimo privilegio, trazabilidad y reproducibilidad.

La constitucion canonica se versiona como submodulo Git en [constitution/](constitution/) (repo [donki/constitution](https://github.com/donki/constitution)). El fichero raiz [constitucion.md](constitucion.md) es la copia operativa que enlazan README y scripts; se mantiene sincronizada con la canonica. Tras clonar, inicializa el submodulo con `git submodule update --init`.

## Dónde conseguirla

- **Google Play:** https://play.google.com/store/apps/details?id=com.socratic.hiker
- **Releases de GitHub** (APK / EXE / MSIX de cada versión): https://github.com/donki/Hiker/releases

## Arquitectura

| Carpeta | Proposito |
|---|---|
| [Pages/](Pages/) | Paginas XAML/C# (UI MAUI nativa) |
| [Services/](Services/) | Logica de negocio e integraciones (geolocalizacion, rutas, traducciones, ajustes) |
| [Models/](Models/) | Clases de datos y entidades |
| [Helpers/](Helpers/) | Utilidades (GPX, formato, Kalman, ficheros) |
| [Platforms/Android/](Platforms/Android/) | Codigo Android nativo, manifest, recursos |
| [Platforms/Windows/](Platforms/Windows/) | Codigo Windows (objetivo secundario de desarrollo) |
| [Resources/](Resources/) | Iconos, imagenes, fuentes, traducciones, mapa HTML |

La logica de negocio reside exclusivamente en `Services/` y se consume via inyeccion de dependencias desde [MauiProgram.cs](MauiProgram.cs). Las paginas no contienen codigo de plataforma.

- ApplicationId: `com.socratic.hiker`
- TargetFramework principal: `net9.0-android` (API 24+)
- Tambien compila para `net9.0-windows10.0.19041.0` durante desarrollo

## Arranque local

```pwsh
dotnet restore
dotnet build -f net9.0-android
```

Para depurar en dispositivo o emulador Android conectado:

```pwsh
dotnet build -t:Run -f net9.0-android
```

## Build y firma

Script reproducible que genera APK (debug) y AAB (release) firmados:

```pwsh
.\build_and_sign.ps1
```

Por constitucion el keystore debe estar fuera del repositorio. Configurar via:

- `$env:ANDROID_KEYSTORE_PATH` — ruta absoluta al `.keystore`
- `$env:ANDROID_KEY_ALIAS` — alias de la clave (por defecto `hiker`)
- `$env:ANDROID_KEYSTORE_PASSWORD` — contrasena del keystore

Alternativamente, ubicar `keystore.password.local.txt` fuera del repo y pasar `-KeystorePath`.

## Publicacion en Google Play

```pwsh
.\publish_aab_to_play.ps1
```

El script aplica las validaciones de la constitucion:
- `ApplicationId` debe cumplir `com.socratic.[app]`.
- `ApplicationVersion` debe ser entero incremental.
- `CHANGELOG.md` debe contener la version actual.
- Por defecto publica en track `internal`. Para tracks superiores requiere `-AllowNonInternalTrack`.
- Valida longitudes y formato de metadata (titulo ≤ 30, corta ≤ 80, larga ≤ 4000).

Credenciales Google requeridas via `$env:GOOGLE_APPLICATION_CREDENTIALS` apuntando al JSON de cuenta de servicio (fuera del repo).

## Versionado

Sigue [constitucion seccion 6](constitucion.md):

- `ApplicationDisplayVersion`: cadena legible (`1.9.326`).
- `ApplicationVersion`: entero incremental formato `yyyyMMddR` (`202605210`).
- Ambos se actualizan en sincronia antes de publicar y se reflejan en [CHANGELOG.md](CHANGELOG.md).

## Permisos Android

Todos los permisos declarados en [Platforms/Android/AndroidManifest.xml](Platforms/Android/AndroidManifest.xml) cumplen el principio de minimo privilegio:

| Permiso | Justificacion |
|---|---|
| `INTERNET` | Carga de tiles de OpenStreetMap y descarga de elevacion |
| `ACCESS_NETWORK_STATE` | Detectar conectividad para usar cache de mapa cuando no hay red |
| `ACCESS_WIFI_STATE` | Mejorar precision de geolocalizacion cuando GPS no esta disponible |
| `ACCESS_COARSE_LOCATION` | Posicion aproximada para inicio rapido del tracker |
| `ACCESS_FINE_LOCATION` | Tracking GPS de alta precision (funcionalidad principal) |
| `ACCESS_BACKGROUND_LOCATION` | Mantener la grabacion de ruta cuando la pantalla se apaga durante una caminata |
| `REQUEST_IGNORE_BATTERY_OPTIMIZATIONS` | Evitar que el sistema mate el servicio de tracking en rutas largas |

Permisos de almacenamiento acotados por minimo privilegio:
- `READ_EXTERNAL_STORAGE` (`maxSdkVersion=32`) / `WRITE_EXTERNAL_STORAGE` (`maxSdkVersion=28`): la app usa `FileSaver` del CommunityToolkit, que desde Android 11 (API 30) emplea SAF y no los requiere. Se mantienen solo para Android antiguo (export GPX) y se retiran automaticamente en Android moderno. Validar export GPX en dispositivo API ≤ 28 antes de eliminarlos por completo.

## Secretos

No se comitean al repositorio (ver [.gitignore](.gitignore)):
- `*.keystore`, `*.jks`
- `keystore.password.txt`, `keystore.password.local.txt`
- `hiker-*.json`, `*-service-account*.json`
- `*.log`

Recomendacion adicional: ubicar el keystore y el JSON de cuenta de servicio **fuera del directorio del proyecto** (que esta sincronizado con OneDrive). Ver [SECRETS.md](SECRETS.md).

## Historial

Cambios relevantes en [CHANGELOG.md](CHANGELOG.md).
