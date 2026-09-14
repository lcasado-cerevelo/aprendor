import { info, T, resumen, steps } from '../_shared/authoring.mjs';

const ICONO = `<svg viewBox="0 0 64 64" width="84" height="84" fill="none" stroke="#a21caf" stroke-width="3" ` +
  `stroke-linecap="round" stroke-linejoin="round" style="display:block;margin:0 auto 14px">` +
  `<path d="M32 6l20 8v14c0 13-8.5 22-20 26C20.5 50 12 41 12 28V14z"/>` +
  `<path d="M24 32l6 6 10-12"/></svg>`;

export default [
  info('Hostigamiento Sexual en el Empleo',
    T(`${ICONO}
       <p style="text-align:center;font-size:15px;color:#475569">Qué es el hostigamiento sexual bajo la Ley
       Núm. 17, cómo se manifiesta, y cómo reportarlo y prevenirlo en el lugar de trabajo.</p>
       ${resumen({ modulos: 5, tipos: 'Selección múltiple', puntos: 70, minutos: 25 })}`)),

  info('Instrucciones',
    T(`<p>Bienvenido(a) al adiestramiento virtual sobre Hostigamiento Sexual en el Empleo. Para completar
       satisfactoriamente este adiestramiento:</p>
       ${steps([
         'Lea detenidamente todo el contenido presentado en cada sección.',
         'Procure comprender la información, incluyendo los conceptos, ejemplos, responsabilidades y procedimientos discutidos.',
         'Una vez haya completado la presentación, responda las preguntas de evaluación al final del adiestramiento.',
         'Seleccione la respuesta correcta de acuerdo con el contenido presentado.',
       ], '#a21caf')}
       <p><b>Importante:</b> la evaluación está diseñada para validar la comprensión del contenido presentado.</p>`)),

  info('Objetivo',
    T(`<p>Este adiestramiento persigue proveer a los empleados los conocimientos necesarios para identificar,
       prevenir y reportar situaciones de hostigamiento sexual en el lugar de trabajo, conforme a la Política
       de Hostigamiento Sexual en el Empleo establecida en el Manual del Empleado.</p>
       <p>La empresa está comprometida en brindar y garantizarle a todos los empleados un entorno seguro y
       saludable, lejos de comportamientos que alteren este predicamento.</p>`)),
];
