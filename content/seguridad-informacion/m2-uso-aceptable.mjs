// Módulo 2 — Uso Aceptable de los Sistemas (11 → 7 láminas).
// Software sin autorización y USB van en una lámina; «Nubes personales» recibe la
// sincronización automática del módulo 6. El recuadro de WiFi público sale (lo explica
// el módulo 6).
import { slide, T, mod, mc, off, bullets } from '../_shared/authoring.mjs';

export default [
  mod('Módulo 2 — Uso Aceptable de los Sistemas',
    'Cada equipo, red o aplicación del trabajo puede ser una puerta de entrada para un atacante si no se usa como indica la empresa.'),

  slide({ layout: 'dark', title: 'Uso de computadoras de la empresa' },
    T(`<p>Las computadoras de la empresa vienen con antivirus, permisos y controles que te protegen a ti y a la empresa.</p>
       ${bullets([
         'Mantén el equipo actualizado.',
         'No desactives el antivirus ni el firewall, ni «por unos minutos».',
         'No instales programas sin aprobación.',
         'Evita el uso personal excesivo.',
       ])}
       <p><b>Firewall:</b> filtra lo que entra y sale del equipo y bloquea conexiones no autorizadas.</p>`)),

  slide({ layout: 'band', title: 'Uso de internet' },
    T(`<p>Internet es una de las principales fuentes de amenazas: un sitio malicioso o una descarga insegura pueden comprometer un equipo en segundos.</p>
       ${bullets([
         'Evita páginas que no tienen que ver con el trabajo.',
         'No descargues archivos de fuentes desconocidas.',
         'No compartas información del trabajo en redes sociales.',
       ])}
       <p><b>HTTPS</b> (el candado del navegador) indica que la conexión va cifrada. No garantiza que el sitio sea legítimo,
       pero un sitio sin HTTPS que te pide tu contraseña es una mala señal.</p>`)),

  slide({ layout: 'cards', title: 'Uso del correo electrónico', kicker: 'El medio preferido de los atacantes' },
    T(`<ul>
         <li><b>Verifica el remitente</b> antes de abrir un mensaje.</li>
         <li><b>No abras adjuntos</b> que no esperabas.</li>
         <li><b>No reenvíes</b> cadenas ni correos personales.</li>
         <li><b>Reporta</b> los correos sospechosos.</li>
       </ul>
       <p><b>Enlaces acortados</b> (bit.ly, tinyurl…): esconden el destino real. Si no puedes ver a qué dominio lleva, no hagas clic.</p>`)),

  slide({ layout: 'dark', title: 'Uso de dispositivos móviles' },
    T(`<p>El celular y la tableta se pierden con facilidad y se conectan a redes que no controlamos.</p>
       ${bullets([
         'Mantén el sistema actualizado.',
         'Usa bloqueo de pantalla con PIN, huella o rostro.',
         'No guardes información sensible sin cifrar.',
         'Si lo pierdes o te lo roban, repórtalo de inmediato.',
       ])}`)),

  slide({ layout: 'split', title: 'Programas y memorias USB' },
    T(`<p><b>Programas sin autorización.</b> Pueden traer malware, abrir puertas traseras o recopilar datos sin permiso. Muchos
       instaladores gratuitos incluyen programas no deseados que muestran anuncios o espían la actividad.</p>
       <p><b>Memorias USB.</b> Son una de las fuentes de infección más comunes. Usa solo las aprobadas por la empresa y no
       conectes una que te encontraste: los atacantes dejan USB infectados a propósito para que alguien los pruebe.</p>`)),

  slide({ layout: 'band', title: 'Nubes personales y sincronización' },
    T(`<p>Subir documentos de la empresa a tu Google Drive, Dropbox o iCloud personal es un riesgo grave: la empresa pierde el control
       de esa información y no puede protegerla.</p>
       <p>Ojo con la <b>sincronización automática</b>: muchos teléfonos y computadoras copian solos los archivos a la nube personal.
       Guarda los documentos del trabajo solo en las plataformas que aprobó la empresa.</p>
       <p>Cuando información de la empresa termina donde no debe, eso es una <b>fuga de datos</b>.</p>`)),

  slide({ layout: 'callout', kicker: 'Caso práctico' },
    T('<p>Un empleado descarga un programa gratuito de internet para «trabajar más rápido». Después de instalarlo, la computadora empieza a comportarse de forma extraña.</p>')),

  mc('¿Qué acción representa un riesgo al usar internet en el trabajo?',
    ['Acceder al portal interno de la empresa', 'Descargar archivos de sitios desconocidos', 'Revisar manuales corporativos', 'Usar la intranet'], 'b', 1),
  mc('¿Qué debes hacer si recibes un correo sospechoso?',
    ['Abrirlo para verificar su contenido', 'Reenviarlo a compañeros', 'Reportarlo al equipo de seguridad', 'Ignorarlo y borrarlo'], 'c', 1),
  mc('¿Qué práctica es segura en dispositivos móviles de la empresa?',
    ['No usar bloqueo de pantalla', 'Conectarse a cualquier red WiFi pública', 'Mantener el sistema operativo actualizado', 'Instalar apps personales sin revisión'], 'c', 1),
  mc('En el caso del programa gratuito, ¿qué debió hacer el empleado antes de instalarlo?',
    ['Consultar con TI', 'Instalarlo sin preguntar', 'Descargarlo en su celular', 'Ignorar la necesidad'], 'a', 1),
  off({ type: 'OpenResponse', points: 0, payload: { question: 'Describe una situación en la que hayas usado un dispositivo personal para trabajar. ¿Qué riesgos identificas ahora?', graded: false } }),
];
