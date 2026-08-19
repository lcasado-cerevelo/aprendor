import {info, mod, mc, match, T, S, fig } from './authoring.mjs';

// Módulo 1 — diapositivas 4–10 del original.
export default [
  mod('Módulo 1 — Qué es HIPAA y a quién aplica', 'Duración estimada: 7–9 minutos'),

  info('¿Qué es la Ley HIPAA?',
    T(`<p style="${S.lead}">Es una ley federal de 1996 que se conoce como la <strong>Ley de Portabilidad y
       Responsabilidad del Seguro Médico</strong>. Sus siglas provienen del título original en inglés,
       <em>"Health Insurance Portability and Accountability Act"</em>.</p>
       <div style="${S.call}">Esta ley tiene como objetivo <strong>proteger la privacidad y confidencialidad de la
       información del paciente</strong>.</div>
       <p>HIPAA está diseñada para proteger la información sensitiva de salud, regular cómo se puede usar o
       divulgar, y darle derechos al paciente sobre su propia información.</p>
       ${fig('hipaa-logo', 'Health Insurance Portability and Accountability Act', 460)}`)),

  info('Las dos organizaciones reguladas bajo HIPAA',
    T(`<p>Hay dos tipos de organizaciones reguladas bajo HIPAA:</p>
       <div style="${S.grid}">
         <div style="${S.card}"><strong>Entidad Cubierta</strong><br><em>Covered Entity (CE)</em><br>
           Planes de salud, cámaras de compensación y proveedores de atención médica.</div>
         <div style="${S.card}"><strong>Asociado de Negocios</strong><br><em>Business Associate (BA)</em><br>
           Quien maneja PHI a nombre de una entidad cubierta.<br><strong>Aquí entra Advance Logistics.</strong></div>
       </div>`)),

  info('Entidad Cubierta o Covered Entity (CE)',
    T(`<p>Las entidades cubiertas son la fuente de la PHI y donde se generó por primera vez, porque son las que
       tienen una relación directa con las personas cuya PHI mantienen. Están reguladas directamente por HIPAA y
       requieren estar en cumplimiento con la ley.</p>
       <p>Son, además, cualquier persona u organización que transmite información de salud de forma electrónica en
       relación con transacciones cubiertas bajo HIPAA:</p>
       <ul>
         <li><strong>Planes médicos</strong> — por ejemplo, MMM o Triple S.</li>
         <li><strong>Proveedores de salud</strong> — por ejemplo, médicos, dentistas o farmacias.</li>
         <li><strong>Cámaras de compensación</strong> (<em>healthcare clearinghouses</em>) — por ejemplo, Assertus
             o Inmediata.</li>
       </ul>`)),

  info('Asociado de Negocios o Business Associate (BA)',
    T(`<p>Es la persona o empresa que, a nombre de la entidad cubierta, <strong>crea, recibe, mantiene o
       transmite PHI</strong> para una función o actividad regulada por HIPAA.</p>
       <ul>
         <li>Funciones como procesamiento o administración de reclamaciones, análisis de datos, revisión de
             utilización, evaluación de calidad, actividades de seguridad, facturación, manejo de beneficios y
             <strong>entrega y administración de medicamentos</strong>.</li>
         <li>Servicios legales, actuariales, contables, de consultoría, agregación de datos, administración,
             acreditación o servicios financieros que requieran divulgación de PHI.</li>
         <li>No forma parte del personal de la entidad cubierta.</li>
         <li>Incluye a los subcontratistas del asociado de negocios.</li>
         <li>Una entidad cubierta puede ser, a su vez, asociado de negocios de otra entidad cubierta.</li>
       </ul>
       <div style="${S.call}"><strong>Advance Logistics es un asociado de negocios.</strong> Al transportar
       medicamentos, documentos, muestras y equipo médico manejamos PHI a nombre de farmacias, planes y
       proveedores; por eso HIPAA nos aplica directamente.</div>`)),

  info('La cadena de confianza y el contrato BAA',
    T(`<p>Las entidades cubiertas no pueden compartir PHI con un socio de negocios a menos que se aseguren de que
       este cumple con HIPAA, es decir, que tenga las salvaguardas apropiadas para proteger la información. Esa
       garantía se maneja mediante un <strong>contrato de asociado de negocios</strong> firmado y vigente
       (<em>Business Associate Agreement</em>, BAA).</p>
       <p>El BAA es un documento legal que establece la relación de socio de negocio y las obligaciones de cada
       parte para cumplir con las disposiciones de la Ley HIPAA.</p>
       <div style="${S.call}">Esto crea la <strong>"cadena de confianza"</strong>: comienza en la entidad cubierta y
       continúa por múltiples niveles de asociados de negocio y subcontratistas. Todos quedan vinculados a la
       entidad cubierta original a través del contrato de asociado de negocios.</div>
       ${fig('cadena-de-confianza', 'Diagrama: la entidad cubierta encadenada a sus asociados de negocio', 320)}`)),

  info('La Regla Ómnibus de HIPAA',
    T(`<p>La Regla Ómnibus, finalizada en 2012 y en vigencia desde 2013, actualizó todas las reglas que veremos en
       este curso: Privacidad, Seguridad, Notificación de Incumplimiento y Cumplimiento. El objetivo fue mejorar la
       confidencialidad y la seguridad en el intercambio de datos.</p>
       <div style="${S.warn}">El cambio mayor: <strong>los socios comerciales pasaron a estar obligados a cumplir
       con la Regla de Privacidad y la Regla de Seguridad, y a responder directamente por cualquier violación de
       HIPAA</strong>. Desde entonces deben firmar un BAA y sostener los estándares de seguridad igual que las
       entidades cubiertas.</div>`)),

  mc('¿Qué protege principalmente la Ley HIPAA?',
    ['La privacidad y confidencialidad de la información de salud del paciente',
     'El precio de los medicamentos recetados',
     'El horario de trabajo del personal de salud',
     'La calidad del empaque de los envíos médicos'], 'a'),

  mc('¿Qué papel ocupa Advance Logistics bajo HIPAA?',
    ['Entidad cubierta (Covered Entity), porque transporta medicamentos',
     'Asociado de negocios (Business Associate), porque maneja PHI a nombre de entidades cubiertas',
     'Ninguno: HIPAA solo aplica a hospitales y planes médicos',
     'Cámara de compensación (clearinghouse)'], 'b'),

  mc('¿Para qué sirve el contrato de asociado de negocios (BAA)?',
    ['Para fijar la tarifa del servicio de entrega',
     'Para autorizar al paciente a recibir su medicamento',
     'Para establecer por escrito la relación y las obligaciones de proteger la PHI bajo HIPAA',
     'Para reportar las entregas al Departamento de Salud'], 'c'),

  match('Clasifica cada organización según HIPAA.',
    [['ce', 'Entidad cubierta (CE)'], ['ba', 'Asociado de negocios (BA)']],
    [['p1', 'La farmacia que despacha la receta', 'ce'],
     ['p2', 'Advance Logistics, que entrega el medicamento a domicilio', 'ba'],
     ['p3', 'Un plan médico como MMM o Triple S', 'ce'],
     ['p4', 'La compañía que le factura las reclamaciones a la farmacia', 'ba'],
     ['p5', 'Un subcontratista de Advance Logistics con acceso a PHI', 'ba']]),

  mc('Una entidad cubierta va a entregarle PHI a un socio de negocios nuevo. ¿Qué tiene que existir antes?',
    ['Nada; basta con una llamada de coordinación',
     'Un BAA firmado y vigente que obligue al socio a proteger la PHI',
     'Una autorización del Departamento de Salud federal',
     'Que el socio también sea una entidad cubierta'], 'b'),
];
