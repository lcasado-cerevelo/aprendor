// Módulo 4 — Phishing e Ingeniería Social (9 → 6 láminas).
// Phishing se acorta (spear phishing y whaling en una línea); smishing queda con el ejemplo
// del paquete, el más útil para choferes; «¿Qué hacer si ya hiciste clic?» sale (está en el
// módulo 8); los dominios falsos quedan aquí y salen del módulo 5.
import { slide, T, mod, mc, off, bullets } from '../_shared/authoring.mjs';

export default [
  mod('Módulo 4 — Phishing e Ingeniería Social',
    'En vez de atacar sistemas complicados, los atacantes van por el eslabón más fácil: las personas.'),

  slide({ layout: 'dark', title: '¿Qué es el phishing?' },
    T(`<p>Un intento de engañarte para que entregues información haciéndose pasar por alguien de confianza. Casi siempre llega por correo. Suele:</p>
       ${bullets([
         'Meter prisa: «Tu cuenta será suspendida hoy».',
         'Traer enlaces a páginas falsas que imitan las reales.',
         'Tener errores de ortografía.',
         'Pedir contraseñas, pagos o datos personales.',
         'Usar logos de empresas conocidas.',
       ])}
       <p>Hay versiones dirigidas: el atacante investiga a la víctima y usa su nombre, su puesto o sus proyectos para que el mensaje parezca real.</p>`)),

  slide({ layout: 'split', title: 'Smishing: phishing por mensaje de texto' },
    T(`<p>Llega por SMS, WhatsApp o Telegram, con un enlace o un mensaje que asusta:</p>
       <p><b>«Su paquete no pudo ser entregado. Verifique aquí.»</b></p>
       <p>«Su cuenta será bloqueada. Actualice su información.»</p>
       <p>Los mensajes de texto inspiran más confianza que el correo, y por eso funcionan. Si no esperabas el mensaje, no toques el enlace.</p>`)),

  slide({ layout: 'band', title: 'Vishing: phishing por llamada telefónica' },
    T(`<p>El atacante llama haciéndose pasar por soporte técnico, un banco, un supervisor o un proveedor, con prisa y tono de autoridad.
       Busca datos o que instales un programa.</p>
       <p><b>El número que ves en pantalla se puede falsificar:</b> puede parecer de tu banco o una extensión interna. Cuelga y llama tú al número oficial.</p>`)),

  slide({ layout: 'dark', title: 'Ingeniería social en redes sociales' },
    T(`<p>Antes de atacar, los criminales investigan: publicaciones, fotos, amigos, lugares y datos de tu trabajo. Con eso crean perfiles falsos,
       mandan enlaces o se hacen pasar por alguien que conoces.</p>
       <p><b>Cuanta más información compartes, más fácil es atacarte.</b></p>`)),

  slide({ layout: 'cards', title: 'Señales de alerta', kicker: 'Si algo parece extraño, probablemente lo es' },
    T(`<ul>
         <li>Te piden información que no esperabas.</li>
         <li>El mensaje apela al miedo o a la prisa.</li>
         <li>El enlace no coincide con el dominio oficial.</li>
         <li>Trae un adjunto que no pediste.</li>
       </ul>
       <p><b>Dominios falsos:</b> micr0soft.com, paypa1.com, amaz0n-support.com. Una sola letra hace la diferencia.</p>`)),

  slide({ layout: 'callout', kicker: 'Caso práctico' },
    T('<p>Recibes un correo que dice: «Tu cuenta será suspendida hoy. Haz clic aquí para verificar tu identidad». El remitente parece legítimo, pero no habías pedido nada.</p>')),

  mc('¿Cuál de las siguientes es una señal común de un correo de phishing?',
    ['Un saludo personalizado', 'Errores ortográficos', 'Un mensaje interno de la empresa', 'Un adjunto esperado'], 'b', 1),
  mc('¿Qué es smishing?',
    ['Phishing por correo', 'Phishing por mensaje de texto', 'Phishing por llamada', 'Phishing por redes sociales'], 'b', 1),
  mc('¿Qué es vishing?',
    ['Phishing por llamada telefónica', 'Phishing por redes sociales', 'Phishing por correo', 'Phishing por USB'], 'a', 1),
  mc('En el caso de la «cuenta suspendida», ¿qué control ayuda a prevenir daño si caes en el enlace?',
    ['Antivirus', 'MFA', 'Redes sociales', 'USB cifrado'], 'b', 1),
  off({ type: 'OpenResponse', points: 0, payload: { question: '¿Alguna vez recibiste un mensaje que ahora reconoces como intento de phishing? ¿Qué señales recuerdas y cómo habrías actuado diferente con lo aprendido?', graded: false } }),
];
