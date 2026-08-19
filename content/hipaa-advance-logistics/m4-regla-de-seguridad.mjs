import {info, mod, mc, match, T, S, fig } from './authoring.mjs';

// Módulo 4 — diapositivas 20–23 y 33–36 del original.
export default [
  mod('Módulo 4 — La Regla de Seguridad y las salvaguardas', 'Duración estimada: 6–8 minutos'),

  info('Disposiciones clave de la Regla de Seguridad',
    T(`<div style="${S.grid}">
         <div style="${S.cardG}">La Regla de Seguridad protege la información cubierta por la Regla de Privacidad
           que se crea, recibe, mantiene o transmite <strong>en forma electrónica (e-PHI)</strong>. No aplica a la
           PHI transmitida oralmente o por escrito.</div>
         <div style="${S.cardS}">Requiere mantener y proteger la e-PHI de manera razonable mediante:
           <ul style="margin:8px 0 0 18px">
             <li>Salvaguardas administrativas</li>
             <li>Salvaguardas técnicas</li>
             <li>Salvaguardas físicas</li>
           </ul></div>
       </div>`)),

  info('Requisitos de seguridad para entidades cubiertas y sus socios',
    T(`<div style="${S.grid}">
         <div style="${S.card}">Garantizar la confidencialidad, integridad y disponibilidad de toda la e-PHI que se
           crea, recibe, mantiene o transmite.</div>
         <div style="${S.card}">Identificar y proteger contra amenazas razonablemente anticipadas a la seguridad o
           integridad de la información.</div>
         <div style="${S.card}">Proteger contra usos o divulgaciones razonablemente anticipados e inadmisibles.</div>
         <div style="${S.card}">Garantizar el cumplimiento por parte de toda su fuerza laboral.</div>
         <div style="${S.card}">Realizar un análisis de riesgos como parte de sus procesos de gestión de seguridad y
           actualizarlo regularmente.</div>
       </div>`)),

  info('Salvaguardas técnicas',
    T(`<p><strong>Acceso y control de la facilidad.</strong> Deben existir límites físicos y lógicos de acceso,
       asegurando al mismo tiempo que se permita el acceso autorizado.</p>
       <p><strong>Seguridad en el área de trabajo y el equipo.</strong> Deben existir políticas y procedimientos que
       especifiquen el uso y acceso apropiado a las estaciones de trabajo y medios electrónicos (laptops, correos
       electrónicos, sitios web, memorias USB).</p>
       <p>En la práctica, las prácticas de seguridad de TI deben aplicarse estrictamente:</p>
       <ul>
         <li>Cifrado de redes y discos duros — los datos se cifran en las tres fases: en reposo, en tránsito y en
             almacenamiento.</li>
         <li>Inicios de sesión únicos para acceso controlado.</li>
         <li>Autenticación de dos factores para todos los sistemas con e-PHI.</li>
         <li>Cifrado de los dispositivos de la empresa.</li>
         <li>Auditorías de actividad y control.</li>
         <li>Tiempos de espera y cierres de sesión automáticos en los sistemas.</li>
       </ul>
       ${fig('salvaguardas-tecnicas', 'Estetoscopio sobre un teclado', 360)}`)),

  info('Salvaguardas físicas',
    T(`<p>Involucran la forma en que se manejan los sistemas físicos y los equipos que contienen PHI. Los
       dispositivos como servidores y computadoras deben estar en un lugar seguro. También se recomiendan registros
       de acceso detallados de las personas que ingresan a espacios seguros, para monitorear adecuadamente a quiénes
       ven la PHI. Incluyen:</p>
       <ul>
         <li>Control de acceso a las instalaciones.</li>
         <li>Administración de estaciones de trabajo.</li>
         <li>Política de dispositivos móviles.</li>
         <li>Seguimiento a los servidores.</li>
       </ul>
       <div style="${S.call}"><strong>En nuestra operación:</strong> el vehículo y la tableta de ruta también son
       "estaciones de trabajo". Paquetes y manifiestos no se dejan a la vista dentro del vehículo, y el vehículo no
       se deja abierto ni desatendido con PHI adentro.</div>`)),

  info('Salvaguardas administrativas',
    T(`<p>Requieren que la organización documente las actividades que realiza para su cumplimiento de HIPAA. Las
       actividades documentadas pueden incluir:</p>
       <ul>
         <li>Designación de un Oficial de Seguridad.</li>
         <li>Capacitación de los miembros del personal.</li>
         <li>Finalización de la evaluación de riesgos.</li>
         <li>Manejo sistemático de riesgos.</li>
         <li>Implementación de políticas y procedimientos de seguridad.</li>
         <li>Implementación del Plan de Recuperación ante Desastres.</li>
         <li>Planes de contingencia.</li>
       </ul>
       <div style="${S.ok}">Este adiestramiento y su certificado forman parte de las salvaguardas administrativas de
       Advance Logistics: son la evidencia documentada de que el personal fue capacitado.</div>`)),

  mc('¿A qué información aplica específicamente la Regla de Seguridad de HIPAA?',
    ['A toda la PHI, incluyendo la conversada de viva voz',
     'Solo a los expedientes en papel',
     'A la información de salud protegida en forma electrónica (e-PHI)',
     'Solo a la información de facturación'], 'c'),

  match('Clasifica cada medida según el tipo de salvaguarda.',
    [['tec', 'Técnica'], ['fis', 'Física'], ['adm', 'Administrativa']],
    [['p1', 'Autenticación de dos factores para entrar al sistema', 'tec'],
     ['p2', 'Cerrar con llave el cuarto donde está el servidor', 'fis'],
     ['p3', 'Designar un Oficial de Seguridad', 'adm'],
     ['p4', 'Cifrar el disco duro de la laptop', 'tec'],
     ['p5', 'Adiestrar anualmente al personal', 'adm']]),

  mc('Según la lista de verificación de cumplimiento, ¿en qué fases deben cifrarse los datos?',
    ['Solo cuando se envían por correo electrónico',
     'En reposo, en tránsito y en almacenamiento',
     'Solo mientras están en la tableta del conductor',
     'Solo al final del día, en el respaldo'], 'b'),

  mc('Terminas la ruta y dejas la tableta con los manifiestos abiertos en el asiento del vehículo mientras almuerzas. ¿Qué falla?',
    ['Nada, el vehículo es propiedad de la empresa',
     'Falla una salvaguarda física: el equipo con PHI quedó accesible y sin supervisión',
     'Falla la Regla de Privacidad, porque no hubo divulgación',
     'Falla solo si alguien llega a ver la pantalla'], 'b'),
];
