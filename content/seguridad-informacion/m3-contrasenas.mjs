// Módulo 3 — Contraseñas y Autenticación (9 → 6 láminas).
// La lámina de contraseña segura pone primero el largo (NIST SP 800-63B). «Gestores de
// contraseñas» queda como consejo general, sin marcas, hasta que la empresa confirme si
// ofrece uno.
import { slide, T, mod, mc, off, bullets } from '../_shared/authoring.mjs';

export default [
  mod('Módulo 3 — Contraseñas y Autenticación',
    'La mayoría de los ataques que funcionan usan contraseñas débiles, fáciles de adivinar o repetidas.'),

  slide({ layout: 'dark', title: '¿Qué hace que una contraseña sea segura?' },
    T(`<p>Los atacantes usan programas que prueban millones de combinaciones por segundo. Una contraseña segura es difícil de adivinar, no fácil de recordar.</p>
       <ol>
         <li><b>Que sea larga:</b> 12 caracteres o más. Una frase es más fácil de recordar y más difícil de adivinar.</li>
         <li><b>Que no tenga datos personales:</b> nada de fechas, nombres ni direcciones.</li>
         <li><b>Que no sea una palabra del diccionario.</b></li>
         <li><b>Que no se repita</b> en otras cuentas.</li>
       </ol>
       <p>Mezclar mayúsculas, números y símbolos ayuda, pero el largo es lo que más cuenta.</p>`)),

  slide({ layout: 'callout', title: 'Una contraseña = una cuenta', kicker: 'El riesgo de reutilizar contraseñas' },
    T(`<p>Cuando una página sufre una filtración, los atacantes prueban esas mismas contraseñas en otros servicios. Si usas la misma
       en tu correo personal, tus redes sociales y el trabajo, una sola filtración les abre todas las puertas.</p>`)),

  slide({ layout: 'band', title: 'Gestores de contraseñas' },
    T(`<p>Recordar decenas de contraseñas largas y distintas es casi imposible. Un gestor de contraseñas las guarda por ti:</p>
       ${bullets([
         'Guarda todas tus contraseñas cifradas.',
         'Crea contraseñas largas y únicas.',
         'Solo tienes que recordar una contraseña maestra.',
       ])}
       <p>Pregunta a tu supervisor si la empresa tiene uno aprobado.</p>`)),

  slide({ layout: 'cards', title: 'Verificación en dos pasos (MFA)', kicker: 'Dos o más de estos elementos' },
    T(`<ul>
         <li><b>Algo que sabes</b><br>Tu contraseña.</li>
         <li><b>Algo que tienes</b><br>Tu celular o una app autenticadora.</li>
         <li><b>Algo que eres</b><br>Tu huella o tu rostro.</li>
       </ul>
       <p>Aunque un atacante consiga tu contraseña, no podrá entrar sin el segundo paso. Es uno de los controles más efectivos.</p>`)),

  slide({ layout: 'dark', title: 'Riesgos de compartir contraseñas' },
    T(`<p>Compartir tu contraseña, aunque sea con un compañero de confianza, es una falta grave. Cuando varias personas usan la misma cuenta:</p>
       ${bullets([
         'No se puede saber quién hizo qué.',
         'Se abren puertas a accesos no autorizados.',
         'Se incumplen las políticas de la empresa.',
       ])}
       <p><b>Cada persona tiene su propia cuenta y su propia contraseña.</b></p>`)),

  slide({ layout: 'callout', kicker: 'Caso práctico' },
    T('<p>Un empleado usa la misma contraseña para su correo personal y para su cuenta del trabajo. Su correo personal apareció en una filtración masiva.</p>')),

  mc('¿Qué elemento forma parte de la verificación en dos pasos (MFA)?',
    ['Tu color favorito', 'Tu contraseña', 'Tu historial de navegación', 'Tu dirección de correo'], 'b', 1),
  mc('¿Qué ocurre al compartir credenciales?',
    ['Se mejora la seguridad', 'Se pierde trazabilidad', 'Se acelera el trabajo', 'Se reduce el riesgo'], 'b', 1),
  mc('En el caso de la contraseña repetida, ¿qué debió hacer el empleado?',
    ['Usar la misma contraseña', 'Usar contraseñas únicas', 'Compartir la contraseña', 'Guardarla en un papel'], 'b', 1),
  mc('En el caso de la contraseña repetida, ¿qué debe hacer ahora?',
    ['Ignorar el incidente', 'Cambiar la contraseña', 'Desactivar MFA', 'Compartir la contraseña'], 'b', 1),
  off({ type: 'OpenResponse', points: 0, payload: { question: 'Piensa en tus contraseñas actuales. ¿Cuántas crees que has reutilizado? ¿Qué cambios puedes implementar hoy para mejorar tu seguridad?', graded: false } }),
];
