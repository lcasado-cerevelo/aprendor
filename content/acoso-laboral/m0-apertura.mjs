import { info, T, resumen, steps } from '../_shared/authoring.mjs';

const ICONO = `<svg viewBox="0 0 64 64" width="84" height="84" fill="none" stroke="#4338ca" stroke-width="3" ` +
  `stroke-linecap="round" stroke-linejoin="round" style="display:block;margin:0 auto 14px">` +
  `<circle cx="24" cy="22" r="7"/><circle cx="44" cy="22" r="7"/>` +
  `<path d="M12 52c0-8 6-14 12-14s12 6 12 14M32 52c0-8 6-14 12-14s12 6 12 14"/></svg>`;

export default [
  info('Acoso Laboral en el Empleo',
    T(`${ICONO}
       <p style="text-align:center;font-size:15px;color:#475569">Qué es el acoso laboral bajo la Ley Núm.
       90-2020, qué conductas lo constituyen (y cuáles no) y qué debe hacer un empleado que lo enfrenta o lo
       presencia.</p>
       ${resumen({ modulos: 4, tipos: 'Selección múltiple y cierto/falso', puntos: 60, minutos: 20 })}`)),

  info('Instrucciones',
    T(`<p>Bienvenido(a) al adiestramiento virtual sobre Acoso Laboral. Para completar satisfactoriamente este
       adiestramiento:</p>
       ${steps([
         'Lea detenidamente todo el contenido presentado en cada sección.',
         'Procure comprender la información, incluyendo los conceptos, ejemplos, responsabilidades y procedimientos discutidos.',
         'Una vez haya completado la presentación, responda las preguntas de evaluación al final del adiestramiento.',
         'Seleccione la respuesta correcta de acuerdo con el contenido presentado.',
       ], '#4338ca')}
       <p><b>Importante:</b> la evaluación está diseñada para validar la comprensión del contenido presentado.</p>`)),

  info('Objetivo',
    T(`<p>Se persigue que los empleados puedan reconocer conductas inapropiadas, canalizar las situaciones que
       ocurran y comprender los procedimientos establecidos para el manejo de situaciones de acoso laboral.</p>
       <p>A través de sus prácticas y políticas, la empresa promueve el respeto, la cordialidad y las
       relaciones de trabajo saludables.</p>`)),
];
