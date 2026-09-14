import { info, T, S, fig, steps, badgeList, resources } from '../_shared/authoring.mjs';

export default [
  info('¿Qué hará la empresa?',
    T(`<p>En cumplimiento con la Política de Hostigamiento Sexual:</p>
       ${steps([
         'El empleado no tiene que realizar una querella escrita; puede presentarla de manera verbal.',
         'Se comenzará con un proceso de investigación, aun cuando el empleado(a) indique que no interesa que se proceda con el mismo.',
         'Se entrevistará a todos los posibles testigos identificados por ambas partes (víctima y hostigador).',
         'De acuerdo con la información obtenida se llegará a una conclusión.',
         'Se tomarán medidas disciplinarias, que pueden incluir el despido.',
         'Se tomarán medidas que garanticen un ambiente saludable.',
       ], '#a21caf')}`)),

  info('¿Qué debe saber todo empleado?',
    T(`${badgeList([
         'El patrono tiene cero tolerancia a las conductas de índole sexual',
         'Todo empleado que observe una conducta de índole sexual no deseada tiene la responsabilidad de informarlo de manera inmediata a su supervisor',
         'El proceso de radicación de querella y de investigación se llevará a cabo de manera confidencial',
       ], '#a21caf')}`)),

  info('Prohibición de represalias',
    T(`<p>La empresa prohíbe represalias contra una persona que:</p>
       ${badgeList(['Presenta una queja', 'Reporta una situación', 'Participa en una investigación', 'Provee información o sirve como testigo'], '#dc2626', '!')}
       <div style="${S.warn}">Cualquier alegación de represalia debe ser reportada inmediatamente.</div>`)),

  info('Recomendaciones',
    T(`${fig(import.meta.url, './img/empoderamiento.jpg',
        'Un puño en alto contra la luz del atardecer, en señal de determinación',
        'Foto: Visuals / Unsplash')}
       <ul>
         <li>Trate con respeto a sus compañeros y clientes</li>
         <li>No tolere conductas ofensivas en ningún momento</li>
         <li>No sienta temor en notificar una situación</li>
         <li>Acérquese a cualquier supervisor</li>
       </ul>
       ${resources([
         ['Ley Núm. 17 de 1988 — texto oficial (Departamento del Trabajo de PR)', 'https://www.trabajo.pr.gov/docs/Unidad_Antidiscrimen/Ley_17_Hostigamiento_Sexual_Trabajo.pdf'],
         ['Guías para la Prevención y el Manejo del Hostigamiento Sexual en el Empleo — Oficina de la Procuradora de las Mujeres', 'https://docs.pr.gov/files/Mujer/Leyes/Gu%C3%ADas%20para%20la%20Prevenci%C3%B3n%20y%20el%20Manejo%20del%20Hostigamiento%20Sexual%20en%20el%20Empleo.pdf'],
         ['¿Qué es hostigamiento sexual? — video, opcional', 'https://www.youtube.com/watch?v=nKD7T0CX3XA'],
       ])}`)),
];
