// Entrada, portada, objetivos y módulos 1 y 2.
// M1 (9 → 6): IA generativa se acorta; «Qué son los LLMs» se corrige (las herramientas ya
// pueden guardar memoria e historial); los casos de uso pasan a ejemplos de logística.
// M2 (11 → 4): cinco láminas de «qué información» van en tarjetas; clientes y salud en una
// lámina con la ley de aquí; de los «casos reales» solo Samsung (2023) es verificable: los
// demás pasan a ejemplos, sin multa ni investigación.
import { slide, intro, T, mod, bullets, photo } from '../_shared/authoring.mjs';
import * as Q from './preguntas.mjs';

const caso = texto => slide({ layout: 'callout', kicker: 'Caso práctico' }, T(`<p>${texto}</p>`));

export const m0 = [
  intro({
    title: 'Inteligencia Artificial: Uso Seguro, Ético y Responsable',
    description:
      '<p>La inteligencia artificial ya está en el correo, el celular y las herramientas de trabajo. Bien usada ahorra tiempo; ' +
      'mal usada puede <b>filtrar información de la empresa y de los clientes</b> o llevar a decisiones equivocadas.</p>' +
      '<p>En este adiestramiento verás qué es la IA, qué información nunca se le comparte, por qué hay que verificar lo que ' +
      'dice y cómo usarla de forma responsable en el trabajo.</p>',
    minutes: 25,
  }),

  // Portada: foto de Steve Johnson en Unsplash (licencia de Unsplash: uso comercial sin
  // atribución), reducida a 1600 px. También es la foto de la tarjeta del curso y, atenuada,
  // el fondo de la pantalla de entrada.
  slide({ layout: 'cover', title: 'Inteligencia Artificial: Uso Seguro, Ético y Responsable',
          photo: photo(import.meta.url, './img/portada.jpg') }),

  slide({ layout: 'cards', title: 'Objetivos de aprendizaje', kicker: 'Al terminar este curso podrás' },
    T(`<ul>
         <li><b>Entender qué es la IA</b>, incluida la IA generativa y los modelos de lenguaje.</li>
         <li><b>Identificar qué información no se comparte</b> y evitar filtraciones.</li>
         <li><b>Reconocer errores e información inventada</b> y verificar los resultados.</li>
         <li><b>Respetar la propiedad intelectual</b> y evitar problemas legales.</li>
         <li><b>Evaluar decisiones apoyadas por IA</b> sin sesgos ni discriminación.</li>
         <li><b>Usar la IA de forma responsable</b>, con revisión humana y siguiendo las reglas de la empresa.</li>
       </ul>`)),
];

export const m1 = [
  mod('Módulo 1 — Introducción a la Inteligencia Artificial',
    'Usar la IA de forma responsable empieza por entender qué es, qué puede hacer y qué limitaciones tiene.'),

  slide({ layout: 'dark', title: '¿Qué es la inteligencia artificial?' },
    T(`<p>Es la capacidad de un sistema de computadoras de hacer tareas que normalmente requieren inteligencia humana:</p>
       ${bullets(['Analizar información y reconocer patrones.', 'Tomar decisiones y resolver problemas.', 'Generar contenido.', 'Entender el lenguaje de las personas.'])}
       <p><b>La IA no «piensa» ni «entiende» como una persona:</b> funciona con modelos matemáticos entrenados con enormes cantidades de datos.</p>`)),

  slide({ layout: 'band', title: '¿Qué es la IA generativa?' },
    T(`<p>Es la IA que crea contenido nuevo a partir de lo que aprendió: texto, imágenes, audio, video, código, resúmenes e ideas.</p>
       <p>Por ejemplo: redactar un correo, preparar una presentación, resumir un documento largo o proponer ideas para un problema.</p>
       <p><b>La IA generativa no copia: predice.</b> Y esa predicción puede ser útil… o completamente incorrecta.</p>`)),

  slide({ layout: 'split', title: 'Los modelos de lenguaje (LLM)' },
    T(`<p>Son modelos entrenados con enormes cantidades de texto para responder de forma fluida. Algunos conocidos: GPT, Claude, Gemini, Llama.</p>
       ${bullets([
         'Predicen la siguiente palabra a partir de patrones.',
         'No verifican los datos por sí mismos.',
         'Simulan que entienden, pero no entienden el mundo.',
         'Pueden equivocarse con total seguridad.',
         'Muchas herramientas guardan tus conversaciones o una «memoria»: lo que escribes puede quedar guardado.',
       ])}`)),

  slide({ layout: 'cards', title: 'Herramientas más conocidas', kicker: 'Cuáles se pueden usar lo decide la empresa (módulo 8)' },
    T(`<ul>
         <li><b>ChatGPT</b><br>De OpenAI. Genera textos, ideas, código y análisis.</li>
         <li><b>Claude</b><br>De Anthropic. Útil para análisis y documentos largos.</li>
         <li><b>Gemini</b><br>De Google, integrado con Gmail, Docs y Drive.</li>
         <li><b>Copilot</b><br>De Microsoft, integrado con Windows y Office.</li>
       </ul>`)),

  slide({ layout: 'dark', title: 'Usos en el trabajo' },
    T(`<p>En una empresa de logística, la IA puede ayudar a:</p>
       ${bullets([
         'Redactar y corregir correos y avisos.',
         'Resumir documentos largos y traducir textos que no son confidenciales.',
         'Ordenar ideas para planificar rutas, inventario o procesos.',
         'Preparar borradores de respuestas para clientes, sin datos personales.',
       ])}
       <p>Es una herramienta poderosa, siempre que se use de forma segura, ética y responsable.</p>`)),

  ...Q.m1.slice(0, 7),
  caso('Un empleado usa IA para redactar un informe. La herramienta genera un texto que se lee bien, pero incluye datos incorrectos y referencias inventadas.'),
  ...Q.m1.slice(7),
];

export const m2 = [
  mod('Módulo 2 — Riesgos de Compartir Información con IA',
    'Muchas herramientas de IA envían lo que escribes a servidores externos. Compartir información sensible puede provocar una filtración que no se puede deshacer.'),

  slide({ layout: 'cards', title: 'Lo que nunca va a una IA pública', kicker: 'Aunque sea para ahorrar tiempo' },
    T(`<ul>
         <li><b>Información confidencial</b><br>Estrategias, planes, procedimientos y políticas internas.</li>
         <li><b>Datos de clientes</b><br>Nombres, direcciones, teléfonos, pedidos, información financiera.</li>
         <li><b>Datos de empleados</b><br>Información personal, evaluaciones, nómina.</li>
         <li><b>Contratos</b><br>Tarifas, cláusulas y acuerdos de confidencialidad.</li>
         <li><b>Información financiera interna</b><br>Presupuestos, resultados, proyecciones.</li>
         <li><b>Contraseñas y accesos</b><br>Y también código o configuraciones de los sistemas.</li>
       </ul>`)),

  slide({ layout: 'dark', title: 'Datos de clientes y de salud: lo que dice la ley' },
    T(`<p>Los datos personales y de salud están protegidos por ley. Pegarlos en una IA pública puede ser una violación legal.</p>
       ${bullets([
         '<b>HIPAA</b> protege la información de salud de los pacientes: diagnósticos, medicamentos, órdenes médicas. Ninguna IA pública está autorizada para recibirla.',
         'En Puerto Rico, la <b>Ley Núm. 111-2005</b> obliga a avisar a las personas cuando se filtra su información personal.',
       ])}
       <p>Consecuencias: demandas, multas y pérdida de la confianza de los clientes.</p>`)),

  slide({ layout: 'band', title: 'Casos y ejemplos' },
    T(`<p><b>Caso real: Samsung (2023).</b> Empleados pegaron código interno en ChatGPT para corregir errores. La información quedó en
       servidores externos y la empresa restringió el uso de estas herramientas.</p>
       <p><b>Ejemplos de lo que puede pasar:</b> un analista pega datos de clientes para preparar un informe; un empleado pega un contrato
       para resumirlo; alguien escribe información médica de un paciente. En todos, la información sale de la empresa sin permiso.</p>
       <p><b>La IA no es peligrosa. El peligro es compartir lo que nunca debió salir de la empresa.</b></p>`)),

  ...Q.m2.slice(0, 7),
  caso('Un empleado copia y pega un contrato confidencial en una IA pública para pedir un resumen. La herramienta procesa el documento y responde.'),
  ...Q.m2.slice(7),
];
