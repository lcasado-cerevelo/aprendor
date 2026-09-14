# Acoso Laboral en el Empleo

Recreación **nativa** del deck `Adiestramiento Plataforma - Acoso Laboral.pptx`,
basado en la Ley Núm. 90-2020 de Puerto Rico.

- **4 módulos**, **11 pantallas** de contenido (`Info`) y **6 preguntas** (4 de
  selección única y 2 de cierto/falso, 60 puntos) — se aprueba con 70% (42 puntos).
- Recurrencia **anual** (`recurrenceMonths: 12`, reabre 30 días antes de vencer).

## Uso

```bash
node content/acoso-laboral/seed.mjs --preview
node content/acoso-laboral/seed.mjs --update --url https://<host-produccion> --email autor@advancelogistics.com --password "***"
```

`--update` actualiza el curso que ya existe (por título exacto) en vez de crear uno
nuevo. Queda en **borrador**. Añade `--publish` para publicarlo en la misma corrida.

## Fidelidad al material original

Las preguntas 5 y 6 del deck son de "Cierto o Falso"; la plataforma no tiene un tipo
de pregunta específico para eso, así que se modelaron como selección única con
opciones "Cierto" / "Falso". El resto de las preguntas no traían marcada la respuesta
correcta en el PPTX; se determinó a partir del contenido de las diapositivas
anteriores. **Revísalas en `--preview` antes de publicar.**

El deck trae una imagen decorativa de portada (banco de imágenes genérico); se
sustituyó por un ícono propio en el mismo estilo que el resto del catálogo.

## Material de enriquecimiento (fuera del deck original)

- **Duración estimada**: 20 min, añadida a la caja de resumen de la portada.
- **Fotos e ilustraciones**: 4 en total, con crédito visible.
  - "¿Qué es el acoso laboral?": foto de Unsplash (Vitaly Gariev), enlazada externa —
    se verificó que la URL carga.
  - "La Ley Núm. 90-2020": `img/exclusion.jpg` (Markus Spiske) — fichas del mismo color
    con una apartada del grupo, para ilustrar que el acoso no depende de jerarquía.
  - "Conductas que pueden constituir acoso laboral": `img/senalando.jpg` (Maulana Ahmad)
    — manos señalando a una persona, para las conductas que atentan contra la reputación.
  - "Prevenir el acoso laboral es responsabilidad de todos": `img/respeto.jpg` (Ruliff
    Andrean) — compañeros diversos chocando las manos, como contraste positivo.
  Las 3 últimas las bajó el cliente y las pedí incrustar en local (`img/`), igual que el
  patrón del curso HIPAA original — no son enlaces externos.
- **Recursos adicionales**: texto oficial de la Ley 90-2020 (LexJuris), las Guías sobre
  el Acoso Laboral en el Sector Privado del Departamento del Trabajo y Recursos Humanos,
  y un enlace (opcional, no incrustado) a una conferencia completa en YouTube sobre la
  ley — no se incrustó como video porque es una grabación larga de conferencia, no un
  video corto apto para verse dentro de una pantalla del curso.
- El formato de varias pantallas se hizo más visual (cita destacada, chips, tarjetas,
  pasos numerados) usando los helpers de `../_shared/authoring.mjs` — el contenido no
  cambió, solo la presentación.
