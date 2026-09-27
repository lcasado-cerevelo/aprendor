// Módulos 3, 4 y 5.
// M3 (9 → 5): información incorrecta y datos inventados van juntas; «Fuentes falsas» precisa
// el caso real de los abogados (Nueva York, 2023, Mata contra Avianca, multa de 5,000 dólares).
// M4 (9 → 3): material protegido, imágenes y uso comercial en una lámina; «Código generado»
// sale (no aplica a casi nadie y traía un dato inexacto de la licencia GPL); «Riesgos
// legales» corrige el «ejemplo real» de las demandas (son contra las empresas de IA, p. ej.
// Getty Images contra Stability AI, 2023). El caso del diseñador pasa a un volante.
// M5 (8 → 4): sesgos y discriminación juntas, con el caso real de Amazon (Reuters, 2018).
import { slide, T, mod, bullets } from '../_shared/authoring.mjs';
import * as Q from './preguntas.mjs';

const caso = texto => slide({ layout: 'callout', kicker: 'Caso práctico' }, T(`<p>${texto}</p>`));

export const m3 = [
  mod('Módulo 3 — Alucinaciones y Errores de la IA',
    'La IA puede presentar información inventada como si fuera 100 % real. No es una falla: es cómo funcionan estos modelos. Por eso todo se verifica.'),

  slide({ layout: 'dark', title: '¿Qué es una alucinación?' },
    T(`<p>Es cuando la IA:</p>
       ${bullets([
         'Da información incorrecta o inventa datos.',
         'Crea fuentes que no existen.',
         'Mezcla hechos reales con ficción.',
         'Responde con seguridad aunque no tenga información suficiente.',
       ])}
       <p>No lo hace a propósito: son predicciones que parecen reales, pero no lo son. Por ejemplo, una ley, un estudio o una cita que no existe.</p>`)),

  slide({ layout: 'cards', title: 'Datos incorrectos e inventados', kicker: 'Cuando la IA «rellena» lo que no sabe' },
    T(`<ul>
         <li><b>Fechas</b><br>Dice que algo pasó en 2021 cuando fue en 2019.</li>
         <li><b>Cifras</b><br>Inventa números de ventas, costos o inventario.</li>
         <li><b>Nombres y documentos</b><br>Inventa empleados, números de contrato o registros internos.</li>
       </ul>
       <p>En el trabajo, un dato incorrecto puede llevar a una decisión equivocada o a un error en la operación.</p>`)),

  slide({ layout: 'split', title: 'Fuentes falsas' },
    T(`<p>La IA puede inventar artículos, estudios, citas de expertos y enlaces que no funcionan, con un formato perfecto.</p>
       <p><b>Caso real:</b> en 2023, en Nueva York, unos abogados presentaron a un tribunal un escrito con casos inventados por ChatGPT
       (caso Mata contra Avianca). El juez los multó con 5,000 dólares y el caso dio la vuelta al mundo.</p>`)),

  slide({ layout: 'callout', title: 'Si la información es importante, se verifica. Siempre.', kicker: 'Verificación de resultados' },
    T(`<p>La IA es una herramienta de apoyo, no una fuente. Confirma las cifras con los documentos de la empresa, las leyes y políticas con
       la fuente oficial, y revisa los enlaces y las referencias. Si hace falta, consulta a quien sabe.</p>`)),

  ...Q.m3.slice(0, 7),
  caso('Un empleado usa IA para preparar un análisis. La herramienta produce cifras que parecen correctas, pero ninguna coincide con los reportes oficiales.'),
  ...Q.m3.slice(7),
];

export const m4 = [
  mod('Módulo 4 — Propiedad Intelectual y Derechos de Autor',
    'Que la IA pueda generar un texto o una imagen no significa que se pueda usar sin restricciones.'),

  slide({ layout: 'dark', title: 'Lo que genera la IA puede tener dueño' },
    T(`<p>Libros, fotos, música, logos, diseños y textos de otros están protegidos por derechos de autor. La IA puede generar algo muy
       parecido a una obra protegida, sin que lo busques.</p>
       ${bullets([
         'Una imagen generada puede parecerse demasiado a una foto con derechos.',
         'Un texto puede repetir frases de un artículo protegido.',
         'No todas las herramientas permiten usar lo generado en anuncios o productos.',
       ])}`)),

  slide({ layout: 'band', title: 'Riesgos legales' },
    T(`<p>Usar sin revisar lo que genera la IA puede traer reclamaciones por derechos de autor, uso indebido de marcas o de la imagen
       de una persona, y daño a la reputación.</p>
       <p>Hay demandas de artistas y agencias de fotos contra las empresas que crean estas herramientas (por ejemplo, Getty Images contra
       Stability AI, 2023). El tema legal no está resuelto: por eso se revisa antes de publicar algo generado con IA.</p>
       <p><b>La IA no quita la responsabilidad legal de quien la usa.</b></p>`)),

  ...Q.m4.slice(0, 7),
  caso('Un empleado usa IA para hacer la imagen del volante de una promoción. La imagen imita claramente el estilo de un artista famoso.'),
  ...Q.m4.slice(7),
];

export const m5 = [
  mod('Módulo 5 — IA en la Toma de Decisiones',
    'La IA puede analizar y recomendar, pero no entiende consecuencias, ética ni el impacto en las personas. La decisión la toma una persona.'),

  slide({ layout: 'split', title: 'Sesgos y discriminación' },
    T(`<p>La IA aprende de datos del pasado. Si esos datos tienen sesgos (de género, edad, origen o idioma), la IA los repite y los agranda.</p>
       <p><b>Caso real:</b> en 2018, Amazon dejó de usar una herramienta de contratación que favorecía a los hombres porque había aprendido
       de solicitudes de años anteriores (Reuters).</p>
       <p>La IA puede discriminar sin intención, pero con un impacto real y consecuencias legales.</p>`)),

  slide({ layout: 'callout', title: 'La IA asiste, pero no decide', kicker: 'Supervisión humana' },
    T(`<p>Cuando la IA recomienda, evalúa personas, clasifica casos o sugiere acciones, una persona valida la información, corrige errores,
       detecta sesgos y toma la decisión final.</p>`)),

  slide({ layout: 'dark', title: 'Responsabilidad final' },
    T(`<p>La responsabilidad siempre es de quien usa la IA:</p>
       ${bullets([
         'Si la IA se equivoca, el responsable es quien la usó.',
         'Si inventa información, el empleado debe detectarla.',
         'Si recomienda algo, el empleado debe evaluarlo.',
       ])}
       <p>La IA no puede ser demandada, disciplinada ni despedida.</p>`)),

  ...Q.m5.slice(0, 7),
  caso('Un sistema de IA recomienda rechazar a un candidato porque su universidad no aparece en los datos del modelo. El reclutador acepta la recomendación sin revisar.'),
  ...Q.m5.slice(7),
];
