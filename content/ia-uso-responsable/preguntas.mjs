// Banco de preguntas del curso original (112), generado desde original-produccion.json.
// Activas: el subconjunto inicial (3–4 por módulo, 27 en total). Las demás se suben
// DESACTIVADAS (off): quedan en el borrador y se pueden activar desde la app para cambiar el
// subconjunto. Las preguntas de un caso práctico llevan el caso en el enunciado. Las del
// módulo 7 original (Casos prácticos, que se repartió) quedan en el banco del módulo 6.
// Cierto o falso: opciones «Verdadero» / «Falso» (vf).
import { mc, ms, off } from '../_shared/authoring.mjs';

const vf = (question, verdadero) => mc(question, ['Verdadero', 'Falso'], verdadero ? 'a' : 'b', 1);

export const m1 = [
  /* 1.1 */ mc("¿Qué es la Inteligencia Artificial?", ["Un sistema que piensa como un humano","Tecnología que realiza tareas basadas en patrones y datos","Un programa que nunca se equivoca","Un sistema de entretenimiento"], "b", 1),
  /* 1.2 */ mc("¿Qué es IA generativa?", ["IA que solo analiza datos","IA que crea contenido nuevo","IA que solo responde preguntas","IA que no puede generar texto"], "b", 1),
  /* 1.3 */ off(mc("¿Qué es un LLM?", ["Un modelo pequeño","Un modelo de lenguaje entrenado con grandes cantidades de texto","Un sistema de almacenamiento","Un hardware especializado"], "b", 1)),
  /* 1.4 */ off(mc("¿Cuál de estas herramientas es un asistente de IA corporativo?", ["ChatGPT","Gemini","Copilot","Claude"], "c", 1)),
  /* 1.5 */ off(mc("¿Qué describe un caso de uso empresarial?", ["Jugar con IA","Generar contenido útil para el trabajo","Usar IA sin supervisión","Compartir información personal"], "b", 1)),
  /* 1.6 */ off(ms("¿Qué puede generar la IA generativa?", ["Texto","Imágenes","Código","Información médica real"], ["a","b","c"], 1)),
  /* 1.7 */ off(ms("¿Qué herramientas son LLMs?", ["ChatGPT","Claude","Gemini","Excel"], ["a","b","c"], 1)),
  /* 1.8 */ off(mc("En el caso del informe, ¿qué ocurrió?", ["Un error humano","Una alucinación de IA","Un problema de red","Un fallo del sistema"], "b", 1)),
  /* 1.9 */ mc("En el caso del informe, ¿qué debe hacer el empleado?", ["Enviar el informe sin revisar","Verificar la información antes de usarla","Asumir que la IA siempre tiene razón","Compartir datos sensibles para mejorar la respuesta"], "b", 1),
  /* 1.10 */ off(vf("La IA generativa puede inventar información.", true)),
  /* 1.11 */ vf("Los LLMs entienden el mundo como un humano.", false),
  /* 1.12 */ off(vf("Copilot está diseñado para entornos corporativos.", true)),
  /* 1.13 */ off(vf("La IA puede ser útil en análisis, creatividad y productividad.", true)),
  /* 1.14 */ off(vf("Todas las herramientas de IA son seguras para compartir información confidencial.", false)),
];

export const m2 = [
  /* 2.1 */ mc("¿Cuál es el mayor riesgo al compartir información con IA pública?", ["Respuestas lentas","Filtración de datos sensibles","Falta de creatividad","Problemas de conexión"], "b", 1),
  /* 2.2 */ off(mc("¿Qué tipo de información nunca debe compartirse?", ["Información pública","Datos de clientes","Ideas generales","Preguntas técnicas sin contexto"], "b", 1)),
  /* 2.3 */ off(mc("¿Qué riesgo implica compartir contratos?", ["Mejorar la productividad","Violación de acuerdos de confidencialidad","Acelerar procesos","Obtener resúmenes más rápidos"], "b", 1)),
  /* 2.4 */ off(mc("¿Qué representa el código fuente?", ["Información irrelevante","Un activo crítico de la empresa","Un documento público","Un archivo temporal"], "b", 1)),
  /* 2.5 */ off(mc("¿Qué ocurrió en el caso de Samsung?", ["Se filtraron estados financieros","Se filtró código fuente interno","Se filtraron datos médicos","No ocurrió nada"], "b", 1)),
  /* 2.6 */ off(ms("¿Qué información es considerada altamente sensible?", ["Estados financieros","Código fuente","Información médica","Memes"], ["a","b","c"], 1)),
  /* 2.7 */ ms("¿Qué consecuencias puede tener una filtración?", ["Multas legales","Pérdida de clientes","Mejora de reputación","Despido del empleado"], ["a","b","d"], 1),
  /* 2.8 */ mc("En el caso del contrato, ¿qué riesgo ocurrió?", ["Ninguno","Filtración de información legal","Error técnico","Problema de formato"], "b", 1),
  /* 2.9 */ mc("En el caso del contrato, ¿qué debió hacer el empleado?", ["Usar una herramienta aprobada por la empresa","Compartir más información para mejorar la respuesta","Enviar el contrato sin revisar","Publicarlo en redes sociales"], "a", 1),
  /* 2.10 */ off(vf("Compartir datos de clientes con IA pública puede violar leyes.", true)),
  /* 2.11 */ off(vf("El código fuente puede compartirse si es para depurar errores.", false)),
  /* 2.12 */ off(vf("Las IA públicas pueden almacenar temporalmente la información ingresada.", true)),
  /* 2.13 */ off(vf("Los estados financieros son información sensible.", true)),
  /* 2.14 */ off(vf("Las filtraciones por IA no tienen consecuencias reales.", false)),
];

export const m3 = [
  /* 3.1 */ mc("¿Qué es una alucinación de IA?", ["Un error visual","Información inventada presentada como real","Un problema de hardware","Un fallo de conexión"], "b", 1),
  /* 3.2 */ off(mc("¿Qué puede generar la IA cuando no tiene suficiente información?", ["Datos inventados","Resultados perfectos","Información verificada","Cifras oficiales"], "a", 1)),
  /* 3.3 */ mc("¿Qué es una fuente falsa?", ["Un documento oficial","Una referencia inventada por la IA","Un enlace verificado","Un estudio académico real"], "b", 1),
  /* 3.4 */ off(mc("¿Qué debe hacerse con información generada por IA?", ["Usarla sin revisar","Verificarla con fuentes confiables","Asumir que es correcta","Compartirla inmediatamente"], "b", 1)),
  /* 3.5 */ off(mc("¿Qué tipo de información puede ser incorrecta?", ["Fechas","Cifras","Procedimientos","Todas las anteriores"], "d", 1)),
  /* 3.6 */ off(ms("¿Qué puede inventar la IA?", ["Datos financieros","Citas legales","Estudios académicos","Contraseñas reales"], ["a","b","c"], 1)),
  /* 3.7 */ off(ms("¿Qué acciones ayudan a evitar errores?", ["Verificar información","Revisar fuentes","Confiar ciegamente en la IA","Consultar documentos oficiales"], ["a","b","d"], 1)),
  /* 3.8 */ mc("En el caso de las cifras que no coinciden, ¿qué ocurrió?", ["Un error de formato","Una alucinación de IA","Un problema de impresión","Un fallo del servidor"], "b", 1),
  /* 3.9 */ mc("En el caso de las cifras que no coinciden, ¿qué debe hacer el empleado?", ["Usar las cifras generadas","Verificar los datos con reportes oficiales","Compartir el análisis sin revisar","Pedirle a la IA que invente más datos"], "b", 1),
  /* 3.10 */ off(vf("La IA puede generar información falsa con total confianza.", true)),
  /* 3.11 */ off(vf("Las fuentes generadas por IA siempre son reales.", false)),
  /* 3.12 */ off(vf("La verificación humana es obligatoria.", true)),
  /* 3.13 */ off(vf("Las alucinaciones solo ocurren en herramientas gratuitas.", false)),
  /* 3.14 */ off(vf("La IA no entiende el contenido: solo predice patrones.", true)),
];

export const m4 = [
  /* 4.1 */ mc("¿Qué es material protegido?", ["Contenido público","Obras con derechos de autor","Información irrelevante","Contenido sin dueño"], "b", 1),
  /* 4.2 */ off(mc("¿Qué riesgo tiene el código generado por IA?", ["Siempre es seguro","Puede incluir código con licencias restrictivas","No requiere revisión","No puede usarse comercialmente"], "b", 1)),
  /* 4.3 */ off(mc("¿Qué riesgo existe con imágenes generadas?", ["No pueden parecerse a nada","Pueden imitar obras protegidas","Siempre son libres de derechos","No pueden usarse en redes sociales"], "b", 1)),
  /* 4.4 */ off(mc("¿Qué implica el uso comercial?", ["Libertad total","Restricciones según la herramienta","No requiere revisión legal","No tiene riesgos"], "b", 1)),
  /* 4.5 */ off(mc("¿Qué puede causar una violación de propiedad intelectual?", ["Aumento de productividad","Demandas y sanciones","Mejora de reputación","Reducción de riesgos"], "b", 1)),
  /* 4.6 */ off(ms("¿Qué puede generar riesgos legales?", ["Código generado por IA","Imágenes generadas","Uso comercial sin revisión","Ideas generales"], ["a","b","c"], 1)),
  /* 4.7 */ off(ms("¿Qué prácticas ayudan a evitar problemas legales?", ["Revisar licencias","Consultar al equipo legal","Usar contenido sin verificar","Validar derechos de uso"], ["a","b","d"], 1)),
  /* 4.8 */ mc("En el caso del volante de la promoción, ¿qué riesgo existe?", ["Ninguno","Violación de derechos de autor","Mejora de creatividad","Uso permitido automáticamente"], "b", 1),
  /* 4.9 */ off(mc("En el caso del volante de la promoción, ¿qué debió hacer el empleado?", ["Usar la imagen sin revisar","Consultar si el estilo está protegido","Publicarla inmediatamente","Ignorar riesgos legales"], "b", 1)),
  /* 4.10 */ off(vf("La IA puede generar contenido similar a obras protegidas.", true)),
  /* 4.11 */ off(vf("El código generado por IA siempre es seguro legalmente.", false)),
  /* 4.12 */ off(vf("El uso comercial requiere revisión legal.", true)),
  /* 4.13 */ off(vf("Las imágenes generadas nunca imitan estilos protegidos.", false)),
  /* 4.14 */ vf("El usuario es responsable del contenido generado por IA.", true),
];

export const m5 = [
  /* 5.1 */ off(mc("¿Qué es un sesgo en IA?", ["Un error técnico","Un patrón injusto aprendido de los datos","Una falla de hardware","Un problema de conexión"], "b", 1)),
  /* 5.2 */ off(mc("¿Qué es discriminación algorítmica?", ["Una decisión justa","Una decisión injusta basada en patrones sesgados","Un proceso manual","Un error de formato"], "b", 1)),
  /* 5.3 */ mc("¿Qué rol tiene la supervisión humana?", ["Ninguno","Validar y corregir resultados de IA","Dejar que la IA decida","Ignorar recomendaciones"], "b", 1),
  /* 5.4 */ mc("¿Quién tiene la responsabilidad final?", ["La IA","El empleado que la usa","El servidor","El modelo de lenguaje"], "b", 1),
  /* 5.5 */ off(mc("¿Qué puede causar un sesgo algorítmico?", ["Datos históricos injustos","Información equilibrada","Reglas éticas","Supervisión humana"], "a", 1)),
  /* 5.6 */ off(ms("¿Qué decisiones NO deben delegarse a la IA?", ["Contratación","Evaluación de desempeño","Aprobación de préstamos","Generación de ideas creativas"], ["a","b","c"], 1)),
  /* 5.7 */ off(ms("¿Qué acciones forman parte de la supervisión humana?", ["Verificar información","Evaluar impacto","Tomar decisiones finales","Dejar que la IA decida sola"], ["a","b","c"], 1)),
  /* 5.8 */ mc("En el caso del candidato rechazado, ¿qué ocurrió?", ["Un proceso correcto","Discriminación algorítmica","Un error administrativo","Un caso sin impacto"], "b", 1),
  /* 5.9 */ mc("En el caso del candidato rechazado, ¿qué debió hacer el reclutador?", ["Aceptar la recomendación sin revisar","Evaluar al candidato con criterios humanos","Dejar que la IA tome la decisión","Ignorar el proceso"], "b", 1),
  /* 5.10 */ off(vf("La IA puede discriminar sin intención.", true)),
  /* 5.11 */ off(vf("La supervisión humana es opcional.", false)),
  /* 5.12 */ off(vf("La responsabilidad final siempre es del empleado.", true)),
  /* 5.13 */ off(vf("La IA entiende ética y justicia.", false)),
  /* 5.14 */ off(vf("Los sesgos pueden estar presentes en los datos de entrenamiento.", true)),
];

export const m6 = [
  /* 6.1 */ mc("¿Qué tipo de información puede compartirse con IA?", ["Información pública","Estados financieros internos","Datos de clientes","Código fuente real"], "a", 1),
  /* 6.2 */ off(mc("¿Qué información nunca debe compartirse?", ["Conceptos generales","Información confidencial","Ideas creativas","Ejemplos ficticios"], "b", 1)),
  /* 6.3 */ mc("¿Qué es un prompt seguro?", ["Uno que incluye datos internos","Uno que no contiene información sensible","Uno que pega contratos reales","Uno que expone código interno"], "b", 1),
  /* 6.4 */ off(mc("¿Qué debe hacerse con contenido generado por IA?", ["Usarlo sin revisar","Revisarlo antes de usarlo","Publicarlo inmediatamente","Compartirlo con clientes"], "b", 1)),
  /* 6.5 */ off(mc("¿Qué requiere aprobación?", ["Ideas creativas","Contenido legal o financiero","Preguntas generales","Ejemplos ficticios"], "b", 1)),
  /* 6.6 */ off(ms("¿Qué información NO debe compartirse con IA?", ["Contratos","Código fuente","Datos de clientes","Conceptos generales"], ["a","b","c"], 1)),
  /* 6.7 */ off(ms("¿Qué acciones forman parte del uso responsable?", ["Revisar contenido","Usar prompts seguros","Compartir datos internos","Solicitar aprobaciones cuando aplique"], ["a","b","d"], 1)),
  /* 6.8 */ mc("En el caso del archivo de clientes, ¿qué ocurrió?", ["Un uso correcto","Una violación grave de seguridad","Un análisis permitido","Un proceso normal"], "b", 1),
  /* 6.9 */ mc("En el caso del archivo de clientes, ¿qué debió hacer el empleado?", ["Usar una herramienta aprobada por la empresa","Compartir más datos para mejorar la respuesta","Publicar el análisis","Ignorar políticas"], "a", 1),
  /* 6.10 */ off(vf("Los prompts deben evitar información sensible.", true)),
  /* 6.11 */ off(vf("Todo contenido generado por IA debe revisarse.", true)),
  /* 6.12 */ off(vf("La IA puede recibir estados financieros internos si es para análisis.", false)),
  /* 6.13 */ off(vf("Algunas tareas requieren aprobación previa.", true)),
  /* 6.14 */ off(vf("La IA puede reemplazar el juicio humano.", false)),
];

export const m7 = [
  /* 7.1 */ off(mc("¿Qué ocurrió en el caso de la base de datos?", ["Uso correcto","Filtración de datos personales","Mejora de productividad","Proceso autorizado"], "b", 1)),
  /* 7.2 */ off(mc("¿Qué riesgo existe al generar contratos con IA pública?", ["Ninguno","Exposición de información legal interna","Aumento de eficiencia","Mejora de redacción"], "b", 1)),
  /* 7.3 */ off(mc("¿Qué representa pegar código fuente en IA pública?", ["Una práctica recomendada","Exposición de propiedad intelectual","Un proceso seguro","Un análisis técnico válido"], "b", 1)),
  /* 7.4 */ off(mc("¿Qué riesgo existe al compartir estados financieros internos?", ["Ninguno","Violación regulatoria","Mejora de análisis","Aumento de ventas"], "b", 1)),
  /* 7.5 */ off(mc("¿Qué ocurrió en el caso de servicio al cliente?", ["Uso correcto","Violación de privacidad","Mejora de comunicación","Proceso autorizado"], "b", 1)),
  /* 7.6 */ off(ms("¿Qué casos representan violaciones graves?", ["Pegar datos de clientes","Pegar contratos internos","Pegar código fuente","Pedir ideas creativas"], ["a","b","c"], 1)),
  /* 7.7 */ off(ms("¿Qué acciones debieron tomarse?", ["Usar herramientas aprobadas","Anonimizar datos","Compartir más información","Consultar a legal o cumplimiento"], ["a","b","d"], 1)),
  /* 7.8 */ off(mc("En el caso del reporte financiero, ¿qué riesgo ocurrió?", ["Ninguno","Filtración de información estratégica","Mejora de productividad","Proceso autorizado"], "b", 1)),
  /* 7.9 */ off(mc("En el caso del reporte financiero, ¿qué debió hacer el empleado?", ["Usar datos públicos","Compartir más información","Publicar el resumen","Ignorar políticas"], "a", 1)),
  /* 7.10 */ off(vf("Pegar código fuente en IA pública es seguro.", false)),
  /* 7.11 */ off(vf("Los datos de clientes nunca deben compartirse.", true)),
  /* 7.12 */ off(vf("Los estados financieros internos son información sensible.", true)),
  /* 7.13 */ off(vf("Los contratos pueden pegarse si son cortos.", false)),
  /* 7.14 */ off(vf("Los casos prácticos muestran riesgos reales.", true)),
];

export const m8 = [
  /* 8.1 */ mc("¿Qué es uso permitido?", ["Compartir datos internos","Usar IA con información pública","Pegar contratos","Compartir código fuente"], "b", 1),
  /* 8.2 */ mc("¿Qué está prohibido?", ["Ideas creativas","Información confidencial","Conceptos generales","Ejemplos ficticios"], "b", 1),
  /* 8.3 */ mc("¿Qué define una herramienta autorizada?", ["Que sea popular","Que cumpla con seguridad y privacidad","Que sea gratuita","Que no requiera aprobación"], "b", 1),
  /* 8.4 */ off(mc("¿Qué puede causar una violación de política?", ["Aumento de productividad","Consecuencias disciplinarias","Mejora de procesos","Innovación"], "b", 1)),
  /* 8.5 */ off(mc("¿Qué deben hacer los empleados?", ["Ignorar la política","Aceptarla y cumplirla","Usar cualquier IA","Compartir información interna"], "b", 1)),
  /* 8.6 */ off(ms("¿Qué acciones están prohibidas?", ["Compartir contratos","Compartir datos de clientes","Compartir código fuente","Pedir explicaciones conceptuales"], ["a","b","c"], 1)),
  /* 8.7 */ off(ms("¿Qué herramientas pueden usarse?", ["Herramientas aprobadas","IA corporativa","IA pública sin contrato","Sistemas internos autorizados"], ["a","b","d"], 1)),
  /* 8.8 */ mc("En el caso del contrato en una IA pública, ¿qué ocurrió?", ["Uso permitido","Violación de política","Proceso autorizado","Mejora de productividad"], "b", 1),
  /* 8.9 */ off(mc("En el caso del contrato en una IA pública, ¿qué debió hacer el empleado?", ["Usar plantillas internas","Compartir más información","Publicar el contrato","Ignorar la política"], "a", 1)),
  /* 8.10 */ off(vf("Las herramientas públicas de IA están prohibidas si no hay contrato corporativo.", true)),
  /* 8.11 */ off(vf("Los empleados deben aceptar la política de IA.", true)),
  /* 8.12 */ off(vf("Compartir estados financieros internos está permitido.", false)),
  /* 8.13 */ off(vf("La empresa puede disciplinar a empleados por mal uso de IA.", true)),
  /* 8.14 */ off(vf("La política de IA protege a la empresa y a los empleados.", true)),
];

