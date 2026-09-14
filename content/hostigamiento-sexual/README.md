# Hostigamiento Sexual en el Empleo

Recreación **nativa** del deck `Adiestramiento Plataforma - Hostigamiento Sexual.pptx`,
basado en la Ley Núm. 17 de 1988 de Puerto Rico.

- **5 módulos**, **13 pantallas** de contenido (`Info`) y **7 preguntas** de selección
  única (70 puntos) — se aprueba con 70% (49 puntos).
- Recurrencia **anual** (`recurrenceMonths: 12`, reabre 30 días antes de vencer).

## Uso

```bash
node content/hostigamiento-sexual/seed.mjs --preview
node content/hostigamiento-sexual/seed.mjs --update --url https://<host-produccion> --email autor@advancelogistics.com --password "***"
```

`--update` actualiza el curso que ya existe (por título exacto) en vez de crear uno
nuevo. Queda en **borrador**. Añade `--publish` para publicarlo en la misma corrida.

## Fidelidad al material original

- La pregunta 5 del deck ("¿Cuál de las siguientes acciones está protegida contra
  represalias?") solo trae 3 opciones sin letras, a diferencia del resto de las
  preguntas; se transcribió igual, con 3 opciones en vez de 4.
- Ninguna pregunta traía marcada la respuesta correcta en el PPTX; se determinó a
  partir del contenido de las diapositivas anteriores. **Revísalas en `--preview`
  antes de publicar.**
- En el módulo 2 se añadió una frase de cierre ("No tiene que confrontar al hostigador
  para poder reportar la situación") para hacer explícito lo que la diapositiva 9 ya
  daba a entender ("si se siente seguro para hacerlo..."). Es una aclaración menor,
  no un hecho nuevo — confirmar que no contradice la política interna de la empresa.
- El deck trae una imagen decorativa de portada (banco de imágenes genérico); se
  sustituyó por un ícono propio en el mismo estilo que el resto del catálogo.

## Material de enriquecimiento (fuera del deck original)

- **Duración estimada**: 25 min, añadida a la caja de resumen de la portada.
- **Fotos e ilustraciones**: 3 en total, con crédito visible.
  - "¿Qué es el hostigamiento sexual bajo la Ley Núm. 17?": foto de Unsplash (Vitaly
    Gariev), enlazada externa — se verificó que la URL carga.
  - "¿Cómo se puede manifestar?": `img/incomodidad.jpg` (Fast Ink) — una sombra
    amenazante detrás de una persona, para el ambiente hostil/intimidante.
  - "Recomendaciones": `img/empoderamiento.jpg` (Visuals) — un puño en alto contra la
    luz, para transmitir determinación a la hora de reportar.
  Estas 2 últimas las bajó el cliente y se incrustaron en local (`img/`), igual que el
  curso HIPAA original.
- **Video**: "¿Qué es hostigamiento sexual?" (`youtube.com/watch?v=nKD7T0CX3XA`) — lo
  encontró y pidió incluirlo el cliente. Como **enlace** en "Recursos adicionales", no
  incrustado (el ID de este video también da "Error 153" al incrustarse, igual que el
  de HIPAA). Solo se verificó el título por metadatos, no el contenido completo del
  video — revísalo tú mismo antes de publicar.
- **Recursos adicionales**: el video anterior, el texto oficial de la Ley 17 de 1988
  (Departamento del Trabajo de PR) y las Guías para la Prevención y el Manejo del
  Hostigamiento Sexual en el Empleo de la Oficina de la Procuradora de las Mujeres, al
  final del curso. Nota:
  la Ley 17 fue enmendada por la Ley 82-2022 (añade la obligación de protocolo escrito);
  el deck no la menciona por nombre y este curso tampoco la cita aparte — si la empresa
  quiere referenciarla explícitamente, se puede añadir.
- El formato de varias pantallas se hizo más visual (cita destacada, chips, columnas,
  pasos numerados) usando los helpers de `../_shared/authoring.mjs` — el contenido no
  cambió, solo la presentación.
