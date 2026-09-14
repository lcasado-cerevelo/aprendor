// ============================================================================
// Curso: "Hostigamiento Sexual en el Empleo"
//
// Transcripción nativa del deck "Adiestramiento Plataforma - Hostigamiento
// Sexual.pptx", basado en la Ley Núm. 17 de 1988 (Ley para Prohibir el
// Hostigamiento Sexual en el Empleo) de Puerto Rico.
//
// 5 módulos. El último cierra con las 7 preguntas de evaluación (10 puntos c/u).
// El umbral de aprobación de la versión es 70% (TrainingVersion.PassPercent).
// ============================================================================

import m0 from './m0-apertura.mjs';
import m1 from './m1-que-es-hostigamiento.mjs';
import m2 from './m2-responsabilidades-y-reporte.mjs';
import m3 from './m3-proceso-y-proteccion.mjs';
import m4 from './m4-evaluacion.mjs';

export default {
  training: {
    title: 'Hostigamiento Sexual en el Empleo',
    description:
      'Adiestramiento sobre hostigamiento sexual bajo la Ley Núm. 17 de Puerto Rico: qué lo constituye, sus ' +
      'formas (quid pro quo y ambiente hostil), responsabilidades de empleados y supervisores, el proceso de ' +
      'investigación de la empresa y la prohibición de represalias.',
    category: 'Cumplimiento',
    recurrenceMonths: 12,
    renewLeadDays: 30,
    certificate: {
      enabled: true,
      issuerName: 'Advance Logistics',
      signatoryName: 'Recursos Humanos',
      signatoryTitle: 'Advance Logistics',
      statement:
        'Certifica haber completado el adiestramiento sobre Hostigamiento Sexual en el Empleo bajo la Ley ' +
        'Núm. 17 de Puerto Rico.',
      showScore: true,
      showValidity: true,
      accentColor: '#a21caf',
    },
  },

  items: [...m0, ...m1, ...m2, ...m3, ...m4],
};
