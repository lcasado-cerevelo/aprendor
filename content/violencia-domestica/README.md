# Protocolo de Manejo de Situaciones de Violencia Doméstica en el Empleo

Recreación **nativa** del deck `Adiestramiento Plataforma - Violencia Domestica.pptx`,
basado en la Ley Núm. 54-1989 (querellas y órdenes de protección), la Ley Núm. 217-2006
(exige el protocolo patronal, supervisado por PR-OSHA) y la Ley Núm. 83-2019 (licencia
especial de 15 días).

- **4 módulos**, **10 pantallas** de contenido (`Info`) y **6 preguntas** de selección
  única (60 puntos) — se aprueba con 70% (42 puntos).
- Recurrencia **anual** (`recurrenceMonths: 12`, reabre 30 días antes de vencer).

## Uso

```bash
node content/violencia-domestica/seed.mjs --preview
node content/violencia-domestica/seed.mjs --update --url https://<host-produccion> --email autor@advancelogistics.com --password "***"
```

`--update` actualiza el curso que ya existe (por título exacto) en vez de crear uno
nuevo. Queda en **borrador**. Añade `--publish` para publicarlo en la misma corrida.

## Fidelidad al material original y ajustes hechos al transcribir

- Las 6 preguntas del deck no traían marcada la respuesta correcta; se determinó a
  partir del contenido de las diapositivas anteriores. **Revísalas en `--preview`
  antes de publicar.**
- La pregunta 5 del deck ("¿Para cuál de las siguientes actividades puede utilizarse la
  licencia especial?") incluye como opciones "vacacionar" e "ir a citas de hijos" junto
  a "solicitar una Orden de Protección" y "todas las anteriores" — pero la propia
  diapositiva 9 del deck limita el uso de la licencia a gestiones relacionadas con la
  situación de violencia doméstica (orden de protección, asistencia legal, vivienda
  segura, citas médicas relacionadas). Se marcó **"Solicitar una Orden de Protección"**
  como la respuesta correcta, no "todas las anteriores". Confirmar con quien preparó
  el material que esa es la intención de la pregunta.
- La diapositiva 10 del deck original ("Importante") termina la oración a mitad de
  frase: *"Si tiene dudas sobre el Protocolo de "* — quedó incompleta en el PPTX
  fuente. Se completó como *"...comuníquese con su supervisor, la Administradora o
  Recursos Humanos"* para no dejar una pantalla cortada. **Verificar con el cliente
  que ese cierre es el correcto** antes de publicar.
- El deck trae una imagen decorativa de portada (banco de imágenes genérico); se
  sustituyó por un ícono propio en el mismo estilo que el resto del catálogo.
- Se corrigió además la cita legal de este README y de `course.mjs`: el deck y una
  versión anterior de este archivo citaban solo "Ley 54 y Ley 83-2018", pero el
  protocolo patronal en sí lo exige la **Ley 217-2006** (Ley 54 es la ley general de
  violencia doméstica, y la licencia especial es la **Ley 83-2019**, no 83-2018).

## Material de enriquecimiento (fuera del deck original)

- **Duración estimada**: 20 min, añadida a la caja de resumen de la portada.
- **Fotos**: 3 en total, con crédito visible.
  - "Importante": foto de Unsplash (Liana S), enlazada externa — una mano
    extendiéndose hacia la luz del atardecer, símbolo de esperanza/salir adelante, en
    vez de una imagen literal de violencia o de pareja. Se verificó que la URL carga.
  - "¿Qué es violencia doméstica?": `img/apoyo.jpg` — una persona apoya la mano en el
    hombro de una compañera preocupada.
  - "¿Quién puede ser víctima?": `img/sola.jpg` (Zhivko Minkov) — una persona sentada
    sola en una escalinata, para transmitir que le puede pasar a cualquiera.
  Estas 2 últimas las bajó el cliente de un lote de 11 fotos e ilustraciones; se
  incrustaron en local (`img/`), igual que el curso HIPAA original. De ese mismo lote
  se descartaron 4 explícitamente para este curso: dos por ser demasiado gráficas para
  un adiestramiento corporativo (manos atadas con soga, manos tapando boca y ojos —
  parecían secuestro o asfixia, con riesgo real de perturbar a una víctima real que
  tome el curso), una por su estilo anime (no combina con el resto del catálogo), y una
  cuarta (mujer cabizbaja en una mesa, en penumbra) por ser innecesariamente sombría
  además de las otras dos ya elegidas.
- **No se incrustó video**: no se encontró un video corto y verificable en español
  específico sobre la Ley 217-2006 o el protocolo patronal en Puerto Rico. Se priorizó
  no forzar material de relleno de baja calidad en un tema sensible.
- **Recursos adicionales** (verificados): la Línea de Orientación 24 horas de la Oficina
  de la Procuradora de las Mujeres (787-722-2977) y los textos oficiales de la Ley
  217-2006 y la Ley 83-2019, al final del curso.
- El formato de varias pantallas se hizo más visual (tarjetas de color por tipo de
  violencia, cita destacada, tarjetas de estadística para la licencia especial) usando
  los helpers de `../_shared/authoring.mjs` — el contenido no cambió, solo la presentación.
