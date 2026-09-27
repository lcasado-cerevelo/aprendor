// ============================================================================
// Curso: "Acoso Laboral en el Empleo"
//
// Transcripción nativa del deck "Adiestramiento Plataforma - Acoso Laboral.pptx",
// basado en la Ley Núm. 90-2020 de Puerto Rico (Ley para Prohibir el Hostigamiento
// Laboral en Puerto Rico).
//
// 17 láminas en el orden del deck (1 portada; 2-3 instrucciones y objetivo; 4-11
// contenido; 12-17 las 6 preguntas de 10 puntos, dos de cierto o falso) más una lámina
// final de recursos adicionales. Antes de la portada va la pantalla de entrada (intro():
// resumen del curso y tiempo estimado), que no cuenta como lámina. Se toma en modo
// presentación (PlayerConfig.presentation), con el mismo estilo que Hostigamiento Sexual.
// El umbral de aprobación de la versión es 70% (TrainingVersion.PassPercent).
// ============================================================================

import m0 from './m0-apertura.mjs';
import m1 from './m1-que-es-acoso-laboral.mjs';
import m2 from './m2-que-hacer-y-prevencion.mjs';
import m3 from './m3-evaluacion.mjs';

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
    // Modo presentación con el tema del deck: fondo negro, panel diagonal con el título en
    // mayúsculas y acento azul (las líneas del deck son azul grisáceo).
    playerConfig: {
      allowBack: true,
      presentation: {
        enabled: true,
        theme: { bg: '#0d0d0d', accent: '#60a5fa', panel: true, panelTitle: 'ACOSO LABORAL EN EL EMPLEO' },
        transition: 'cover',
      },
    },
  },

  items: [...m0, ...m1, ...m2, ...m3],
};
