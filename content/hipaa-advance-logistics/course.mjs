// ============================================================================
// Curso: "Cumplimiento HIPAA para transporte y logística" — Advance Logistics
//
// Recreación nativa (texto + HTML) del adiestramiento preparado por
// Marie Carmen Muntaner, Esq. — MM & Associates, LLC. No se usan las imágenes
// escaneadas del PDF: el contenido está transcrito para que sea buscable,
// accesible, editable y legible en celular.
//
// 7 módulos. Cada uno abre con un ModuleHeader, sigue con sus pantallas de
// contenido (Info) y cierra con sus preguntas (10 puntos cada una).
// El umbral de aprobación de la versión es 70% (TrainingVersion.PassPercent).
// ============================================================================

import m0 from './m0-apertura.mjs';
import m1 from './m1-que-es-hipaa.mjs';
import m2 from './m2-transporte-y-logistica.mjs';
import m3 from './m3-phi-y-privacidad.mjs';
import m4 from './m4-regla-de-seguridad.mjs';
import m5 from './m5-incumplimiento-y-notificacion.mjs';
import m6 from './m6-practicas-diarias.mjs';
import m7 from './m7-panorama-y-cierre.mjs';

export default {
  training: {
    title: 'Cumplimiento HIPAA para transporte y logística',
    description:
      'Adiestramiento anual de cumplimiento con la Ley HIPAA para el personal de Advance Logistics: ' +
      'conductores, despacho, servicio al cliente y personal administrativo que maneja o transporta ' +
      'información de salud protegida (PHI). Basado en el material de MM & Associates, LLC.',
    category: 'Cumplimiento',
    recurrenceMonths: 12,   // HIPAA se re-adiestra todos los años
    renewLeadDays: 30,      // reabre 30 días antes de vencer
    certificate: {
      enabled: true,
      issuerName: 'Advance Logistics',
      signatoryName: 'Marie Carmen Muntaner, Esq.',
      signatoryTitle: 'MM & Associates, LLC — Asesoría legal',
      statement:
        'Certifica haber completado el adiestramiento de cumplimiento con la Ley de Portabilidad y ' +
        'Responsabilidad del Seguro Médico (HIPAA), 45 CFR Partes 160 y 164, aplicable al personal de ' +
        'transporte y logística que maneja información de salud protegida.',
      showScore: true,
      showValidity: true,
      accentColor: '#1e6fd9',
    },
  },

  items: [...m0, ...m1, ...m2, ...m3, ...m4, ...m5, ...m6, ...m7],
};
