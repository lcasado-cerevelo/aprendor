# Protocolo de Manejo de Situaciones de Violencia Doméstica en el Empleo

Recreación **nativa** del deck `Adiestramiento Plataforma - Violencia Domestica.pptx`, en
**modo presentación**: cada lámina se dibuja en un escenario 16:9 (1280×720) con el mismo
estilo que Hostigamiento Sexual (fondo negro, panel diagonal con el título en mayúsculas),
aquí con acento violeta. Basado en la Ley Núm. 54-1989 (querellas y órdenes de protección),
la Ley Núm. 217-2006 (exige el protocolo patronal, supervisado por PR-OSHA) y la Ley Núm.
83-2019 (licencia especial de 15 días).

- **17 láminas del deck, 19 en la plataforma**: 1 portada, 2-3 instrucciones y objetivo,
  4-10 contenido, 11 «Sesión de preguntas», 12-17 las **6 preguntas** de selección única (60
  puntos; se aprueba con 70 % = 42 puntos), y una lámina final «Recursos adicionales» fuera
  del deck. La lámina 9 (licencia especial) va repartida en dos —condiciones y usos— porque
  entera no cabe en 720 px.
- **Pantalla de entrada** antes de la portada (`intro()` en `m0-apertura.mjs`, 20 min
  estimados). No es lámina ni cuenta en el contador.
- Recurrencia **anual** (`recurrenceMonths: 12`, reabre 30 días antes de vencer).
- `course.mjs` trae `playerConfig.presentation` (tema y transición `cover`).

## Uso

```bash
node content/violencia-domestica/seed.mjs --preview
node content/violencia-domestica/seed.mjs --update --url https://<host-produccion> --email autor@advancelogistics.com --password "***"
```

`--preview` escribe `preview.html` con las láminas tal como las dibuja el reproductor y marca
**«Excede la lámina»** en las que no quepan. `--update` actualiza el curso que ya existe (por
título exacto). Queda en **borrador**; añade `--publish` para publicarlo en la misma corrida.
Desde el servidor, `tools/Seed-Curso.ps1 -Update -Publish` con el `course.json` generado hace
lo mismo.

## Fidelidad al material original y ajustes hechos al transcribir

- Portada: `img/portada.jpg`, la foto del propio deck.
- Las láminas 5 y 8 no tienen encabezado propio en el deck: van sin título.
- Erratas corregidas: «Si es usted identifica», «antes las autoridades», «domética».
- La lámina 10 termina a mitad de frase en el deck («Si tiene dudas sobre el Protocolo
  de»); se completó como «…comuníquese con su supervisor, la Administradora o Recursos
  Humanos». **Confirmar con el cliente.**
- Ninguna pregunta traía marcada la respuesta correcta en el PPTX; se determinó a partir
  del contenido. En la 5 («¿Para cuál de las siguientes actividades puede utilizarse la
  licencia especial?») se marcó «Solicitar una Orden de Protección», no «Todas las
  anteriores», porque la licencia se limita a gestiones relacionadas con la situación.
  **Revísalas en `--preview` antes de publicar.**
- Las fotos de banco que tenía la versión anterior de este curso (no estaban en el deck)
  se retiraron.
- «Recursos adicionales»: la Línea de Orientación 24 horas de la Oficina de la Procuradora
  de las Mujeres (787-722-2977) y los textos oficiales de la Ley 217-2006 y la Ley 83-2019.
