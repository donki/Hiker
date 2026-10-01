# Solicitud de acceso a producción en Google Play — Hiker

Respuestas para el cuestionario de Play Console › **Panel › Solicitar acceso a producción**, en
catalán (el idioma de la consola). Cada texto cabe en los 300 caracteres del formulario; el número
entre paréntesis es su longitud. Constitución Mobile §11. **Última actualización: 2026-10-01**
(versión 2026.09.30.00). Estado en Play: prueba cerrada (alpha 2026.09.12.2 publicada; la 2026.09.28.00 en borrador; en la pista desde el 2026-09-12).

> Lo marcado con ⚠ no lo puedo saber yo: compruébalo en la consola antes de enviarlo y cámbialo si
> no es así.
>
> - ⚠ La **declaración del servicio en primer plano (ubicación)** sigue pendiente en la consola y bloquea el commit de las versiones: sin ella no saldrá ni la 2026.09.28.00 ni producción.
> - ⚠ En alpha publicada solo está la 2026.09.12.2: envía a revisión el borrador 2026.09.28.00 (o súbeme la 2026.09.30.00, con el filtro del GPS y las 77 pruebas) antes de pedir producción.

---

## Informació sobre la prova tancada

**Com has reclutat usuaris per a la prova tancada?** (274) ⚠ *comprueba que las rutas de prueba se grabaron en el Xiaomi (no en el emulador).*

```
He afegit a la prova tancada quatre grups públics de Google de verificadors voluntaris (comunitats d'intercanvi de proves de 12 persones durant 14 dies). No he fet servir cap proveïdor de pagament. També l'he provada jo mateix gravant rutes amb un mòbil real amb Android 16.
```

**Fins a quin punt t'ha resultat fàcil reclutar verificadors?** — Propuesta: **Ni fàcil ni difícil** (los grupos públicos dan el número, pero participan poco).

**Descriu la implicació dels verificadors** (254) ⚠ *comprueba en Estadísticas / Prova tancada que de verdad la abrieron; si no hay datos, quita la parte de las funciones.*

```
Els verificadors han instal·lat l'app, han vist la seva posició al mapa i han gravat i desat alguna ruta curta. Un usuari real la faria servir en excursions llargues, amb la pantalla apagada hores i sense cobertura, cosa difícil de reproduir en la prova.
```

**Resum dels suggeriments i com els has recollit** (257) ⚠ *si algún verificador dejó comentarios (en la consola o por correo), menciónalos.*

```
Pocs comentaris escrits dels verificadors; els he recollit des de la consola de Play i GitHub. Les millores han sortit de les meves proves gravant rutes i del banc de proves: el filtre del GPS, el botó enrere, la traducció a l'anglès i l'atribució del mapa.
```

## Informació sobre l'aplicació

**A quin públic objectiu va dirigida?** (180)

```
Excursionistes, caminants i ciclistes que volen gravar i consultar les seves rutes GPS al mòbil, també sense cobertura, sense compte i sense enviar la seva ubicació a cap servidor.
```

**Com proporciona valor als usuaris?** (251)

```
Grava la ruta amb el GPS també amb la pantalla apagada (servei en primer pla amb notificació visible), la mostra en un mapa obert amb distància, desnivell i perfil, importa i exporta GPX i permet seguir-la. Les rutes es queden al mòbil; sense anuncis.
```

**Instal·lacions esperades el primer any** — Propuesta: **0 - 10.000** (app nueva, sin promoción).

## Preparació per a la producció

**Quins canvis has fet en funció de la prova tancada?** (238) ⚠ *el filtro del GPS y las 77 pruebas son de la 2026.09.30.00; la atribución y el atrás, de la 2026.09.28.00 (borrador).*

```
El mapa ja se centra a la posició real, hi ha fitxa de ruta amb desnivell i perfil, tota l'app en anglès, el botó enrere a Android 16 que mai atura la gravació, un gestor d'errors, el filtre del GPS que no suavitzava arreglat i 77 proves.
```

**Com has decidit que està preparada per a producció?** (282) ⚠ *comprueba en Qualitat › Android Vitals que no hay fallos; si los hay, quita «sense tancaments a la consola». La declaración del servicio en primer plano tiene que estar enviada; si no, quítala del texto.*

```
Les 77 proves automàtiques passen totes, l'he provada en un mòbil real amb Android 16 sense errors, els verificadors l'han fet servir 14 dies sense tancaments a la consola i la fitxa, la privadesa i la seguretat de les dades i la declaració del servei en primer pla estan completes.
```
