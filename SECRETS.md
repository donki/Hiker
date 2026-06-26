# Gestion de secretos

Documento operativo para cumplir [constitucion seccion 5](constitucion.md).

## Situacion actual

El directorio del proyecto (`c:\ID\OneDrive\Hiker\Hiker`) esta dentro de OneDrive. Cualquier credencial que viva aqui se sincroniza a la nube aunque este gitignored. **Esto viola el principio de "tratar credenciales como material sensible incluso en maquinas locales"**.

## Ficheros sensibles que deben moverse fuera del proyecto

- `socratic.keystore`
- `keystore.password.txt` (renombrado a `keystore.password.local.txt`)
- `hiker-433118-98861f2881fa.json` (cuenta de servicio Google Play)

## Procedimiento de relocalizacion (manual)

1. Crear un directorio local fuera de OneDrive, por ejemplo `C:\secrets\hiker\` o `%USERPROFILE%\.secrets\hiker\`.
2. Mover los tres ficheros a ese directorio.
3. Configurar variables de entorno de usuario:
   ```pwsh
   [Environment]::SetEnvironmentVariable('ANDROID_KEYSTORE_PATH', 'C:\secrets\hiker\socratic.keystore', 'User')
   [Environment]::SetEnvironmentVariable('ANDROID_KEY_ALIAS', 'hiker', 'User')
   [Environment]::SetEnvironmentVariable('ANDROID_KEYSTORE_PASSWORD', '<contrasena>', 'User')
   [Environment]::SetEnvironmentVariable('GOOGLE_APPLICATION_CREDENTIALS', 'C:\secrets\hiker\hiker-433118-98861f2881fa.json', 'User')
   ```
4. Cerrar y reabrir PowerShell para que las variables apliquen.
5. Verificar que los scripts siguen funcionando:
   ```pwsh
   .\build_and_sign.ps1 -SkipApk -NoPause
   .\publish_aab_to_play.ps1 -ValidateOnly -AssumeYes
   ```
6. Si todo ok, en OneDrive forzar la eliminacion del backup en la nube (papelera de OneDrive web).

## Rotacion de credenciales

Si en algun momento se sospecha exposicion:

- **Keystore**: NO se puede rotar sin perder firma de la app en Play. Si se cree expuesto, contactar con soporte de Google Play para App Signing.
- **Cuenta de servicio Google**: revocar el JSON desde Google Cloud Console (IAM & Admin → Service Accounts → Keys) y emitir uno nuevo.
- **Contrasena del keystore**: cambiar via `keytool -storepasswd` y actualizar `ANDROID_KEYSTORE_PASSWORD`.

## Override puntual

Los scripts aceptan `-AllowInRepoSecrets` para escenarios de emergencia (ej. otra maquina). Documentar siempre el motivo y eliminar los ficheros del repo tras la operacion.
