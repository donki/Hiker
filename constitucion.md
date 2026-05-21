# Constitucion de Proyectos Android en .NET MAUI

## 1. Proposito
Documento de referencia para la arquitectura, gobernanza, publicación y mantenimiento de aplicaciones Android desarrolladas en .NET MAUI. Define estándares operacionales aplicables a cualquier proyecto con este stack.

## 2. Alcance
- Plataforma objetivo principal: Android.
- Ciclo de desarrollo: desde arquitectura hasta publicación en Google Play.
- Áreas cubiertas: estructura del proyecto, seguridad, versionado, publicación, iconografía, idiomas, calidad y colaboración.
- Fuera del alcance: decisiones de producto específicas, requisitos funcionales particulares, integraciones de terceros.

## 3. Principios no negociables
- Privacidad primero: datos de usuario se mantienen en el dispositivo.
- Minimo privilegio: solo permisos estrictamente necesarios.
- Confiabilidad operativa: prevenir fallos silenciosos y errores no documentados.
- Trazabilidad: toda publicacion debe ser reproducible y verificable.
- Cambios pequenos y seguros: evitar refactorizaciones masivas sin necesidad.
- Reproducibilidad: cualquier version debe poder regenerarse desde el codigo fuente.

## 4. Estructura del Proyecto
Carpetas y proposito estandar:
- Pages/: interfaces de usuario en XAML/C#.
- Services/: logica de negocio e integraciones.
- Models/: clases de datos y entidades.
- Platforms/Android/: codigo Android nativo, manifiestos, recursos especificos.
- Resources/: iconos, imagenes, estilos, fuentes.
- Properties/: configuracion del proyecto (launchSettings.json).

Reglas de dependencia obligatorias:
- La logica de negocio reside en Services, no en interfaz.
- Codigo Android especifico (permisos, integraciones nativas) debe estar encapsulado en Platforms/Android.
- La interfaz MAUI no debe contener logica de plataforma o referencias directas a APIs Android.
- Inyeccion de dependencias para todos los servicios.

Convención de identificador de paquete (Package Name):
- Formato obligatorio: `com.socratic.[nombre-aplicacion]` (en minusculas, sin espacios).
- Ejemplos: `com.socratic.smsforwarder`, `com.socratic.notasapp`, `com.socratic.calculadora`.
- Configurar en CSPROJ: ApplicationId = "com.socratic.[nombre-aplicacion]".
- Este identificador es permanente y publicamente visible en Google Play; no puede cambiar una vez publicado.

## 5. Seguridad, Secretos y Cumplimiento
- Politica de secretos:
  - Nunca subir archivos con credenciales (json de servicio, contrasenas, almacenes de claves) a repositorio.
  - Usar variables de entorno o archivos locales gitignored para credenciales.
  - Tratar credenciales como material sensible incluso en máquinas locales.
- Permisos Android:
  - Revisar AndroidManifest.xml antes de cada publicacion.
  - Justificar cada permiso solicitado en documentacion.
  - Aplicar principio de minimo privilegio.
- Metadata de Play:
  - Mantener descripciones, titulos y notas de lanzamiento precisos y veridicos.
  - Evitar promesas no cumplidas o contenido enganoso.
  - Actualizar descripciones en todos los idiomas soportados antes de publicar.

## 6. Versionado y Publicacion
Esquema de versionado recomendado:
- ApplicationDisplayVersion: cadena legible para usuario (ejemplo: 2026.05.20.0 o 1.10.0).
- ApplicationVersion: entero incremental requerido por Play Store (ejemplo: 202605200).
- Ambos valores deben actualizarse en sincronía antes de cada publicacion.
- Registrar todos los cambios de version en CHANGELOG.

Artefactos de compilacion y publicacion:
- Proyecto CSPROJ: configura version, metadatos, dependencias.
- Script de compilacion: automatiza generacion de APK y AAB firmados (debe ser reproducible).
- Script de publicacion: subida a Google Play automatizando AAB, ficha, icono, canal de distribucion.

## 7. Flujo de Publicacion Estandar
1. Actualizar ApplicationDisplayVersion y ApplicationVersion en el archivo CSPROJ.
2. Actualizar CHANGELOG con los cambios de esta version.
3. Ejecutar script de compilacion y firmado (debe producir APK y AAB).
4. Validar localmente el artefacto AAB firmado.
5. Ejecutar script de publicacion con parametros de Play Console (cuenta de servicio, pista, metadata).
6. Confirmar en Play Console:
   - versionCode es incremental y correcto
   - pista/canal es la deseada
   - icono de aplicacion se ve correctamente
   - idioma por defecto es valido
   - descripcion y notas de lanzamiento estan en idiomas soportados
7. Registrar la publicacion en repositorio (tag de version, commit de merge).

## 8. Politica de Google Play Store
Canales de distribucion:
- Pruebas internas: primer canal para validacion, sin revisor de Play.
- Pruebas cerradas: para grupos limitados de testers.
- Produccion: distribucion publica a todos los usuarios.

Reglas obligatorias:
- Publicar siempre primero en pruebas internas para validacion.
- No pasar a produccion sin cumplir lista de verificacion completa de metadata y permisos.
- Mantener metadatos (titulo, descripcion corta, descripcion larga, notas de lanzamiento, capturas) consistentes en todos los idiomas.
- Fijar idioma por defecto antes de eliminar cualquier traduccion.
- El icono de aplicacion debe ser visible, legible y cumplir guias de Google Play.
- No mezclar cambios funcionales con cambios de metadata en misma publicacion.

## 9. Estandares de Calidad
- Compilacion: debe finalizar sin errores. Las advertencias deben documentarse y reducirse progresivamente.
- Pruebas: incluir pruebas unitarias e integracion cuando sea viable.
- Permisos: revisar AndroidManifest.xml y justificar cada permiso.
- Validacion manual: cada cambio funcional debe verificarse en dispositivo Android real o emulador antes de publicar.
- Documentacion: mantener README y CHANGELOG actualizados con cambios relevantes.

## 10. Convenciones de Colaboracion en Repositorio
- Confirmaciones (commits) atomicas: un cambio logico por commit.
- Mensajes de commit descriptivos: explicar el "que" y el "por que", no solo el "que".
- No mezclar cambios funcionales con cambios cosmeticos o refactorizacion.
- Ramas de trabajo: crear rama descriptiva para cada feature o fix.
- Pull requests: incluir descripcion del cambio, impacto y verificacion realizada.
- Revisor: al menos un revisor antes de merge a rama principal.
- Documentacion: actualizar README y CHANGELOG si aplica.
- Publicacion: si el cambio afecta publicacion o scripts de build, registrar procedimiento.

## 11. Plan de Contingencia
Errores comunes y procedimientos de recuperacion:
- Compilacion bloqueada por permisos: cerrar procesos de compilacion, limpiar bin/obj, reintentar.
- versionCode rechazado por Play: regenerar AAB con entero incremental valido que no haya sido usado antes.
- Play bloquea publicacion por idioma por defecto: cambiar idioma por defecto a uno soportado via API, reintentar.
- Problemas de firma: validar que el keystore sea accesible, contrasena correcta, alias correcto.
- Fallo de autenticacion Play: verificar cuenta de servicio JSON, permisos en Play Console, token expirado.

## 12. Criterio de Entrega (Definition of Done)
Una version se considera lista para produccion cuando:
- Compila sin errores y genera AAB/APK firmado correctamente.
- Subida a Play Console sin fallos.
- Icono de aplicacion visible y legible en Play Store.
- Metadata (titulo, descripcion, notas) esta en todos los idiomas soportados.
- Permisos solicitados estan justificados y documentados.
- Cambios principales estan registrados en CHANGELOG.
- Version (versionCode y versionName) esta incrementada y registrada.
- Se ha realizado validacion manual en dispositivo Android real o emulador.

## 13. Revision Continua y Mejora Incremental
Politica de revision y optimizacion:
- Revisar periodicamente el codigo, arquitectura y dependencias para identificar areas de mejora.
- Aplicar mejoras pequeñas e incrementales: refactorizacion, actualizacion de dependencias, optimizacion de rendimiento, claridad de codigo.
- **Principio fundamental: nunca sacrificar estabilidad por mejora.** Todo cambio debe dejar la aplicacion en estado funcional y verificado.
- Cambios de mejora deben:
  - Aislarse en rama separada y commit independiente.
  - No alterar la funcionalidad existente o la experiencia del usuario.
  - Incluir validacion manual en dispositivo antes de merge.
  - Documentarse en CHANGELOG como "mejora tecnica" o "refactorizacion".
- Si una mejora requiere cambios mayores, planificar como feature independiente en version futura.
