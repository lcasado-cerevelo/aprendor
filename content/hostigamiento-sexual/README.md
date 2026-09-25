# Hostigamiento Sexual en el Empleo

Recreación **nativa** del deck `Adiestramiento Plataforma - Hostigamiento Sexual.pptx`,
basado en la Ley Núm. 17 de 1988 de Puerto Rico, en **modo presentación**: cada lámina se
dibuja en un escenario 16:9 (1280×720) con el estilo del deck (fondo negro, acento naranja,
panel diagonal con el título en mayúsculas).

- **21 láminas** en el orden del deck: 1 portada, 2-3 instrucciones y objetivo, 4-13
  contenido, 14-20 las **7 preguntas** de selección única (70 puntos; se aprueba con 70 % =
  49 puntos), y una lámina final «Recursos adicionales» fuera del deck.
- **Pantalla de entrada** antes de la portada (`intro()` en `m0-apertura.mjs`: descripción para
  el empleado y 25 min estimados; de fondo, la foto de la portada atenuada). No es lámina ni cuenta en el
  contador; el reproductor le añade láminas, preguntas y puntos, y `--preview` la dibuja aparte.
- Recurrencia **anual** (`recurrenceMonths: 12`, reabre 30 días antes de vencer).
- `course.mjs` trae `playerConfig.presentation` (tema y transición `cover`); `seed.mjs` lo
  manda a `PUT /trainings/{id}/player-config` al crear o actualizar el curso.
  `tools/Seed-Curso.ps1` hace lo mismo cuando `course.json` trae `training.playerConfig`.

## Uso

```bash
node content/hostigamiento-sexual/seed.mjs --preview
node content/hostigamiento-sexual/seed.mjs --update --url https://<host-produccion> --email autor@advancelogistics.com --password "***"
```

`--preview` escribe `preview.html` con las 21 láminas tal como las dibuja el reproductor
(copia la hoja de estilos de `wwwroot/player.html`), con la respuesta correcta en verde y
el aviso **«Excede la lámina»** en las que no quepan en 720 px — hay que recortar o repartir
ese texto antes de publicar. `--update` actualiza el curso que ya existe (por título exacto)
en vez de crear uno nuevo. Queda en **borrador**. Añade `--publish` para publicarlo en la
misma corrida.

## Layouts (helpers `slide()`, `photo()`, `bullets()`, `heading()` de `../_shared/authoring.mjs`)

| Láminas | `layout` | Notas |
|---|---|---|
| 1 | `cover` | `img/portada.jpg` (image12 del pptx) a sangre + banda blanca con el título |
| 2-3, 21 | `dark` | título del panel arriba, línea de acento, subtítulo (`heading`) y cuerpo |
| 4, 5, 7, 8, 10 | `split` | panel diagonal oscuro a la izquierda, contenido sobre blanco |
| 6, 11, 12 | `split` + `variant: 'right'` | panel blanco a la izquierda, contenido sobre oscuro (como el deck) |
| 13 | `split` + `variant: 'lines'` | fondo oscuro, título del panel entre dos líneas de acento |
| 9 | `photo-left` | `img/victima.jpg` (image13 del pptx); el pptx la recorta al 38 % central, `photoPos: '41% 50%'` reproduce el encuadre |
| 14-20 | pregunta | estilo oscuro con marco de acento, «PREGUNTA n» y opciones a./b./c./d. |

El cuerpo de cada lámina es HTML semántico (`p`, `ul/li`, `b`, `h3`) sin estilos inline: la
hoja de estilos del reproductor lo viste como el deck. Sin `presentation.enabled` el curso
se ve como cualquier otro (título, foto si la hay, texto normal).

## Fidelidad al material original

- Los SmartArt de las láminas 2-3 se rehicieron como lista y párrafos.
- El panel de la lámina 10 del deck dice «Hostigamiento y violencia doméstica en el empleo»
  (errata heredada de otro deck); se usa el título del panel del curso.
- La pregunta 5 del deck ("¿Cuál de las siguientes acciones está protegida contra
  represalias?") solo trae 3 opciones; se transcribió igual, con 3 opciones en vez de 4.
- Ninguna pregunta traía marcada la respuesta correcta en el PPTX; se determinó a
  partir del contenido de las diapositivas anteriores. **Revísalas en `--preview`
  antes de publicar.**
- La frase de cierre «No tiene que confrontar al hostigador para poder reportar la
  situación» (aclaración de lo que la lámina 9 da a entender) se movió a la lámina final
  de recursos, para que la lámina 9 quede como en el deck. Confirmar que no contradice la
  política interna de la empresa.
- Las fotos son las del deck (`img/portada.jpg`, `img/victima.jpg`), incrustadas como data
  URL en `course.json`. Las fotos de Unsplash y las ilustraciones locales de la versión
  anterior (`incomodidad.jpg`, `empoderamiento.jpg`) se retiraron: el deck no las traía.

## Material de enriquecimiento (fuera del deck original)

Se conserva como lámina final «Recursos adicionales»:

- **Video**: "¿Qué es hostigamiento sexual?" (`youtube.com/watch?v=nKD7T0CX3XA`) — lo
  encontró y pidió incluirlo el cliente. Como **enlace**, no incrustado (el ID da
  "Error 153" al incrustarse). Solo se verificó el título por metadatos, no el contenido
  completo del video — revísalo tú mismo antes de publicar.
- El texto oficial de la Ley 17 de 1988 (Departamento del Trabajo de PR) y las Guías para
  la Prevención y el Manejo del Hostigamiento Sexual en el Empleo de la Oficina de la
  Procuradora de las Mujeres. Nota: la Ley 17 fue enmendada por la Ley 82-2022 (añade la
  obligación de protocolo escrito); el deck no la menciona por nombre y este curso tampoco
  la cita aparte — si la empresa quiere referenciarla explícitamente, se puede añadir.
