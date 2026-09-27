// Módulos 6 y 8 (el 7 original, Casos prácticos, se repartió: los casos de la base de datos
// y de servicio al cliente pasan al 6; los del contrato, el código y el análisis financiero
// salen porque repiten el módulo 2 o no aplican. Sus preguntas quedan en el banco).
// M6 (9 → 5): «Qué nunca se debe compartir» pasa al módulo 2; revisión y aprobaciones juntas;
// los prompts usan ejemplos de logística.
// M8 (9 → 6): PENDIENTE DE LA EMPRESA si hay una política de IA escrita. Mientras tanto las
// láminas no nombran herramientas autorizadas ni sanciones concretas.
import { slide, T, mod, bullets, heading } from '../_shared/authoring.mjs';
import * as Q from './preguntas.mjs';

const caso = texto => slide({ layout: 'callout', kicker: 'Caso práctico' }, T(`<p>${texto}</p>`));

export const m6 = [
  mod('Módulo 6 — Uso Responsable de IA en el Trabajo',
    'La IA no reemplaza el criterio de una persona y no distingue entre información pública y confidencial. Estas son las reglas para usarla bien.'),

  slide({ layout: 'callout', title: 'Si lo puedes publicar en la página web de la empresa, lo puedes compartir con la IA.', kicker: 'Qué se puede compartir' },
    T(`<p>Preguntas generales, conceptos, ideas, ejercicios de redacción sin datos sensibles e información que la empresa ya publicó.
       Nada que identifique personas, que sea interno o que esté protegido por un acuerdo de confidencialidad.</p>`)),

  slide({ layout: 'split', title: 'Prompts seguros' },
    T(`<p>Un prompt (lo que le escribes a la IA) es seguro cuando no tiene nombres reales, datos de clientes, empleados o proveedores, ni documentos internos.</p>
       <p><b>Sí:</b> «Redacta un aviso de retraso de entrega, amable y breve, sin nombres».</p>
       <p><b>Sí:</b> «Dame ideas para organizar mejor el inventario de un almacén».</p>
       <p><b>No:</b> «Resume este contrato con el cliente» o «Analiza esta lista de clientes con sus direcciones».</p>`)),

  slide({ layout: 'dark', title: 'Revisión humana y aprobaciones' },
    T(`<p><b>Todo lo que genera la IA lo revisa una persona</b> antes de enviarlo a un cliente, publicarlo, usarlo en un documento oficial o decidir con él:
       que sea correcto, coherente, legal y profesional.</p>
       <p><b>Necesita aprobación previa</b> lo que tenga que ver con contenido legal, financiero o médico, lo que se publica fuera de la empresa,
       lo que afecta a clientes o proveedores y las automatizaciones de procesos. Pregunta a tu supervisor.</p>`)),

  slide({ layout: 'cards', title: 'Dos errores comunes', kicker: 'Qué pasó y qué debió hacerse' },
    T(`<ul>
         <li><b>La lista de clientes</b><br>Un empleado pega en ChatGPT una hoja de Excel con nombres, correos y direcciones de clientes para pedir un análisis. Esos datos ya salieron de la empresa y no se pueden recuperar. Debió usar una herramienta aprobada o quitar los datos personales.</li>
         <li><b>La queja del cliente</b><br>Otro pega el reclamo de un cliente, con su nombre y número de cuenta, para que la IA redacte la respuesta. Debió escribir el prompt sin datos personales y usar las plantillas de la empresa.</li>
       </ul>`)),

  ...Q.m6.slice(0, 7),
  caso('Un empleado copia y pega un archivo con datos de clientes en una IA pública para pedir un análisis. La herramienta genera un reporte detallado.'),
  ...Q.m6.slice(7),
  // Banco del módulo 7 original (Casos prácticos): desactivadas.
  ...Q.m7,
];

export const m8 = [
  mod('Módulo 8 — Reglas de la Empresa para Usar IA',
    'Las reglas protegen a la empresa, a los clientes y a los empleados. Si tienes dudas, pregunta antes de usar la IA.'),

  slide({ layout: 'dark', title: 'Uso permitido' },
    T(`<p>La IA se puede usar para tareas que no incluyen información sensible y no crean riesgos legales:</p>
       ${bullets([
         'Redactar textos generales.',
         'Buscar ideas y explicaciones.',
         'Resumir o traducir contenido público.',
         'Tareas internas que la empresa aprobó.',
       ])}`)),

  slide({ layout: 'cards', title: 'Uso prohibido', kicker: 'Además de compartir información sensible' },
    T(`<ul>
         <li><b>Evaluar personas</b><br>Empleados, candidatos o clientes.</li>
         <li><b>Decidir sin supervisión</b><br>La decisión final es de una persona.</li>
         <li><b>Contenido legal o financiero</b><br>Sin la aprobación que corresponde.</li>
         <li><b>Automatizar procesos</b><br>Sin autorización de la empresa.</li>
       </ul>`)),

  slide({ layout: 'band', title: 'Herramientas autorizadas' },
    T(`<p>Usa solo las herramientas de IA que la empresa haya aprobado. Una herramienta aprobada cumple con controles de seguridad,
       protección de datos y acuerdos de privacidad.</p>
       <p>Si no sabes si una herramienta está aprobada, <b>pregunta a tu supervisor antes de usarla</b>.</p>`)),

  slide({ layout: 'dark', title: 'Consecuencias del mal uso' },
    T(`<p>Usar mal la IA, por ejemplo filtrando datos de clientes o de la empresa, puede llevar a medidas disciplinarias según las
       políticas de la empresa, además de las consecuencias legales para la empresa.</p>
       <p><b>Cada empleado es responsable del uso que hace de la IA.</b></p>`)),

  slide({ layout: 'callout', title: 'Tu compromiso', kicker: 'Aceptación' },
    T(`<p>Al completar este adiestramiento queda registrado en Aprendor que conoces estas reglas. Cúmplelas, reporta los usos indebidos
       y, ante cualquier duda, consulta a tu supervisor o a Recursos Humanos.</p>`)),

  ...Q.m8.slice(0, 7),
  caso('Un empleado usa ChatGPT público para resumir un contrato interno.'),
  ...Q.m8.slice(7),

  // Lámina final antes del resultado (como en los cursos de cumplimiento). Enlaces
  // verificados el 27 sep 2026.
  slide({ layout: 'dark', title: 'Recursos adicionales' },
    T(`${heading('Recursos adicionales')}
       <p>Material complementario, opcional, para seguir aprendiendo:</p>
       ${bullets([
         '<a href="https://consumidor.ftc.gov/alertas-para-consumidores/2024/09/operacion-ai-comply-como-detectar-fraudes-y-enganos-con-inteligencia-artificial-aplicada" target="_blank" rel="noopener">Cómo detectar fraudes y engaños con inteligencia artificial</a> — Comisión Federal de Comercio (FTC), en español.',
         '<a href="https://consumidor.ftc.gov/articulos/como-reconocer-y-evitar-las-estafas-de-phishing" target="_blank" rel="noopener">Cómo reconocer y evitar las estafas de phishing</a> — la IA también se usa para hacer mensajes falsos más creíbles.',
       ])}
       <p><b>Recuerda:</b> nunca compartas con una IA pública información de la empresa, de clientes o de empleados, y verifica siempre lo que te responda. Si tienes dudas, pregunta a tu supervisor antes de usarla.</p>`)),
];
