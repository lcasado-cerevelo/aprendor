// ============================================================================
// Curso: "Hostigamiento Sexual en el Empleo"
//
// Transcripción nativa del deck "Adiestramiento Plataforma - Hostigamiento
// Sexual.pptx", basado en la Ley Núm. 17 de 1988 (Ley para Prohibir el
// Hostigamiento Sexual en el Empleo) de Puerto Rico.
//
// 21 láminas en el orden del deck (1 portada; 2-3 instrucciones y objetivo; 4-13
// contenido; 14-20 las 7 preguntas de 10 puntos) más una lámina final de recursos
// adicionales. Antes de la portada va la pantalla de entrada (intro(): resumen del curso
// y tiempo estimado), que no cuenta como lámina. Se toma en modo presentación (PlayerConfig.presentation): cada
// lámina se dibuja en un escenario 16:9 con el estilo del deck.
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
    // Opciones del reproductor (PUT /trainings/{id}/player-config). El bloque
    // `presentation` enciende el modo presentación con el tema del deck: fondo negro,
    // acento naranja y el panel diagonal con el título en mayúsculas.
    playerConfig: {
      allowBack: true,
      presentation: {
        enabled: true,
        theme: { bg: '#0d0d0d', accent: '#f97316', panel: true, panelTitle: 'HOSTIGAMIENTO SEXUAL EN EL EMPLEO' },
        transition: 'cover',
      },
    },
  },

  items: [...m0, ...m1, ...m2, ...m3, ...m4],
};
