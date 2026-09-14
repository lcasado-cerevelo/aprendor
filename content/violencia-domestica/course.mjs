// ============================================================================
// Curso: "Protocolo de Manejo de Situaciones de Violencia Doméstica en el Empleo"
//
// Transcripción nativa del deck "Adiestramiento Plataforma - Violencia
// Domestica.pptx". Basado en la Ley Núm. 54-1989 (Ley para la Prevención e
// Intervención con la Violencia Doméstica — querellas y órdenes de protección),
// la Ley Núm. 217-2006 (exige a los patronos adoptar un protocolo de manejo de
// estas situaciones en el lugar de trabajo, supervisado por PR-OSHA) y la Ley
// Núm. 83-2019 (licencia especial de hasta 15 días para el empleado afectado).
//
// 4 módulos. El último cierra con las 6 preguntas de evaluación (10 puntos c/u).
// El umbral de aprobación de la versión es 70% (TrainingVersion.PassPercent).
// ============================================================================

import m0 from './m0-apertura.mjs';
import m1 from './m1-que-es-violencia-domestica.mjs';
import m2 from './m2-protocolo-en-el-empleo.mjs';
import m3 from './m3-cierre-y-evaluacion.mjs';

export default {
  training: {
    title: 'Protocolo de Manejo de Situaciones de Violencia Doméstica en el Empleo',
    description:
      'Adiestramiento sobre el protocolo de la empresa para el manejo de situaciones de violencia doméstica ' +
      'en el lugar de trabajo: qué es la violencia doméstica, cómo reportar una situación y las medidas de ' +
      'apoyo y licencia especial disponibles para un empleado afectado.',
    category: 'Cumplimiento',
    recurrenceMonths: 12,
    renewLeadDays: 30,
    certificate: {
      enabled: true,
      issuerName: 'Advance Logistics',
      signatoryName: 'Recursos Humanos',
      signatoryTitle: 'Advance Logistics',
      statement:
        'Certifica haber completado el adiestramiento sobre el Protocolo de Manejo de Situaciones de ' +
        'Violencia Doméstica en el Empleo.',
      showScore: true,
      showValidity: true,
      accentColor: '#9f1239',
    },
  },

  items: [...m0, ...m1, ...m2, ...m3],
};
