// ============================================================================
// Curso: "Cumplimiento con la Ley HIPAA" — versión estándar de plataforma
//
// Reemplaza el curso anterior "Cumplimiento HIPAA para transporte y logística"
// (content/hipaa-advance-logistics, retirado). Transcripción nativa del deck
// "Adiestramiento Plataforma - Ley HIPAA.pptx".
//
// 18 láminas en el orden del deck (1 portada; 2-3 instrucciones y objetivo; 4-12
// contenido; 13-18 las 6 preguntas de 10 puntos) más una lámina final de recursos
// adicionales. Antes de la portada va la pantalla de entrada (intro()), que no cuenta
// como lámina. Se toma en modo presentación (PlayerConfig.presentation), con el mismo
// estilo que Hostigamiento Sexual.
// El umbral de aprobación de la versión es 70% (TrainingVersion.PassPercent).
// ============================================================================

import m0 from './m0-apertura.mjs';
import m1 from './m1-informacion-protegida.mjs';
import m2 from './m2-practicas-diarias.mjs';
import m3 from './m3-incidentes-y-responsabilidad.mjs';
import m4 from './m4-evaluacion.mjs';

export default {
  training: {
    title: 'Cumplimiento con la Ley HIPAA',
    description:
      'Adiestramiento de cumplimiento con la Ley HIPAA para el personal que maneja, transporta o entrega ' +
      'medicamentos, equipos o materiales de salud: qué es información de salud protegida (PHI), cómo ' +
      'protegerla en el día a día y cómo reportar un posible incidente.',
    category: 'Cumplimiento',
    recurrenceMonths: 12,
    renewLeadDays: 30,
    certificate: {
      enabled: true,
      issuerName: 'Advance Logistics',
      signatoryName: 'Recursos Humanos',
      signatoryTitle: 'Advance Logistics',
      statement:
        'Certifica haber completado el adiestramiento de cumplimiento con la Ley de Portabilidad y ' +
        'Responsabilidad del Seguro Médico (HIPAA), 45 CFR Partes 160 y 164.',
      showScore: true,
      showValidity: true,
      accentColor: '#1d4ed8',
    },
    // Modo presentación: fondo negro, panel diagonal con el título en mayúsculas y acento
    // verde azulado (tono de salud).
    playerConfig: {
      allowBack: true,
      presentation: {
        enabled: true,
        theme: { bg: '#0d0d0d', accent: '#2dd4bf', panel: true, panelTitle: 'CUMPLIMIENTO CON LA LEY HIPAA' },
        transition: 'cover',
      },
    },
  },

  items: [...m0, ...m1, ...m2, ...m3, ...m4],
};
