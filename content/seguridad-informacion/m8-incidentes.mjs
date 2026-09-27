// Módulo 8 — Reporte y Respuesta a Incidentes (13 → 9 láminas).
// «Qué incluir en el reporte» y «Qué pasa después» van juntas.
// PENDIENTE DE LA EMPRESA: el canal real para reportar. Mientras tanto la lámina dice
// «tu supervisor o el departamento de sistemas».
import { slide, T, mod, mc, off, bullets, heading } from '../_shared/authoring.mjs';

export default [
  mod('Módulo 8 — Reporte y Respuesta a Incidentes',
    'Lo que más reduce el daño de un incidente es la rapidez con que se reporta. Reporta aunque no estés seguro de lo que pasó.'),

  slide({ layout: 'dark', title: '¿Qué es un incidente de seguridad?' },
    T(`<p>Cualquier evento que pone en riesgo la información, los equipos o las cuentas de la empresa, o que parece sospechoso. Por ejemplo:</p>
       ${bullets([
         'Hacer clic en un enlace sospechoso o descargar un archivo malicioso.',
         'Perder un equipo o que te lo roben.',
         'Un acceso que no reconoces.',
         'Archivos que desaparecen o aparecen cifrados.',
         'Mensajes inesperados que piden tu contraseña.',
       ])}
       <p><b>No hace falta confirmar que fue un ataque.</b> Si algo parece extraño, se reporta.</p>`)),

  slide({ layout: 'cards', title: 'Señales de que algo no está bien', kicker: 'Incluso si no estás seguro, repórtalo' },
    T(`<ul>
         <li>Ventanas que aparecen solas.</li>
         <li>Programas que se abren sin que los abras.</li>
         <li>Te piden la contraseña cuando no es normal.</li>
         <li>Archivos renombrados o cifrados.</li>
         <li>El equipo se pone muy lento.</li>
         <li>Alertas del antivirus.</li>
       </ul>`)),

  slide({ layout: 'band', title: '¿Hiciste clic en un enlace sospechoso?' },
    T(`<p>Mucha gente no lo reporta por vergüenza, pero el tiempo es clave:</p>
       <ol>
         <li>No escribas tu contraseña ni tus datos.</li>
         <li>Cierra la página y no descargues nada.</li>
         <li>No intentes arreglarlo por tu cuenta.</li>
         <li>Repórtalo enseguida.</li>
         <li>Si llegaste a escribir tu contraseña, cámbiala.</li>
       </ol>`)),

  slide({ layout: 'band', title: '¿Abriste un archivo malicioso?' },
    T(`<ol>
         <li>Desconéctate de internet si puedes.</li>
         <li>No reinicies el equipo: se puede perder evidencia.</li>
         <li>No borres el archivo.</li>
         <li>Repórtalo de inmediato y espera instrucciones de sistemas.</li>
       </ol>
       <p>Muchos programas maliciosos se propagan rápido. El tiempo importa.</p>`)),

  slide({ layout: 'split', title: 'Pérdida o robo de un equipo' },
    T(`<p>Si pierdes o te roban la laptop, el teléfono de la empresa, una memoria USB autorizada o cualquier equipo con acceso a información,
       repórtalo de inmediato. Así sistemas puede:</p>
       ${bullets([
         'Quitarle el acceso a las cuentas.',
         'Bloquear el equipo.',
         'Borrarlo a distancia, si se puede.',
         'Vigilar actividad sospechosa.',
       ])}`)),

  slide({ layout: 'callout', title: '¿A quién reportar?', kicker: 'Reporta rápido' },
    T(`<p>Repórtalo a <b>tu supervisor</b> o al <b>departamento de sistemas</b> de la empresa, por teléfono o en persona si es urgente.</p>
       <p>No esperes a estar seguro de que es un incidente real.</p>`)),

  slide({ layout: 'dark', title: 'El reporte y lo que pasa después' },
    T(`<p><b>No necesitas un reporte técnico.</b> Basta con decir qué pasó, qué estabas haciendo, qué viste en pantalla y si hiciste clic,
       escribiste tu contraseña, descargaste algo o perdiste un equipo.</p>
       <p><b>Después</b>, sistemas analiza lo que pasó, revisa los registros, aísla el equipo si hace falta, cambia contraseñas o bloquea accesos.</p>
       <p>Reportar no es un castigo: es lo que protege a toda la empresa.</p>`)),

  slide({ layout: 'callout', title: 'No existe el error tonto', kicker: 'Cultura de reporte' },
    T(`<p>Ni el «no es nada», ni el «mejor no digo nada». Reportar es parte del trabajo y una de las formas más efectivas de evitar un daño mayor.</p>`)),

  slide({ layout: 'callout', kicker: 'Caso práctico' },
    T('<p>Un empleado recibe un correo que parece legítimo, hace clic en el enlace y la página le pide su usuario y contraseña. Sospecha que algo no está bien.</p>')),

  mc('¿Cuál de los siguientes es un incidente de seguridad?',
    ['Abrir un documento interno', 'Perder un equipo corporativo', 'Revisar el correo laboral', 'Usar la VPN'], 'b', 1),
  mc('¿A quién debes reportar un incidente?',
    ['Redes sociales', 'A tu supervisor o al departamento de sistemas', 'Familiares', 'Compañeros de trabajo'], 'b', 1),
  mc('¿Qué debes hacer si haces clic en un enlace sospechoso?',
    ['Ingresar tus datos para verificar', 'Ignorarlo', 'Reportarlo inmediatamente', 'Reenviarlo a compañeros'], 'c', 1),
  mc('En el caso de la página que pide la contraseña, ¿qué NO debe hacer el empleado?',
    ['Reportarlo', 'Cambiar su contraseña', 'Ingresar sus credenciales', 'Contactar a TI'], 'c', 1),
  off({ type: 'OpenResponse', points: 0, payload: { question: '¿Alguna vez dudaste si debías reportar algo? ¿Qué señales aprendiste hoy que te ayudarán a decidir más rápido?', graded: false } }),

  // Lámina final antes del resultado (como en los cursos de cumplimiento). Enlaces
  // verificados el 27 sep 2026.
  slide({ layout: 'dark', title: 'Recursos adicionales' },
    T(`${heading('Recursos adicionales')}
       <p>Material complementario, opcional, para seguir aprendiendo:</p>
       ${bullets([
         '<a href="https://consumidor.ftc.gov/articulos/como-reconocer-y-evitar-las-estafas-de-phishing" target="_blank" rel="noopener">Cómo reconocer y evitar las estafas de phishing</a> — Comisión Federal de Comercio (FTC), en español.',
         '<a href="https://www.cisa.gov/secure-our-world" target="_blank" rel="noopener">Secure Our World</a> — cuatro prácticas básicas de la agencia de ciberseguridad de EE. UU. (CISA), en inglés.',
         'Videos del curso: <a href="https://www.youtube.com/watch?v=fkrsaWn5tjo" target="_blank" rel="noopener">el triángulo CIA</a>, <a href="https://www.youtube.com/watch?v=g6tO3ObVvEc" target="_blank" rel="noopener">el factor humano</a> y <a href="https://www.youtube.com/watch?v=s6xbg6jbTKo" target="_blank" rel="noopener">el phishing</a>.',
       ])}
       <p><b>Recuerda:</b> si algo no se ve bien, no lo ocultes ni lo arregles por tu cuenta. Repórtalo enseguida a tu supervisor o al departamento de sistemas.</p>`)),
];
