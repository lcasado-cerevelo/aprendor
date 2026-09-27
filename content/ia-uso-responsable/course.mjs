// ============================================================================
// Curso: "Inteligencia Artificial: Uso Seguro, Ético y Responsable" — Adiestramiento
//
// Recorte del curso que estaba en la base de producción (versión 1, borrador; el original
// completo está en original-produccion.json). Cambios aprobados pantalla por pantalla en
// https://claude.ai/artifact/Lq5gRVGuks3KSVYgdk7amP: de 75 a 35 pantallas y de 112 a 27
// preguntas activas, unos 25–30 minutos. El módulo 7 (Casos prácticos) se repartió entre
// el 2 y el 6. Las 112 preguntas originales se suben (preguntas.mjs): 27 activas y 85
// DESACTIVADAS, para cambiar el subconjunto desde la app sin perder el banco.
//
// Modo presentación en tema claro (blanco y verde), el estilo de los cursos de
// Adiestramiento. Cada pregunta vale 1 punto; se aprueba con 70 %.
//
// PENDIENTE DE LA EMPRESA: si hay una política de IA escrita (herramientas autorizadas y
// sanciones, m8) y qué leyes se citan (m2). Mientras tanto, texto neutro.
// ============================================================================

import { m0, m1, m2 } from './m0-m2.mjs';
import { m3, m4, m5 } from './m3-m5.mjs';
import { m6, m8 } from './m6-m8.mjs';

export default {
  training: {
    title: 'Inteligencia Artificial: Uso Seguro, Ético y Responsable',
    description:
      'Uso seguro de la inteligencia artificial en el trabajo: qué información nunca se comparte, cómo reconocer ' +
      'información inventada, propiedad intelectual, decisiones con supervisión humana y las reglas de la empresa.',
    category: 'Adiestramiento',
    recurrenceMonths: 12,
    renewLeadDays: 30,
    voice: 'es-PR-KarinaNeural',
    certificate: {
      enabled: true,
      issuerName: 'Advance Logistics',
      signatoryName: 'Recursos Humanos',
      signatoryTitle: 'Advance Logistics',
      statement: 'Certifica haber completado el adiestramiento Inteligencia Artificial: Uso Seguro, Ético y Responsable.',
      showScore: true,
      showValidity: true,
      accentColor: '#047857',
    },
    playerConfig: {
      allowBack: true,
      presentation: {
        enabled: true,
        theme: { mode: 'light', bg: '#ffffff', accent: '#059669', panel: true, panelTitle: 'INTELIGENCIA ARTIFICIAL' },
        transition: 'cover',
      },
    },
  },

  items: [...m0, ...m1, ...m2, ...m3, ...m4, ...m5, ...m6, ...m8],
};
