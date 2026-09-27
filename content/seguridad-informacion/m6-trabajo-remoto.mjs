// Módulo 6 — Trabajo Remoto y Dispositivos Personales (12 → 6 láminas).
// Dispositivos personales y cifrado de disco van juntos; la sincronización automática pasa
// al módulo 2 y «Pérdida o robo» sale (está completa en el módulo 8).
// PENDIENTE DE LA EMPRESA: si hay VPN y si se permite el teléfono personal. Mientras
// tanto la VPN se nombra «si la empresa te la dio» y el teléfono personal queda como
// «si la empresa lo permite».
import { slide, T, mod, mc, off, bullets } from '../_shared/authoring.mjs';

export default [
  mod('Módulo 6 — Trabajo Remoto y Dispositivos Personales',
    'Fuera de la oficina no están los controles de la red de la empresa: los riesgos cambian y las precauciones también.'),

  slide({ layout: 'band', title: 'Riesgos del WiFi público' },
    T(`<p>Las redes de aeropuertos, cafés, hoteles o centros comerciales suelen ser inseguras: cualquiera conectado a la misma red puede
       intentar ver lo que envías o robar contraseñas.</p>
       <p>Evítalas para el trabajo. Si no hay otra red, es mejor el <b>hotspot de tu celular</b>: tú controlas quién se conecta.
       Si la empresa te dio una <b>VPN</b>, actívala: cifra la conexión con la red de la empresa.</p>`)),

  slide({ layout: 'dark', title: 'Computadoras compartidas' },
    T(`<p>Las computadoras de hoteles, bibliotecas o de familiares pueden tener malware, programas que graban lo que escribes o
       historial de otros usuarios.</p>
       <p><b>No las uses para entrar a los sistemas ni al correo del trabajo.</b> El modo incógnito no basta: el equipo puede guardar
       igual contraseñas y archivos temporales.</p>`)),

  slide({ layout: 'cards', title: 'Bloqueo de pantalla', kicker: 'Simple y muy efectivo' },
    T(`<ul>
         <li><b>Bloquea siempre</b><br>Cada vez que te levantas, aunque sea un momento: Windows + L.</li>
         <li><b>Bloqueo automático</b><br>Que el equipo se bloquee solo a los pocos minutos.</li>
         <li><b>Miradas ajenas</b><br>En lugares públicos, cuida quién ve tu pantalla.</li>
       </ul>`)),

  slide({ layout: 'split', title: 'Dispositivos personales y cifrado' },
    T(`<p>Si la empresa permite usar tu teléfono o computadora personal para el trabajo, ese equipo necesita lo mínimo:</p>
       ${bullets([
         'Bloqueo con PIN, huella o rostro.',
         'Actualizaciones automáticas.',
         'Disco cifrado: si se pierde, nadie puede leer lo que tiene.',
         'Datos del trabajo separados de los personales.',
       ])}
       <p>Un equipo sin antivirus, sin actualizar o con aplicaciones no autorizadas pone en riesgo a toda la empresa.</p>`)),

  slide({ layout: 'callout', kicker: 'Caso práctico' },
    T('<p>Estás trabajando desde un café y necesitas enviar un documento de la empresa con urgencia. El WiFi del lugar está disponible y es gratis.</p>')),

  mc('¿Qué red es más segura para trabajar?',
    ['WiFi público', 'Hotspot desconocido', 'Red corporativa o VPN', 'Red abierta de un café'], 'c', 1),
  mc('¿Qué dispositivo NO debe usarse para acceder a los sistemas de la empresa?',
    ['Laptop corporativa', 'Computadora personal sin controles', 'Dispositivo con VPN', 'Equipo aprobado por TI'], 'b', 1),
  mc('En el caso del café, ¿qué debes hacer primero?',
    ['Conectarte al WiFi del café', 'Usar tu hotspot personal o activar la VPN', 'Enviarlo sin revisar', 'Guardarlo en tu USB'], 'b', 1),
  mc('En el caso del café, ¿qué riesgo tiene el WiFi público?',
    ['Velocidad lenta', 'Intercepción de datos', 'Falta de señal', 'Consumo de batería'], 'b', 1),
  off({ type: 'OpenResponse', points: 0, payload: { question: '¿En qué lugares sueles trabajar fuera de la oficina? ¿Qué medidas de seguridad aplicarás a partir de ahora?', graded: false } }),
];
