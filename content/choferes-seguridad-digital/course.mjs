// ============================================================================
// Curso: "Seguridad Digital para Choferes" — Adiestramiento
//
// Versión para choferes que junta lo que les aplica de «Seguridad de la Información» y de
// «Inteligencia Artificial», escrito para su día a día: la tableta, el teléfono personal,
// WhatsApp (lo que más usan), la información de clientes y pacientes, los mensajes y
// llamadas falsas (con voces clonadas con IA) y qué hacer si algo pasa. 5 módulos, unos
// 18 minutos, 15 preguntas de 1 punto; se aprueba con 70 %.
//
// Se asigna por grupo: el grupo «Choferes» lo tiene en su plan, y los cursos completos de
// Seguridad e IA quedan para los demás grupos (Training.Audience = groups en los tres).
// Tema claro (blanco y verde), como los demás de Adiestramiento.
// ============================================================================

import { m0, m1, m2 } from './m0-m2.mjs';
import { m3, m4, m5 } from './m3-m5.mjs';

export default {
  training: {
    title: 'Seguridad Digital para Choferes',
    description:
      'Seguridad digital para choferes: cuidar la tableta y la contraseña, proteger la información de clientes y ' +
      'pacientes, usar WhatsApp con cuidado, reconocer mensajes y llamadas falsas (también con IA), usar la ' +
      'inteligencia artificial sin exponer información y qué hacer si algo pasa.',
    category: 'Adiestramiento',
    recurrenceMonths: 12,
    renewLeadDays: 30,
    voice: 'es-PR-KarinaNeural',
    certificate: {
      enabled: true,
      issuerName: 'Advance Logistics',
      signatoryName: 'Recursos Humanos',
      signatoryTitle: 'Advance Logistics',
      statement: 'Certifica haber completado el adiestramiento de Seguridad Digital para Choferes.',
      showScore: true,
      showValidity: true,
      accentColor: '#047857',
    },
    playerConfig: {
      allowBack: true,
      presentation: {
        enabled: true,
        theme: { mode: 'light', bg: '#ffffff', accent: '#059669', panel: true, panelTitle: 'SEGURIDAD DIGITAL' },
        transition: 'cover',
      },
    },
  },

  items: [...m0, ...m1, ...m2, ...m3, ...m4, ...m5],
};
