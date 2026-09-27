# Acoso Laboral en el Empleo

Recreación **nativa** del deck `Adiestramiento Plataforma - Acoso Laboral.pptx`, basado en la
Ley Núm. 90-2020 de Puerto Rico, en **modo presentación**: cada lámina se dibuja en un
escenario 16:9 (1280×720) con el mismo estilo que Hostigamiento Sexual (fondo negro, panel
diagonal con el título en mayúsculas), aquí con acento azul.

- **17 láminas** en el orden del deck: 1 portada, 2-3 instrucciones y objetivo, 4-11
  contenido, 12-17 las **6 preguntas** (4 de selección única y 2 de cierto o falso, 60
  puntos; se aprueba con 70 % = 42 puntos), y una lámina final «Recursos adicionales» fuera
  del deck.
- **Pantalla de entrada** antes de la portada (`intro()` en `m0-apertura.mjs`, 20 min
  estimados). No es lámina ni cuenta en el contador.
- Recurrencia **anual** (`recurrenceMonths: 12`, reabre 30 días antes de vencer).
- `course.mjs` trae `playerConfig.presentation` (tema y transición `cover`).

## Uso

```bash
node content/acoso-laboral/seed.mjs --preview
node content/acoso-laboral/seed.mjs --update --url https://<host-produccion> --email autor@advancelogistics.com --password "***"
```

`--preview` escribe `preview.html` con las láminas tal como las dibuja el reproductor y marca
**«Excede la lámina»** en las que no quepan. `--update` actualiza el curso que ya existe (por
título exacto). Queda en **borrador**; añade `--publish` para publicarlo en la misma corrida.
Desde el servidor, `tools/Seed-Curso.ps1 -Update -Publish` con el `course.json` generado hace
lo mismo.

## Fidelidad al material original

- Portada: `img/portada.jpg`, la foto del propio deck.
- Las láminas 5, 6 y 8 no tienen encabezado propio en el deck: van sin título.
- Los pasos de la lámina 9 van numerados, como en el deck; se corrigió la errata «entono».
- Las preguntas 5 y 6 son de cierto o falso: se modelaron como selección única con las
  opciones «Cierto» / «Falso».
- Ninguna pregunta traía marcada la respuesta correcta en el PPTX; se determinó a partir
  del contenido. **Revísalas en `--preview` antes de publicar.**
- Las fotos de banco que tenía la versión anterior de este curso (no estaban en el deck)
  se retiraron.
