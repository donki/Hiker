# Changelog

## 2026-06-26 - Cumplimiento de la constitucion (mejora tecnica, sin publicacion)
- Incorporada la constitucion canonica como submodulo Git en `constitution/` (repo donki/constitution).
- Completada `constitucion.md`: `Helpers/` y plataformas secundarias en la estructura; minimo privilegio con `maxSdkVersion`; gobernanza de logs; nueva seccion 14 (internacionalizacion) y 15 (documentos de gobernanza y submodulos).
- Minimo privilegio en AndroidManifest: `READ_EXTERNAL_STORAGE` acotado a `maxSdkVersion=32` y `WRITE_EXTERNAL_STORAGE` a `maxSdkVersion=28` (Android moderno no los solicita).
- README: documentado el submodulo de la constitucion y actualizada la nota de permisos de almacenamiento.
- Pendiente de validacion manual: export GPX en dispositivo Android API ≤ 28 (cambio de manifest).

## 2026-05-20 - Hiker 1.9.326 (1)
- Alineacion de scripts de build/publicacion con la constitucion del proyecto.
- Endurecimiento de manejo de secretos en scripts PowerShell.
- Validaciones de publicacion para pista internal y control de metadata/version.
