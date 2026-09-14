import { info, T, resumen, steps, badgeList } from '../_shared/authoring.mjs';

// Apertura. Mismo arranque que el resto de los cursos del catálogo: portada con
// ícono, descripción y caja de resumen (con duración estimada), seguida de las
// Instrucciones y el Objetivo tal como los presenta el material original.

const ICONO = `<svg viewBox="0 0 64 64" width="84" height="84" fill="none" stroke="#1d4ed8" stroke-width="3" ` +
  `stroke-linecap="round" stroke-linejoin="round" style="display:block;margin:0 auto 14px">` +
  `<path d="M32 6l20 8v14c0 13-8.5 22-20 26C20.5 50 12 41 12 28V14z"/>` +
  `<path d="M32 22v16M24 30h16"/></svg>`;

export default [
  info('Cumplimiento con la Ley HIPAA',
    T(`${ICONO}
       <p style="text-align:center;font-size:15px;color:#475569">Qué es la información de salud protegida, por qué
       HIPAA le aplica a la empresa y cómo protegerla al manejar documentos, sistemas y entregas.</p>
       ${resumen({ modulos: 5, tipos: 'Selección múltiple', puntos: 60, minutos: 20 })}`)),

  info('Instrucciones',
    T(`<p>Bienvenido(a) al adiestramiento virtual sobre Cumplimiento con la Ley HIPAA. Para completar
       satisfactoriamente este adiestramiento:</p>
       ${steps([
         'Lea detenidamente todo el contenido presentado en cada sección.',
         'Procure comprender la información, incluyendo los conceptos, ejemplos, responsabilidades y procedimientos discutidos.',
         'Una vez haya completado la presentación, responda las preguntas de evaluación al final del adiestramiento.',
         'Seleccione la respuesta correcta de acuerdo con el contenido presentado.',
       ])}
       <p><b>Importante:</b> la evaluación está diseñada para validar la comprensión del contenido presentado.</p>`)),

  info('Objetivo',
    T(`<p>Se persigue proveer los principios fundamentales de la Ley HIPAA, relacionados con la privacidad y
       seguridad de la información de salud protegida, para proteger la confidencialidad de la información de
       los pacientes durante el manejo, transporte y entrega de medicamentos, equipos médicos o materiales de
       salud.</p>
       <p>Debido a la naturaleza de las operaciones del negocio, todos los empleados pueden estar expuestos de
       manera directa o indirecta a información de salud protegida. A tales efectos, este adiestramiento
       permitirá:</p>
       ${badgeList([
         'Reconocer qué información se considera protegida bajo HIPAA.',
         'Comprender sus responsabilidades en la protección de la información de los pacientes.',
         'Manejar adecuadamente los documentos, órdenes y entregas.',
         'Evitar situaciones que puedan representar una violación de privacidad o seguridad.',
         'Conocer cómo reportar oportunamente una posible violación de HIPAA.',
       ])}`)),
];
