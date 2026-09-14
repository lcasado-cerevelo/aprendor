import { info, T, S, steps, badgeList, statGrid, resources } from '../_shared/authoring.mjs';

export default [
  info('¿Qué es una divulgación no autorizada?',
    T(`<p>Una divulgación no autorizada ocurre cuando información protegida se utiliza o se comparte sin
       autorización. Ejemplos:</p>
       ${badgeList([
         'Entregar documentos del paciente a la persona equivocada',
         'Hablar sobre el diagnóstico de un paciente frente a terceros',
         'Enviar información a un número de teléfono incorrecto',
         'Compartir información con un familiar que no está autorizado',
         'Publicar una fotografía de una orden médica',
         'Dejar documentos con información protegida en un lugar donde otras personas puedan verlos',
       ], '#dc2626', '!')}
       <p style="${S.muted}">Una divulgación puede ocurrir intencionalmente o por accidente.</p>`)),

  info('¿Qué hacer ante un incidente?',
    T(`<p>Si cree que ocurrió una posible violación de privacidad:</p>
       ${steps([
         'No lo oculte — informe la situación inmediatamente a la Administradora.',
         'Notifique al supervisor o a la persona designada por la empresa.',
         'Provea todos los detalles que tenga disponibles.',
         'No investigue o corrija el incidente por su cuenta.',
       ], '#dc2626')}
       <div style="${S.call}"><b>Importante:</b> reportar rápidamente un posible incidente permite que la
       empresa evalúe la situación.</div>`)),

  info('Responsabilidad del empleado',
    T(`<p>Cada empleado tiene una responsabilidad en la protección de la información de salud:</p>
       ${badgeList([
         'Utilizar correctamente la información',
         'Seguir las políticas y procedimientos de la empresa',
         'Proteger documentos y sistemas',
         'Evitar divulgaciones innecesarias',
         'Reportar inmediatamente posibles incidentes',
       ], '#16a34a')}
       <div style="${S.ok}">La privacidad del paciente es responsabilidad de todos.</div>
       ${statGrid([['45 CFR', 'Partes 160 y 164'], ['60 días', 'plazo máximo para notificar una brecha grande a HHS'], ['70%', 'para aprobar este curso']])}
       ${resources([
         ['HHS — Guías y adiestramiento sobre HIPAA (en inglés)', 'https://www.hhs.gov/hipaa/for-professionals/training/index.html'],
         ['¿Qué es la Ley HIPAA? Protección de la privacidad en la salud — video, opcional', 'https://www.youtube.com/shorts/2hYefuexVCA'],
       ])}`)),
];
