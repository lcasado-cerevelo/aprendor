# Paquete de actualización: 4 cursos nuevos para Advance Logistics

Este paquete reemplaza el curso HIPAA actual de Advance Logistics por una versión
estándar más corta, y añade 3 cursos de cumplimiento nuevos. Los 4 cursos siguen el
mismo patrón de autoría que ya usa la plataforma (ver cada `README.md` de curso):
metadatos + módulos en `.mjs`, validados y sembrados en la base del tenant por API con
`seed.mjs` — no son parte de las migraciones de EF Core ni tocan el esquema de la base
de datos.

| Curso | Carpeta | Basado en | Preguntas |
|---|---|---|---|
| Cumplimiento con la Ley HIPAA | `content/ley-hipaa/` | `Adiestramiento Plataforma - Ley HIPAA.pptx` | 6 |
| Protocolo de Violencia Doméstica en el Empleo | `content/violencia-domestica/` | `Adiestramiento Plataforma - Violencia Domestica.pptx` | 6 |
| Acoso Laboral en el Empleo | `content/acoso-laboral/` | `Adiestramiento Plataforma - Acoso Laboral.pptx` (Ley 90-2020) | 6 |
| Hostigamiento Sexual en el Empleo | `content/hostigamiento-sexual/` | `Adiestramiento Plataforma - Hostigamiento Sexual.pptx` (Ley 17) | 7 |

El curso HIPAA anterior (`Cumplimiento HIPAA para transporte y logística`, 7 módulos,
52 pantallas) queda retirado; su carpeta de autoría (`content/hipaa-advance-logistics/`)
se eliminó del repositorio. El borrado del registro en producción se hace con
`tools/sql/borrar-hipaa-advance-logistics.sql` (ver más abajo) — decisión del cliente:
**borrado definitivo**, no archivar.

> **Los 4 cursos nuevos ya se sembraron una vez** (quedaron en `draft`, con Id propio
> cada uno). Todo lo que se edite después de eso — imágenes, videos, formato, texto —
> se sube con **`--update`** (`seed.mjs`) / **`-Update`** (`Seed-Curso.ps1`), que
> encuentra el curso existente **por su título exacto**, reemplaza sus pantallas y
> preguntas, y refresca título/descripción/recurrencia/certificado — **sin crear un
> curso nuevo ni duplicarlo**. Ver "Pasos para producción" más abajo.

## Formato dinámico y material de enriquecimiento

Los 4 cursos usan una presentación más visual que el curso HIPAA anterior: pasos
numerados, chips, tarjetas de estadística, citas destacadas y tarjetas de color en vez
de solo texto y listas (helpers nuevos en `content/_shared/authoring.mjs`: `steps`,
`chips`, `statGrid`, `quote`, `badgeList`, `img`, `video`, `resources`). El contenido
transcrito del PPTX no cambió — solo cómo se presenta.

También se añadió, verificado por búsqueda web antes de incluirlo (no inventado):

- **Duración estimada** en la caja de resumen de cada portada.
- **Fotos de Unsplash** (con crédito al fotógrafo), verificadas una por una — los 4
  cursos llevan foto real. En Violencia Doméstica costó más encontrar una apropiada
  (se descartaron varias fotos genéricas de "pareja" o "dolor físico"); se usó
  finalmente una imagen simbólica de esperanza (una mano hacia la luz), no literal.
- **Videos de YouTube, como enlaces en "Recursos adicionales"** (ninguno incrustado):
  HIPAA y Hostigamiento Sexual llevan uno cada uno; ambos se probaron incrustados
  primero y dieron "Error 153" (frecuente en YouTube Shorts), así que se dejaron como
  enlace normal — ese sí funciona sin pedir inicio de sesión. Acoso Laboral enlaza una
  conferencia completa sobre la Ley 90-2020 (larga, tampoco incrustada). Violencia
  Doméstica no lleva video: no se encontró uno corto y verificable en español
  específico sobre esa ley de Puerto Rico. El video de Hostigamiento Sexual solo se
  verificó por su título (metadatos) — revísalo antes de publicar.
- **Enlaces a recursos oficiales** al final de cada curso (texto de la ley, guías del
  Departamento del Trabajo o de la Procuradora de las Mujeres, y en Violencia
  Doméstica la línea de orientación 24 horas). El detalle de qué se verificó y qué se
  decidió no incluir está en el `README.md` de cada curso.
- De paso, se corrigió un error en la cita legal de Violencia Doméstica: el protocolo
  patronal lo exige la **Ley 217-2006** (no "Ley 54 y Ley 83-2018" como decía antes) —
  ver `content/violencia-domestica/README.md`.
- **Ilustraciones y fotos adicionales** (7 imágenes que el cliente ya tenía descargadas):
  se revisaron una por una — se usaron las que aplicaban por tema y se **descartaron
  explícitamente** dos por ser gráficas para un adiestramiento corporativo (manos atadas
  con soga, manos tapando boca y ojos — parecían secuestro o asfixia) y una por su estilo
  anime, inconsistente con el resto del catálogo. Las 5 que sí se usaron quedaron
  incrustadas como archivo local en `content/<curso>/img/` (igual que el curso HIPAA
  original), no como enlace externo. Detalle en el `README.md` de cada curso.
- **Video de Hostigamiento Sexual**: el cliente encontró y pidió incluir
  `youtube.com/watch?v=nKD7T0CX3XA` ("¿Qué es hostigamiento sexual?"). Igual que con el
  de HIPAA, no se pudo incrustar (Error 153) — quedó como enlace en "Recursos
  adicionales". Solo se verificó el título por metadatos, no el contenido completo.

## Antes de tocar producción: revisar el contenido

Cada curso tiene una vista previa estática que no requiere servidor ni credenciales, y
muestra las respuestas marcadas en verde:

```bash
node content/ley-hipaa/seed.mjs --preview
node content/violencia-domestica/seed.mjs --preview
node content/acoso-laboral/seed.mjs --preview
node content/hostigamiento-sexual/seed.mjs --preview
```

Esto escribe `preview.html` en cada carpeta — ábrelos en el navegador. **Los 4 decks
originales no marcaban la respuesta correcta de cada pregunta**; se determinó a partir
del contenido de las diapositivas y quedó documentada en el `README.md` de cada curso.
Revisa en particular las notas de `violencia-domestica/README.md` (una pregunta con
una opción "todas las anteriores" que no debería ser la correcta, y una oración del
deck original que quedó incompleta y se completó al transcribir) y de
`hostigamiento-sexual/README.md` antes de publicar.

## Pasos para producción

Requiere Node 18+ en la máquina desde la que se corra (usa `fetch` nativo; no hace
falta tenerlo en el servidor de producción). La cuenta debe tener rol `Admin`,
`Author` o `Moderator` **dentro del tenant de Advance Logistics** — un admin de
plataforma sin tenant no sirve, y los cursos se crean en la base del tenant que
resuelve el token de esa cuenta.

1. **Actualizar los 4 cursos** (ya existen desde la siembra anterior; esto reemplaza su
   contenido sin crear cursos duplicados — ver la nota más arriba). Quedan en borrador;
   se revisan en la app antes de publicar, o se añade `--publish` para publicarlos en la
   misma corrida:

   ```bash
   node content/ley-hipaa/seed.mjs --update --url https://<host-produccion> --email autor@advancelogistics.com --password "***"
   node content/violencia-domestica/seed.mjs --update --url https://<host-produccion> --email autor@advancelogistics.com --password "***"
   node content/acoso-laboral/seed.mjs --update --url https://<host-produccion> --email autor@advancelogistics.com --password "***"
   node content/hostigamiento-sexual/seed.mjs --update --url https://<host-produccion> --email autor@advancelogistics.com --password "***"
   ```

   Si el servidor de producción no tiene Node, usa `tools/Seed-Curso.ps1` (PowerShell,
   sin dependencias) contra el `course.json` ya generado de cada carpeta, con `-Update`:

   ```powershell
   .\tools\Seed-Curso.ps1 -Url https://<host-produccion> -Email autor@advancelogistics.com -Password "***" -CoursePath .\content\ley-hipaa\course.json -Update
   ```

   (Si algún día se necesita crear un curso nuevo desde cero en otro tenant, se omite
   `--update`/`-Update` y se corre igual que la primera vez — eso sí crea un `Training`
   nuevo.)

2. **Publicar** cada curso desde la interfaz de autoría (o con `--publish` en el paso
   anterior) una vez revisado.

3. **Asignar** cada curso a los grupos de usuarios que deben tomarlo, desde la
   interfaz de autoría.

4. **Retirar el curso HIPAA anterior** — borrado definitivo, contra la base del
   tenant de Advance Logistics:

   1. Abre `tools/sql/borrar-hipaa-advance-logistics.sql` en una herramienta de SQL
      conectada a esa base.
   2. Corre el `SELECT` del "PASO 0" (comentado al inicio del archivo) para obtener el
      `Id` del curso por su título exacto, y pégalo en `@TrainingId`.
   3. Con `@Simular = 1`, ejecuta el resto del script: muestra cuántas versiones,
      intentos y certificados tiene el curso, sin borrar nada.
   4. Si el curso **tiene** intentos o certificados, el script se niega a borrar
      (`@PermitirConHistorial = 0`) — confirma con el cliente si de verdad se quiere
      perder esa evidencia antes de forzar el borrado (`@PermitirConHistorial = 1`).
   5. Si los números cuadran y no hay objeción, cambia a `@Simular = 0` y vuelve a
      ejecutar.

   Respalda la base antes de este paso — es la única operación de este paquete que no
   tiene vuelta atrás.

## Configuración pendiente (los 4 cursos)

- **Firma del certificado**: los 4 cursos quedaron con `signatoryName: 'Recursos
  Humanos'` y sin logo/firma configurados (`certificate.logoDataUrl` /
  `signatureDataUrl` vacíos, a diferencia del curso HIPAA anterior que usaba a la
  abogada autora del material). Ajusta el nombre y cargo del firmante real, y el logo,
  desde `course.mjs` de cada curso o desde la interfaz de autoría.
- **Umbral de aprobación**: los 4 quedan con el valor por defecto del modelo (70%). No
  hay endpoint para cambiarlo por curso; si se necesita otro umbral hay que ajustar
  `TrainingVersion.PassPercent` directamente o añadir el endpoint.
- **Recurrencia**: los 4 quedan en 12 meses (se reabren 30 días antes de vencer),
  igual que el curso HIPAA anterior. Ajustar si alguna de las leyes que cubren
  (Ley 54, Ley 90-2020, Ley 17) no requiere re-adiestramiento anual en la política
  interna de la empresa.
