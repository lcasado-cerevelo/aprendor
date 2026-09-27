// ============================================================================
// Curso: "Seguridad de la Información para Empleados" — Adiestramiento
//
// Recorte del curso que estaba en la base de producción (versión 3, borrador; el original
// completo está en original-produccion.json). Cambios aprobados pantalla por pantalla en
// https://claude.ai/artifact/Lq5gRVGuks3KSVYgdk7amP: de 94 a 51 pantallas y de 40 a 32
// preguntas activas, unos 30–35 minutos. Las 8 preguntas abiertas se suben DESACTIVADAS
// (off()): quedan en el borrador pero el empleado no las ve.
//
// Modo presentación en tema claro (fondo blanco y verde): el estilo de los cursos de
// Adiestramiento; los de Cumplimiento van en negro. Cada pregunta vale 1 punto; se aprueba
// con 70 % (TrainingVersion.PassPercent).
//
// PENDIENTE DE LA EMPRESA (láminas con texto neutro hasta que conteste): canal real para
// reportar incidentes (m8), VPN y teléfono personal (m6), WhatsApp (m7), gestor de
// contraseñas (m3) y las leyes que se citan (m7).
// ============================================================================

import m0 from './m0-apertura.mjs';
import m1 from './m1-introduccion.mjs';
import m2 from './m2-uso-aceptable.mjs';
import m3 from './m3-contrasenas.mjs';
import m4 from './m4-phishing.mjs';
import m5 from './m5-malware.mjs';
import m6 from './m6-trabajo-remoto.mjs';
import m7 from './m7-informacion-sensible.mjs';
import m8 from './m8-incidentes.mjs';

export default {
  training: {
    title: 'Seguridad de la Información para Empleados',
    description:
      'Prácticas esenciales de seguridad de la información: cómo reconocer el phishing y el malware, proteger ' +
      'contraseñas y datos de la empresa y de los clientes, trabajar de forma segura fuera de la oficina y ' +
      'reportar un incidente a tiempo.',
    category: 'Adiestramiento',
    recurrenceMonths: 12,
    renewLeadDays: 30,
    // Voz de las láminas (seed.mjs --voz / Seed-Curso.ps1 -Voz): Azure, español de Puerto Rico.
    voice: 'es-PR-KarinaNeural',
    certificate: {
      enabled: true,
      issuerName: 'Advance Logistics',
      signatoryName: 'Recursos Humanos',
      signatoryTitle: 'Advance Logistics',
      statement: 'Certifica haber completado el adiestramiento de Seguridad de la Información para Empleados.',
      showScore: true,
      showValidity: true,
      accentColor: '#047857',
    },
    playerConfig: {
      allowBack: true,
      presentation: {
        enabled: true,
        theme: { mode: 'light', bg: '#ffffff', accent: '#059669', panel: true, panelTitle: 'SEGURIDAD DE LA INFORMACIÓN' },
        transition: 'cover',
      },
    },
  },

  items: [...m0, ...m1, ...m2, ...m3, ...m4, ...m5, ...m6, ...m7, ...m8],
};
