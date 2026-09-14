import { info, T, resumen, steps } from '../_shared/authoring.mjs';

const ICONO = `<svg viewBox="0 0 64 64" width="84" height="84" fill="none" stroke="#9f1239" stroke-width="3" ` +
  `stroke-linecap="round" stroke-linejoin="round" style="display:block;margin:0 auto 14px">` +
  `<path d="M32 8c-9 6-16 6-20 6v14c0 13 8 22 20 26 12-4 20-13 20-26V14c-4 0-11 0-20-6z"/>` +
  `<path d="M24 34l6 6 12-12"/></svg>`;

export default [
  info('Protocolo de Manejo de Situaciones de Violencia Doméstica en el Empleo',
    T(`${ICONO}
       <p style="text-align:center;font-size:15px;color:#475569">Qué es la violencia doméstica, cómo reconocerla
       y el protocolo de la empresa para apoyar a un empleado que la enfrenta, incluyendo cómo reportar una
       situación.</p>
       ${resumen({ modulos: 4, tipos: 'Selección múltiple', puntos: 60, minutos: 20 })}`)),

  info('Instrucciones',
    T(`<p>Bienvenido(a) al adiestramiento virtual sobre el Protocolo de Manejo de Situaciones de Violencia
       Doméstica en el Empleo. Para completar satisfactoriamente este adiestramiento:</p>
       ${steps([
         'Lea detenidamente todo el contenido presentado en cada sección.',
         'Procure comprender la información, incluyendo los conceptos, ejemplos, responsabilidades y procedimientos discutidos.',
         'Una vez haya completado la presentación, responda las preguntas de evaluación al final del adiestramiento.',
         'Seleccione la respuesta correcta de acuerdo con el contenido presentado.',
       ], '#9f1239')}
       <p><b>Importante:</b> la evaluación está diseñada para validar la comprensión del contenido presentado.</p>`)),

  info('Objetivo',
    T(`<p>Proveer a los empleados los conocimientos necesarios para conocer el Protocolo establecido por la
       empresa para el manejo de situaciones de violencia doméstica en el lugar de trabajo, incluyendo el
       proceso para reportar una situación.</p>`)),
];
