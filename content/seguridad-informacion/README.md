# Seguridad de la Información para Empleados

Curso de **Adiestramiento** de Advance Logistics, recortado del que estaba en la base de
producción (versión 3, borrador; el contenido original completo está en
`original-produccion.json`). Los cambios se aprobaron pantalla por pantalla en
https://claude.ai/artifact/Lq5gRVGuks3KSVYgdk7amP.

- **51 láminas** (antes 94) en 8 módulos y **32 preguntas** activas de 1 punto (las 4 de
  selección de cada módulo). Se aprueba con 70 %. Unos 30–35 minutos.
- Las 8 preguntas abiertas se suben **desactivadas** (`off()`): quedan en el borrador y se
  pueden activar desde la app, pero el empleado no las ve.
- **Modo presentación en tema claro** (blanco y verde), el estilo de los cursos de
  Adiestramiento. Composiciones variadas; el panel diagonal, una vez por módulo como mucho.
- Las 5 fotos que se conservan ya estaban subidas a la plataforma (`/media/…`): en la vista
  previa local salen como recuadro gris; en el curso real se ven.
- Voz de las láminas: `training.voice` (`es-PR-KarinaNeural`).

## Uso

```bash
node content/seguridad-informacion/seed.mjs --preview
node content/seguridad-informacion/seed.mjs --dry-run
```

En el servidor, con el `course.json` generado (actualiza el curso existente por su título,
graba la voz y publica):

```bash
.\tools\Seed-Curso.ps1 -Url http://localhost:8086 -Email autor@advancelogisticspr.com -Password "***" -CoursePath .\content\seguridad-informacion\course.json -Update -Voz -Publish
```

## Pendiente de la empresa

Estas láminas quedan con texto neutro hasta que la empresa conteste:

- **Canal para reportar incidentes** (módulo 8): hoy dice «tu supervisor o el departamento de sistemas».
- **VPN y teléfono personal** (módulo 6): hoy dice «si la empresa te la dio» y «si la empresa lo permite».
- **WhatsApp** (módulo 7): hoy dice «solo por los canales que aprobó la empresa».
- **Gestor de contraseñas** (módulo 3): consejo general, sin marcas.
- **Leyes citadas** (módulo 7): HIPAA y Ley Núm. 111-2005 de Puerto Rico en vez de GDPR; a confirmar con Legal.
