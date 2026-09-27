// Módulo 7 — Manejo de Información Sensible (13 → 6 láminas).
// Información personal, financiera y de clientes van en tarjetas; «Clasificación» sale
// (está en el módulo 1); almacenamiento y transmisión van juntos. Se cita HIPAA y la
// Ley Núm. 111-2005 de Puerto Rico en vez de GDPR (a confirmar con Legal).
// PENDIENTE DE LA EMPRESA: qué se puede enviar por WhatsApp. Mientras tanto la lámina dice
// «solo por los canales que aprobó la empresa».
import { slide, T, mod, mc, off, bullets } from '../_shared/authoring.mjs';

export default [
  mod('Módulo 7 — Manejo de Información Sensible',
    'Proteger la información sensible no depende solo de la tecnología: depende de cómo la maneja cada empleado.'),

  slide({ layout: 'dark', title: '¿Qué es información sensible?' },
    T(`<p>Cualquier dato que cause daño si se divulga, se cambia o se destruye sin autorización: personal, financiero, de clientes, de la operación o de los sistemas.</p>
       ${bullets([
         '<b>Información personal:</b> identifica a una persona. Nombre, dirección, teléfono.',
         '<b>Información personal sensible:</b> la que más daño hace si se filtra. Seguro social, números de cuenta, historial médico. Lleva controles más estrictos.',
       ])}
       <p>En Puerto Rico, la Ley Núm. 111-2005 obliga a avisar cuando se filtra información personal, y HIPAA protege la información de salud.</p>`)),

  slide({ layout: 'cards', title: 'Tres tipos que manejamos a diario', kicker: 'Información sensible' },
    T(`<ul>
         <li><b>Personal</b><br>De empleados, clientes o proveedores: nombre, dirección, fecha de nacimiento, identificación. Recoge y guarda solo lo necesario.</li>
         <li><b>Financiera</b><br>Números de cuenta, tarjetas, transacciones, reportes. Si se filtra, hay pérdidas y multas.</li>
         <li><b>De clientes</b><br>Contactos, pedidos, documentos y reclamaciones. Muchos contratos exigen protegerla.</li>
       </ul>`)),

  slide({ layout: 'callout', title: 'Principio de mínimo privilegio', kicker: 'Solo lo que necesitas' },
    T(`<p>Cada persona tiene acceso solo a lo que necesita para su trabajo. Los accesos se dan por puesto, no por confianza ni por
       conveniencia, y se revisan de vez en cuando.</p>`)),

  slide({ layout: 'band', title: 'Dónde guardar y cómo enviar' },
    T(`<p><b>Guárdala solo en</b> los sistemas y carpetas de la empresa, con permisos controlados. Nunca en un USB personal, una nube
       personal, un equipo no autorizado o tu correo personal.</p>
       <p><b>Envíala solo por</b> los canales que aprobó la empresa: el correo de la empresa y sus plataformas internas. No por redes
       sociales ni por correo o mensajería personal.</p>
       <p>Cifrar protege los datos guardados, aunque roben el equipo, y los que viajan por la red.</p>`)),

  slide({ layout: 'dark', title: 'Eliminación segura' },
    T(`<p>La información sensible no se guarda más tiempo del necesario. Cuando ya no hace falta:</p>
       <ol>
         <li>Tritura los documentos en papel.</li>
         <li>Borra los archivos con las herramientas que aprobó la empresa.</li>
         <li>Recuerda: arrastrar un archivo a la papelera no es suficiente.</li>
       </ol>`)),

  slide({ layout: 'callout', kicker: 'Caso práctico' },
    T('<p>Un empleado entra a información de clientes que no necesita para su trabajo. Lo hace «por curiosidad», sin mala intención.</p>')),

  mc('¿Qué tipo de información protegen las leyes de privacidad?',
    ['Información pública', 'Información personal', 'Información de marketing', 'Información irrelevante'], 'b', 1),
  mc('¿Qué tipo de información requiere cifrado y controles estrictos?',
    ['Información pública', 'Información financiera', 'Información general', 'Información de redes sociales'], 'b', 1),
  mc('En el caso del acceso «por curiosidad», ¿qué riesgo existe?',
    ['Pérdida de señal', 'Acceso no autorizado', 'Falta de espacio', 'Error de impresión'], 'b', 1),
  mc('En el caso del acceso «por curiosidad», ¿qué debe hacer el empleado ahora?',
    ['Ignorar el incidente', 'Reportarlo', 'Compartirlo con compañeros', 'Guardarlo en su USB'], 'b', 1),
  off({ type: 'OpenResponse', points: 0, payload: { question: '¿Qué tipo de información manejas en tu rol que podría clasificarse como confidencial o restringida? ¿Qué medidas aplicarás para protegerla mejor?', graded: false } }),
];
