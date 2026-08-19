# Cumplimiento HIPAA para transporte y logística — Advance Logistics

Recreación **nativa** (texto + HTML, sin imágenes escaneadas) del adiestramiento de HIPAA
preparado por Marie Carmen Muntaner, Esq. — MM & Associates, LLC, adaptado para el personal
de Advance Logistics.

- **7 módulos**, cada uno con sus pantallas de contenido y sus preguntas al final.
- **52 pantallas** de contenido (`Info`) y **30 preguntas** (25 de selección única, 3 de
  selección múltiple, 2 de pareo) — 300 puntos, se aprueba con 70% (210 puntos).
- Recurrencia **anual** (`recurrenceMonths: 12`, reabre 30 días antes de vencer) y plantilla
  de certificado configurada para Advance Logistics.

## Archivos

| Archivo | Qué es |
|---|---|
| `authoring.mjs` | Helpers y estilos. Construyen los payloads que espera la plataforma. |
| `img/` | Las 14 ilustraciones recortadas del PDF original (logos, diagrama de la cadena de confianza, afiches, fotos). |
| `m0-apertura.mjs` … `m7-panorama-y-cierre.mjs` | El contenido, un archivo por módulo. **Aquí se edita el curso.** |
| `course.mjs` | Metadatos del curso (título, categoría, recurrencia, certificado) y ensamblaje de los módulos. |
| `seed.mjs` | Valida, genera vista previa/JSON y siembra el curso en la plataforma por API. |
| `preview.html` | Generado. Todo el curso en una página, con las respuestas correctas marcadas — para revisión. |
| `course.json` | Generado. El curso completo en JSON, por si se quiere importar de otra forma. |

## Uso

Revisar el contenido antes de subir nada (no necesita servidor ni credenciales):

```bash
node content/hipaa-advance-logistics/seed.mjs --preview
```

Sembrarlo en la base del tenant de Advance Logistics (el usuario debe tener rol `Admin`,
`Author` o `Moderator` en ese tenant; los ítems se crean en la base que resuelve su token):

```bash
node content/hipaa-advance-logistics/seed.mjs --url http://localhost:52045 --email autor@advancelogistics.com --password "***"
```

Queda en **borrador** para que lo revises en la app. Para publicar en la misma corrida añade
`--publish`, o publícalo desde la interfaz de autoría.

El script valida antes de tocar la API: que cada pregunta tenga su respuesta correcta entre las
opciones y que cada par apunte a una opción válida. Si algo no cuadra, aborta sin crear nada.

## Mapa del contenido original

| Módulo | Diapositivas del PDF |
|---|---|
| Apertura (portada, aviso, introducción) | 1–3 |
| 1 · Qué es HIPAA y a quién aplica | 4–10 |
| 2 · Por qué nos aplica en transporte y logística | 11–14 |
| 3 · La PHI y la Regla de Privacidad | 15–19, 24 |
| 4 · La Regla de Seguridad y las salvaguardas | 20–23, 33–36 |
| 5 · Incumplimiento, notificación y consecuencias | 22, 25–29, 54 |
| 6 · Prácticas seguras en el día a día | 30–32, 37–48, 52–53, 56–57 |
| 7 · El panorama de las brechas y tu compromiso | 30, 38, 49–51, 55–58 |

Las tres gráficas del deck (incidentes de hacking 2009–2022, clasificación de brechas 2022 y
dónde ocurrieron) se recrearon con barras en HTML, así que se leen en celular y se actualizan
editando números, no reemplazando una imagen.

## Las imágenes

El texto es nativo, pero las **ilustraciones sí son las del original**: se recortaron de las
diapositivas escaneadas con `tools/extraer-figuras.mjs` (detecta la mancha de color de cada
figura, o usa un recorte manual en fracciones cuando la detección falla) y viven en `img/`.

Se incrustan en el HTML como **data URL** en vez de subirse por `/media`. Así `course.json` es
autosuficiente: al servidor solo hay que llevarle ese archivo y el script, sin subir medios
aparte ni depender de rutas protegidas. Eso lleva el JSON a ~750 KB, con el ítem más pesado en
~112 KB — cómodo para `PayloadJson`.

Si algún día se quiere reemplazar una figura, se sustituye el `.jpg` en `img/` y se regenera el
JSON; el resto del curso no cambia.

## Fidelidad al documento original

El contenido sustantivo se mantiene **tal como lo dice el PDF** — la plataforma no tiene derechos
para editar el material del autor. Esto incluye:

- Los montos de las penalidades tal cual aparecen ($50,000–$1.5 millones criminales, $100–$25,000
  civiles, hasta 10 años de cárcel), aunque esas cifras estén desactualizadas frente a los tramos
  ajustados por inflación que aplica la OCR hoy. Si en algún momento el autor emite una versión
  corregida, se actualiza `m5-incumplimiento-y-notificacion.mjs`.
- Las estadísticas del HIPAA Journal de 2021–2022 (`m7-panorama-y-cierre.mjs`).
- El caso de prensa de la diapositiva 47 y el término "carreros" de la diapositiva 34.

Lo único que se ajustó al transcribir:

1. **Erratas evidentes** del original ("trasportista", "Bussiness Asociate", "perdida" por
   "pérdida") y uso consistente de PHI / e-PHI.
2. **En el caso de prensa** no se reproduce el nombre de usuario de la cuenta de Instagram del
   cirujano: identifica a una persona real y no aporta nada al punto que enseña la diapositiva.
   El titular y el hecho están completos.
3. **Ejemplos operacionales añadidos** — situaciones propias de la operación que no estaban en el
   deck: entrega a un vecino no autorizado, tableta de ruta desatendida, manifiesto descartado en
   zafacón común, mensaje de voz que menciona el medicamento, foto de la etiqueta en redes
   sociales. Aprobados por el cliente.
4. Las diapositivas puramente decorativas (portadillas, logos de redes sociales, "KEEP IN MIND")
   no se recrearon como pantalla propia; su contenido está integrado al módulo correspondiente.

## Pendientes de configuración

- **Umbral de aprobación**: la versión se crea con `PassPercent = 70` (valor por defecto del
  modelo). No hay endpoint para cambiarlo; si Advance Logistics quiere otro umbral hay que
  ajustarlo en `TrainingVersion` o añadir el endpoint.
- **Logo y firma del certificado**: `certificate.logoDataUrl` y `signatureDataUrl` quedaron
  vacíos. Se cargan como data URL (imagen, máx. ~1 MB) en `course.mjs` o desde la app.
- **Asignación**: después de publicar, asignar el curso a los grupos de usuarios que deben
  tomarlo (conductores, despacho, oficina) desde la interfaz de autoría.
- **Autorización del material**: el contenido proviene de MM & Associates, LLC. Conviene tener
  por escrito el permiso de uso dentro de la plataforma para este cliente.
