# Cumplimiento con la Ley HIPAA

Recreación **nativa** (texto + HTML, sin imágenes escaneadas) del deck
`Adiestramiento Plataforma - Ley HIPAA.pptx`. Reemplaza el curso anterior
`content/hipaa-advance-logistics` (retirado; ver `tools/sql/borrar-hipaa-advance-logistics.sql`).

- **5 módulos**, **12 pantallas** de contenido (`Info`) y **6 preguntas** de selección
  única (60 puntos) — se aprueba con 70% (42 puntos).
- Recurrencia **anual** (`recurrenceMonths: 12`, reabre 30 días antes de vencer).

## Archivos

| Archivo | Qué es |
|---|---|
| `../_shared/authoring.mjs` | Helpers y estilos compartidos por los 4 cursos nuevos. |
| `m0-apertura.mjs` … `m4-evaluacion.mjs` | El contenido, un archivo por módulo. **Aquí se edita el curso.** |
| `course.mjs` | Metadatos del curso (título, categoría, recurrencia, certificado) y ensamblaje de los módulos. |
| `seed.mjs` | Valida, genera vista previa/JSON y siembra el curso en la plataforma por API. |
| `preview.html` | Generado. Todo el curso en una página, con las respuestas correctas marcadas — para revisión. |
| `course.json` | Generado. El curso completo en JSON. |

## Uso

Revisar el contenido antes de subir nada (no necesita servidor ni credenciales):

```bash
node content/ley-hipaa/seed.mjs --preview
```

Actualizar el curso que ya existe en la base del tenant de Advance Logistics (el usuario
debe tener rol `Admin`, `Author` o `Moderator` en ese tenant):

```bash
node content/ley-hipaa/seed.mjs --update --url https://<host-produccion> --email autor@advancelogistics.com --password "***"
```

`--update` encuentra el curso por su título exacto y reemplaza su contenido — no crea
uno nuevo. Si en algún momento hace falta crear este curso desde cero en otro tenant,
se omite `--update`. Queda en **borrador**. Añade `--publish` para publicarlo en la
misma corrida, o publícalo desde la interfaz de autoría luego de revisarlo.

## Fidelidad al material original

El contenido y el orden de las pantallas siguen el deck tal cual, incluyendo sus
propias secciones "Instrucciones" y "Objetivo". Las 6 preguntas del deck no traían
marcada la respuesta correcta (el PPTX solo lista las opciones) — se determinó la
respuesta correcta de cada una a partir del contenido de las diapositivas anteriores;
**revísalas en `--preview` antes de publicar**.

El deck trae una imagen decorativa de portada (íconos genéricos de stock); se sustituyó
por un ícono propio en el mismo estilo que el resto del catálogo, en vez de incrustar
una imagen de banco de imágenes de origen desconocido.

## Material de enriquecimiento (fuera del deck original)

- **Duración estimada**: 20 min, añadida a la caja de resumen de la portada.
- **Foto**: una de Unsplash (Vitaly Gariev), con crédito visible, en la pantalla "¿Qué
  es HIPAA?". Se verificó que la URL carga (no es una imagen inventada).
- **Video**: "¿Qué es la Ley HIPAA? Protección de la privacidad en la salud" (YouTube
  Shorts, en español) — como **enlace** en "Recursos adicionales", no incrustado. Se
  intentó incrustarlo primero, pero da "Error 153" (típico de los Shorts, que muchas
  veces no soportan el reproductor clásico); el enlace normal sí funciona sin pedir
  inicio de sesión.
- **Recursos adicionales**: el video anterior más un enlace a la página de
  adiestramiento de HIPAA de HHS.gov, al final del curso.
- El formato de varias pantallas se hizo más visual (pasos numerados, chips, tarjetas de
  estadística, cita destacada) usando los nuevos helpers de `../_shared/authoring.mjs`
  (`steps`, `chips`, `statGrid`, `quote`, `badgeList`) — el contenido no cambió, solo la
  presentación.

