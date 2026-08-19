import { info, mod, mc, T, S } from './authoring.mjs';

// Módulo 2 — diapositivas 11–14 del original.
export default [
  mod('Módulo 2 — Por qué HIPAA nos aplica en transporte y logística', 'Duración estimada: 5–7 minutos'),

  info('¿Tenemos que cumplir con HIPAA?',
    T(`<p style="${S.lead}"><strong>Sí.</strong></p>
       <p>Por ley, la Regla de Privacidad de HIPAA aplica a las entidades cubiertas: planes de salud, cámaras de
       compensación y ciertos proveedores de atención médica. Sin embargo, la mayoría de esos proveedores y planes
       no llevan a cabo todas sus actividades por sí mismos: utilizan los servicios de otras personas y empresas.</p>
       <p>La Regla de Privacidad permite que proveedores y planes divulguen PHI a esos socios comerciales
       <strong>solo si obtienen garantías satisfactorias de que el socio usará la información únicamente para los
       fines para los que fue contratado</strong>, la protegerá del uso indebido y ayudará a la entidad cubierta a
       cumplir con sus deberes bajo la Regla de Privacidad.</p>
       <div style="${S.call}">La PHI se le divulga al socio comercial <strong>solo para ayudar a la entidad cubierta
       a llevar a cabo sus funciones de atención médica</strong>, no para los propósitos independientes del socio,
       excepto lo necesario para su gestión y administración adecuadas.</div>`)),

  info('¿Es necesario el cumplimiento de HIPAA en el envío y la logística?',
    T(`<p style="${S.lead}"><strong>Sí.</strong></p>
       <p>Existen sanciones estrictas por incumplimiento de HIPAA, que incluyen multas que pueden alcanzar montos
       millonarios. Por eso es fundamental que las empresas que transportan PHI tomen precauciones de seguridad
       especiales para proteger la privacidad de los pacientes.</p>
       <div style="${S.warn}">El incumplimiento puede tener consecuencias graves tanto para las farmacias como para
       las empresas de transporte y logística que realizan sus entregas: pueden enfrentar sanciones civiles o
       penales que van desde multas hasta penas de prisión.</div>`)),

  info('¿Qué es el cumplimiento de HIPAA en logística y transporte?',
    T(`<p>A pesar de no ser parte del sector de la salud, las empresas de logística y transporte deben cumplir con
       los estándares de HIPAA. Esto surge de la Regla Ómnibus, que exige el cumplimiento de la Regla de Privacidad
       por parte de los socios comerciales y de las entidades contratadas por individuos y empresas de la industria
       de la salud para completar actividades relacionadas.</p>
       <p>Los proveedores de transporte y logística entran en la categoría de socios comerciales cuando brindan
       servicios que involucran PHI. Las empresas de transporte médico que no son de emergencia contratadas por
       planes de salud y agencias de Medicaid también son socios comerciales cubiertos por HIPAA.</p>
       <p>Los contratistas o proveedores de esas empresas también están sujetos a las reglamentaciones, ya que un
       socio comercial se define como cualquier entidad que actúa como proveedor o subcontratista
       <strong>con acceso a la PHI</strong>.</p>`)),

  info('Responsabilidad del transportista como socio de negocios',
    T(`<ul>
         <li>Al manipular y transportar medicamentos, dispositivos y suministros que pueden salvar vidas, la
             seguridad debe ser una de las principales preocupaciones. El transporte seguro de artículos médicos
             críticos afecta directamente el nivel de atención que los pacientes esperan.</li>
         <li>El campo médico depende de los servicios de mensajería médica para transportar artículos sensibles y
             confidenciales: documentos, medicamentos recetados, medicamentos para infusión, análisis de
             laboratorio, muestras médicas, equipos y suministros. Para garantizar una estricta cadena de custodia,
             todos los conductores deben cumplir con las normas que exigen dichas entregas, incluyendo HIPAA.</li>
         <li>Esto significa que <strong>todo el personal de ruta debe conocer las mejores prácticas de transporte
             médico</strong> para eliminar riesgos y proteger la información de los pacientes.</li>
       </ul>`)),

  mc('¿Por qué HIPAA aplica a una empresa de transporte que no es parte del sector salud?',
    ['Porque toda empresa en Puerto Rico está cubierta por HIPAA',
     'Porque la Regla Ómnibus obliga a los socios comerciales que manejan PHI a cumplir con la ley',
     'Porque transporta paquetes de más de 50 libras',
     'Porque el conductor es empleado de la farmacia'], 'b'),

  mc('Un subcontratista de Advance Logistics cubre una ruta y tiene acceso a los manifiestos con datos de pacientes. ¿Qué aplica?',
    ['No aplica HIPAA: no tiene contrato directo con la farmacia',
     'Aplica solo si transporta medicamentos controlados',
     'Aplica HIPAA: un subcontratista con acceso a PHI también es socio comercial y forma parte de la cadena de confianza',
     'Aplica solo el reglamento del Departamento de Transportación'], 'c'),

  mc('¿Para qué puede usar Advance Logistics la PHI que recibe de una farmacia?',
    ['Únicamente para los fines del servicio contratado, más su gestión y administración adecuadas',
     'Para cualquier propósito comercial propio, incluyendo mercadeo',
     'Para compartirla con otros clientes como referencia de servicio',
     'Para publicarla en redes sociales si el paciente quedó satisfecho'], 'a'),
];
