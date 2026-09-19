# Changelog

## 2026.09.19.02 (2026091902)
- **Configuración simplificada**: fuera la configuración del GPS (GPS nativo, intervalo, precisión) y
  los botones Guardar y Restablecer. Solo queda el idioma, que se guarda solo al pulsarlo.

## 2026.09.19.01 (2026091901)
- **Ficha de ruta** desde el botón de información de la lista: distancia, desnivel positivo y
  negativo (suavizados para no sumar el ruido del GPS), altitud máxima, mínima y diferencia,
  duración, tiempo en movimiento, ritmo, velocidad media y máxima, número de puntos, fecha y el
  **perfil de desnivel** dibujado (altitud según la distancia). Desde ahí, «Ver en el mapa».

## 2026.09.19.00 (2026091900)
- **Corregido: el mapa se quedaba en Madrid aunque la cabecera ya enseñara la posición real.** El
  centrado inicial se lanzaba «a los dos segundos» de cargar el mapa, y con un WebView lento la
  función del mapa aún no existía: la llamada se perdía en silencio. Ahora se pregunta al WebView
  si el mapa está listo (hasta 15 s) y, si la posición llegó antes, se centra en cuanto lo está.
  Además, mover el mapa y pintar el punto de posición ya no esperan a que cargue el estilo (sin
  red o con red lenta se ve la posición igual).

## 2026.08.29.1 (202608291)
- **Corregido: la grabacion perdia puntos con la pantalla apagada o la app en segundo plano.** Eran
  tres fallos a la vez y se han arreglado los tres:
  - Quien escuchaba al GPS era la pagina del mapa, a traves de MAUI Essentials y con precision
    «media». Con la pantalla apagada la pagina deja de recibir y los proveedores que no son GPS los
    suspende el sistema en reposo. **Ahora escucha el propio servicio en primer plano**, pidiendo el
    proveedor GPS (con la red como apoyo), que es lo que Android sigue entregando mientras corre un
    servicio de tipo `location`.
  - Los puntos solo existian en memoria hasta que el usuario pulsaba «guardar»: si Android mataba el
    proceso en una ruta larga, se perdia todo. **Ahora cada punto se escribe en un diario en disco
    nada mas llegar**, y al volver a abrir la aplicacion se ofrece recuperar la grabacion
    interrumpida.
  - Con el servicio recreandose solo (Sticky), la grabacion no se reanudaba porque el estado vivia
    en la pagina. **La grabacion es ahora un servicio de aplicacion** (`TrackRecorder`) y el servicio
    la reanuda desde el diario al recrearse.
- Se coge un **bloqueo parcial de CPU** (`WAKE_LOCK`) mientras dura la grabacion: sin el, con la
  pantalla apagada el procesador se duerme entre posicion y posicion y la traza sale a rachas. Se
  suelta al parar de grabar.

## 2026.08.28.1 (202608281)
- **Grabacion de la ruta con seguimiento en vivo** (nota de autor del 2026-08-01). La logica ya
  estaba en el code-behind, pero se habia quedado sin ningun control en pantalla al pasar el mapa
  a pantalla completa: no habia forma de grabar. Ahora hay un boton de grabar en el mapa y, al
  pulsarlo, una barra con el tiempo, la distancia y los puntos que se llevan, con el trazado
  dibujandose sobre el mapa. Al parar se ofrece guardar la ruta como GPX o descartarla.
- **Servicio en primer plano de tipo `location`** mientras se graba: sin el, Android deja de
  entregar posiciones a los pocos minutos de apagar la pantalla y la ruta sale a trozos. Lleva
  notificacion permanente, que es el requisito de Android y ademas deja claro que se esta
  grabando. Se aniaden `FOREGROUND_SERVICE`, `FOREGROUND_SERVICE_LOCATION` y `POST_NOTIFICATIONS`
  al manifiesto; **no** hace falta `ACCESS_BACKGROUND_LOCATION`, que es de alta revision en Play.
- **API objetivo 36** (`net9.0-android36.0` + `TargetSdkVersion` explicito): requisito de Google
  Play para cualquier actualizacion desde el 31-ago-2026. Antes resolvia a 35.
- Correccion: los controles de grabacion llevan `ZIndex` explicito. Sin el, el WebView del mapa se
  quedaba con el toque y el boton no llegaba a pulsarse (comprobado en dispositivo).

## 2026.07.20.0 (202607200)
- Conformidad con la constitucion: tipografia del sistema (sin fuentes propias embebidas),
  colores/tamaños a tokens semanticos, botones de idioma con estilo, retirada la dependencia
  Microsoft.Maui.Controls.Compatibility.
- GPS: corregido que la geolocalizacion no arrancaba (servicios a Singleton); primer fix por
  red/fused para tablets wifi; sitúa correctamente.
- Mapa MapLibre GL + OpenFreeMap; menu hamburguesa con submenu de acciones desplegable; barra de
  estado en la cabecera y mapa a pantalla completa.
- Comprobacion de version al arrancar (constitucion seccion 15).

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
