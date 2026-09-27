# Cumplimiento con la Ley HIPAA

Recreación **nativa** del deck `Adiestramiento Plataforma - Ley HIPAA.pptx`, en **modo
presentación**: cada lámina se dibuja en un escenario 16:9 (1280×720) con el mismo estilo que
Hostigamiento Sexual (fondo negro, panel diagonal con el título en mayúsculas), aquí con
acento verde azulado. Reemplaza el curso anterior `content/hipaa-advance-logistics`
(retirado; ver `tools/sql/borrar-hipaa-advance-logistics.sql`).

- **18 láminas del deck, 20 en la plataforma**: 1 portada, 2-3 instrucciones y objetivo, 4-12
  contenido, 13-18 las **6 preguntas** de selección única (60 puntos; se aprueba con 70 % = 42
  puntos), y una lámina final «Recursos adicionales» fuera del deck. El Objetivo (lámina 3) y
  la lista de información protegida (lámina 5) van repartidos en dos láminas cada uno porque
  enteros no caben en 720 px.
- **Pantalla de entrada** antes de la portada (`intro()` en `m0-apertura.mjs`, 20 min
  estimados). No es lámina ni cuenta en el contador.
- Recurrencia **anual** (`recurrenceMonths: 12`, reabre 30 días antes de vencer).
- `course.mjs` trae `playerConfig.presentation` (tema y transición `cover`).

## Uso

```bash
node content/ley-hipaa/seed.mjs --preview
node content/ley-hipaa/seed.mjs --update --url https://<host-produccion> --email autor@advancelogistics.com --password "***"
```

`--preview` escribe `preview.html` con las láminas tal como las dibuja el reproductor y marca
**«Excede la lámina»** en las que no quepan. `--update` actualiza el curso que ya existe (por
título exacto). Queda en **borrador**; añade `--publish` para publicarlo en la misma corrida.
Desde el servidor, `tools/Seed-Curso.ps1 -Update -Publish` con el `course.json` generado hace
lo mismo.

## Fidelidad al material original

- Portada: `img/portada.jpg`, la imagen del propio deck.
- La lámina 5 no tiene encabezado propio en el deck: va sin título; su cierre («no se debe
  acceder a información por curiosidad») pasa a una lámina «Importante».
- Los pasos de la lámina 11 van numerados, como en el deck.
- Ninguna pregunta traía marcada la respuesta correcta en el PPTX; se determinó a partir
  del contenido. **Revísalas en `--preview` antes de publicar.**
- «Recursos adicionales»: la página de adiestramiento de HIPAA de HHS.gov y el video
  «¿Qué es la Ley HIPAA?» (YouTube Shorts) como enlace, no incrustado: el Short da
  «Error 153» en el reproductor incrustado.
