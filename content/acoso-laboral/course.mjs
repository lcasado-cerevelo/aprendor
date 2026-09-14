// ============================================================================
// Curso: "Acoso Laboral en el Empleo"
//
// Transcripción nativa del deck "Adiestramiento Plataforma - Acoso Laboral.pptx",
// basado en la Ley Núm. 90-2020 de Puerto Rico (Ley para Prohibir el Hostigamiento
// Laboral en Puerto Rico).
//
// 4 módulos. El último cierra con las 6 preguntas de evaluación (10 puntos c/u,
// incluyendo 2 de cierto/falso). El umbral de aprobación de la versión es 70%
// (TrainingVersion.PassPercent).
// ============================================================================

import m0 from './m0-apertura.mjs';
import m1 from './m1-que-es-acoso-laboral.mjs';
import m2 from './m2-limites-y-consecuencias.mjs';
import m3 from './m3-tu-responsabilidad-y-evaluacion.mjs';

export default {
  training: {
    title: 'Acoso Laboral en el Empleo',
    description:
      'Adiestramiento sobre acoso laboral bajo la Ley Núm. 90-2020 de Puerto Rico: qué conductas lo ' +
      'constituyen, cuáles quedan fuera de la definición, y el procedimiento para reportar y manejar una ' +
      'situación en el empleo.',
    category: 'Cumplimiento',
    recurrenceMonths: 12,
    renewLeadDays: 30,
    certificate: {
      enabled: true,
      issuerName: 'Advance Logistics',
      signatoryName: 'Recursos Humanos',
      signatoryTitle: 'Advance Logistics',
      statement:
        'Certifica haber completado el adiestramiento sobre Acoso Laboral bajo la Ley Núm. 90-2020 de ' +
        'Puerto Rico.',
      showScore: true,
      showValidity: true,
      accentColor: '#4338ca',
    },
  },

  items: [...m0, ...m1, ...m2, ...m3],
};
