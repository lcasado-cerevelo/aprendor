# Seguridad Digital para Choferes

Curso de **Adiestramiento** de Advance Logistics para los choferes. Toma lo que les aplica de
«Seguridad de la Información para Empleados» y de «Inteligencia Artificial: Uso Seguro,
Ético y Responsable», y lo lleva a su día a día: la tableta de trabajo, el teléfono personal
y **WhatsApp**, que es lo que usan a diario.

- **5 módulos, 27 láminas, 15 preguntas** (3 por módulo, 1 punto cada una; se aprueba con
  70 % = 11 puntos). Unos 18 minutos.
  1. Tu tableta y tu contraseña (bloqueo, no prestarla, verificación en dos pasos también
     en WhatsApp, si se pierde).
  2. La información que llevas (datos de pacientes y HIPAA, WhatsApp: lo que sí y lo que no,
     fotos con el teléfono personal, la entrega).
  3. Mensajes y llamadas falsas («su paquete no pudo ser entregado», el código de 6 dígitos
     de WhatsApp, voces clonadas con IA).
  4. La inteligencia artificial y tú (qué es, Meta AI dentro de WhatsApp, para qué sirve, qué
     nunca darle, que se equivoca).
  5. Si algo pasa (qué avisar, los pasos, «no existe el error tonto») y recursos adicionales.
- Cada módulo cierra con un caso práctico; la tercera pregunta del módulo es la del caso.
- **Modo presentación en tema claro** (blanco y verde), sin fotos: portada en banda. Voz:
  `es-PR-KarinaNeural` (unos 11,000 caracteres).
- Certificado como el de Seguridad de la Información; renovación anual.

## Asignación

Se asigna **por grupo**: el grupo «Choferes» lo tiene en su plan, y los cursos completos de
Seguridad de la Información e IA van en el plan de los demás grupos. Los tres cursos deben
tener la audiencia en «solo a los grupos»; los de cumplimiento siguen para todos.

## Uso

```bash
node content/choferes-seguridad-digital/seed.mjs --preview
node content/choferes-seguridad-digital/seed.mjs --dry-run
```

Es un curso nuevo, así que la primera vez va **sin** `-Update`:

```bash
.\tools\Seed-Curso.ps1 -Url http://localhost:8086 -Email autor@advancelogisticspr.com -Password "***" -CoursePath .\content\choferes-seguridad-digital\course.json -Voz -Publish
```
