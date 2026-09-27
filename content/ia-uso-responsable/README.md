# Inteligencia Artificial: Uso Seguro, Ético y Responsable

Curso de **Adiestramiento** de Advance Logistics, recortado del que estaba en la base de
producción (versión 1, borrador; el contenido original completo está en
`original-produccion.json`). Los cambios se aprobaron pantalla por pantalla en
https://claude.ai/artifact/Lq5gRVGuks3KSVYgdk7amP.

- **35 láminas** (antes 75) en 7 módulos: el módulo 7 original (Casos prácticos) se
  repartió entre el 2 y el 6. Unos 25–30 minutos.
- **Banco de preguntas completo** (`preguntas.mjs`, generado del original): las 112 se suben,
  **27 activas** (3–4 por módulo) y 85 **desactivadas**, para cambiar el subconjunto desde la
  app sin perder el banco. Las preguntas de un caso práctico llevan el caso en el enunciado
  («En el caso del contrato, …»).
- Frente a la lista aprobada, el subconjunto cambia en tres módulos para que cada caso
  práctico tenga su pregunta: en el 1 entra «¿qué debe hacer el empleado?» del caso del
  informe (sale «todas las herramientas son seguras…»); en el 4, «¿qué riesgo existe?» del
  caso del volante (sale «¿qué riesgo existe con imágenes generadas?»); en el 8, «¿qué
  ocurrió?» del caso del contrato (sale «los empleados deben aceptar la política»).
- Datos corregidos: memoria de los modelos de lenguaje, Samsung (2023) como único caso real
  del módulo 2 (los demás pasan a ejemplos), abogados de Nueva York (Mata contra Avianca,
  2023), demandas por imágenes (contra las empresas de IA, p. ej. Getty Images contra
  Stability AI), Amazon (Reuters, 2018). Sale la lámina de código generado (dato GPL inexacto).
- **Modo presentación en tema claro** (blanco y verde). Voz: `es-PR-KarinaNeural`.

## Uso

```bash
node content/ia-uso-responsable/seed.mjs --preview
node content/ia-uso-responsable/seed.mjs --dry-run
```

```bash
.\tools\Seed-Curso.ps1 -Url http://localhost:8086 -Email autor@advancelogisticspr.com -Password "***" -CoursePath .\content\ia-uso-responsable\course.json -Update -Voz -Publish
```

## Pendiente de la empresa

- **Política de IA** (módulo 8, que ahora se llama «Reglas de la Empresa para Usar IA»): si
  existe una política escrita, las láminas de herramientas autorizadas y consecuencias deben
  decir lo que ella dice. Hoy no nombran herramientas ni sanciones concretas.
- **Leyes citadas** (módulo 2): HIPAA y Ley Núm. 111-2005 de Puerto Rico en vez de GDPR/CCPA.
