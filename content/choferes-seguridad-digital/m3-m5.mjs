// Módulos 3 (mensajes y llamadas falsas, con voces clonadas con IA), 4 (la IA y tú) y
// 5 (si algo pasa), y la lámina de recursos adicionales.
import { slide, T, mod, mc, bullets, heading, photo } from '../_shared/authoring.mjs';

const caso = texto => slide({ layout: 'callout', kicker: 'Caso práctico' }, T(`<p>${texto}</p>`));

export const m3 = [
  mod('Módulo 3 — Mensajes y llamadas falsas',
    'Los estafadores escriben y llaman haciéndose pasar por alguien de confianza. Hoy también usan inteligencia artificial.'),

  slide({ layout: 'split', title: 'Mensajes falsos' },
    T(`<p>Llegan por mensaje de texto, WhatsApp o correo, con prisa y un enlace:</p>
       <p><b>«Su paquete no pudo ser entregado. Verifique aquí.»</b></p>
       <p>«Su cuenta será bloqueada. Actualice sus datos.»</p>
       <p>No toques el enlace. Si tienes duda, pregunta a tu supervisor por el medio de siempre, no respondiendo el mensaje.</p>`)),

  slide({ layout: 'callout', title: 'Nunca compartas el código de WhatsApp', kicker: 'El truco más común' },
    T(`<p>Te escribe «un conocido» o «soporte» pidiéndote un código de 6 dígitos que te llegó por mensaje. <b>Ese código es la llave
       de tu cuenta:</b> si lo das, te roban el WhatsApp y le escriben a tus contactos pidiendo dinero. Nadie legítimo te lo pide.</p>`)),

  slide({ layout: 'band', title: 'Llamadas falsas y voces clonadas con IA' },
    T(`<p>Te llaman haciéndose pasar por tu supervisor, un banco o un familiar. Con inteligencia artificial pueden <b>copiar la voz
       de una persona</b> a partir de un audio corto, como una nota de voz o un video.</p>
       <p>Si te piden algo raro o urgente (un código, una contraseña, dinero, cambiar una entrega), <b>cuelga y llama tú</b> al número
       que ya conoces de esa persona.</p>`)),

  slide({ layout: 'cards', title: 'Señales de alerta', kicker: 'Si algo parece raro, probablemente lo es' },
    T(`<ul>
         <li><b>Prisa</b><br>«Hazlo ya o se bloquea».</li>
         <li><b>Piden códigos o contraseñas</b><br>Nadie legítimo los pide.</li>
         <li><b>Enlaces raros</b><br>Direcciones que no son las de siempre.</li>
         <li><b>Número desconocido</b><br>O conocido pero con una petición extraña.</li>
       </ul>`)),

  caso('Recibes una nota de voz de WhatsApp con la voz de tu supervisor: «Cambia la entrega de hoy a esta otra dirección y no llames, estoy en una reunión».'),

  mc('Te llega un mensaje: «Su paquete no pudo ser entregado, verifique aquí». ¿Qué haces?',
    ['Tocas el enlace para ver', 'No tocas el enlace y, si hay duda, preguntas por el medio de siempre', 'Lo reenvías a tus compañeros', 'Respondes con tus datos'], 'b', 1),
  mc('Alguien te pide por WhatsApp el código de 6 dígitos que te acaba de llegar. ¿Qué es?',
    ['Un trámite normal', 'Un intento de robarte la cuenta de WhatsApp', 'Un mensaje de la empresa', 'Un regalo'], 'b', 1),
  mc('En el caso de la nota de voz del supervisor, ¿qué es lo correcto?',
    ['Cambiar la entrega enseguida', 'Llamar tú al número que conoces de tu supervisor para confirmar', 'Responder la nota de voz con la dirección nueva', 'Ignorar la ruta del día'], 'b', 1),
];

export const m4 = [
  mod('Módulo 4 — La inteligencia artificial y tú',
    'La inteligencia artificial ya está en el teléfono y en WhatsApp. Úsala a tu favor sin darle información que no le toca.'),

  // Foto de Josh Sorenson en Unsplash (licencia de Unsplash), recortada a la pantalla.
  slide({ layout: 'photo-left', title: '¿Qué es la inteligencia artificial?', kicker: 'Ya está a tu alrededor',
          photo: photo(import.meta.url, './img/ia-carro.jpg'), photoPos: 'right center' },
    T(`<p>Son programas que escriben, responden preguntas, traducen o crean imágenes como si fueran una persona. Algunos conocidos:
       ChatGPT, Gemini, Copilot, y <b>Meta AI, que ya viene dentro de WhatsApp</b>.</p>
       <p>No piensan ni entienden como una persona: predicen respuestas a partir de lo que aprendieron. Por eso a veces se equivocan.</p>
       <p>También está en los carros: la navegación que calcula la ruta y los sistemas que ayudan a manejar.</p>`)),

  slide({ layout: 'cards', title: 'Para qué te puede servir', kicker: 'Sin datos de nadie' },
    T(`<ul>
         <li><b>Escribir un mensaje</b><br>«Ayúdame a avisar un retraso de forma amable».</li>
         <li><b>Traducir</b><br>Instrucciones o un mensaje en inglés.</li>
         <li><b>Entender algo</b><br>Qué significa una palabra o una instrucción.</li>
       </ul>`)),

  slide({ layout: 'split', title: 'Lo que nunca le das a una IA' },
    T(`<p>Todo lo que le escribes a una IA sale de tu teléfono y de la empresa, y no sabes quién lo puede ver después.</p>
       ${bullets([
         'Nombres, direcciones o teléfonos de clientes.',
         'Datos de pacientes o fotos de órdenes y recetas.',
         'Contraseñas o códigos.',
         'Información de la empresa: rutas, precios, documentos.',
       ])}
       <p>Esto incluye a <b>Meta AI dentro de WhatsApp</b>.</p>`)),

  slide({ layout: 'callout', title: 'La IA se equivoca con seguridad', kicker: 'Verifica siempre' },
    T(`<p>Puede inventar direcciones, horarios o instrucciones y decirlos como si fueran ciertos. Lo que importa para tu trabajo
       lo confirmas con tu supervisor o con la información oficial de la empresa, no con la IA.</p>`)),

  caso('Para ahorrar tiempo, un chofer le pide a la IA de WhatsApp que ordene sus entregas y le pega la lista con los nombres y direcciones de los clientes.'),

  mc('¿Qué es Meta AI en WhatsApp?',
    ['Un contacto de la empresa', 'Un asistente de inteligencia artificial', 'Un grupo de trabajo', 'Una aplicación de mapas'], 'b', 1),
  mc('¿Qué SÍ le puedes pedir a una IA?',
    ['Que ordene tu lista de clientes con sus direcciones', 'Que te ayude a redactar un aviso de retraso sin datos de nadie', 'Que guarde tu contraseña', 'Que lea la receta de un paciente'], 'b', 1),
  mc('En el caso de la lista de entregas pegada en la IA, ¿qué pasó?',
    ['Nada, la IA es de confianza', 'Se sacó información de clientes de la empresa sin autorización', 'Se ahorró tiempo sin riesgo', 'Se protegió la información'], 'b', 1),
];

export const m5 = [
  mod('Módulo 5 — Si algo pasa',
    'Equivocarse le pasa a cualquiera. Lo que causa el daño es esconderlo o avisar tarde.'),

  slide({ layout: 'cards', title: '¿Qué hay que avisar?', kicker: 'Aunque no estés seguro' },
    T(`<ul>
         <li>Tocaste un enlace sospechoso.</li>
         <li>Diste un código o tu contraseña.</li>
         <li>Perdiste la tableta o el teléfono de trabajo.</li>
         <li>Mandaste información a quien no era.</li>
         <li>La tableta hace cosas raras.</li>
         <li>Te robaron la cuenta de WhatsApp.</li>
       </ul>`)),

  slide({ layout: 'band', title: 'Qué hacer' },
    T(`<ol>
         <li>No lo escondas ni intentes arreglarlo solo.</li>
         <li>Avisa enseguida a tu supervisor.</li>
         <li>Cuenta qué pasó, cuándo y qué tocaste o enviaste.</li>
         <li>Si diste tu contraseña, cámbiala en cuanto puedas.</li>
       </ol>`)),

  slide({ layout: 'callout', title: 'No existe el error tonto', kicker: 'Avisar protege a todos' },
    T(`<p>Avisar rápido no es un castigo: permite bloquear el equipo, cambiar accesos y evitar que el problema crezca.</p>`)),

  caso('Tocaste por error el enlace de un mensaje de «paquete no entregado» y la página te pidió tu usuario y contraseña. Los escribiste.'),

  mc('¿Qué haces primero si perdiste la tableta?',
    ['Esperar unos días a ver si aparece', 'Avisar enseguida a tu supervisor', 'Comprar otra sin decir nada', 'Nada, tiene contraseña'], 'b', 1),
  mc('¿Cuál de estas situaciones hay que avisar?',
    ['Mandaste por error la dirección de un cliente a otra persona', 'Terminaste tu ruta a tiempo', 'Cargaste la tableta', 'Llegó un cliente contento'], 'a', 1),
  mc('En el caso del enlace donde escribiste tu contraseña, ¿qué debes hacer?',
    ['Nada, seguramente no pasa nada', 'Avisar enseguida a tu supervisor y cambiar tu contraseña', 'Borrar el mensaje y olvidarlo', 'Esperar a ver si alguien se queja'], 'b', 1),

  // Lámina final antes del resultado. Enlaces verificados el 27 sep 2026.
  slide({ layout: 'dark', title: 'Recursos adicionales' },
    T(`${heading('Recursos adicionales')}
       <p>Material complementario, opcional:</p>
       ${bullets([
         '<a href="https://faq.whatsapp.com/1920866721452534/?locale=es_LA" target="_blank" rel="noopener">Cómo activar la verificación en dos pasos en WhatsApp</a> — ayuda oficial de WhatsApp.',
         '<a href="https://consumidor.ftc.gov/articulos/como-reconocer-y-evitar-las-estafas-de-phishing" target="_blank" rel="noopener">Cómo reconocer y evitar las estafas de phishing</a> — Comisión Federal de Comercio (FTC), en español.',
         '<a href="https://consumidor.ftc.gov/alertas-para-consumidores/2024/09/operacion-ai-comply-como-detectar-fraudes-y-enganos-con-inteligencia-artificial-aplicada" target="_blank" rel="noopener">Cómo detectar fraudes con inteligencia artificial</a> — FTC, en español.',
       ])}
       <p><b>Recuerda:</b> si algo no se ve bien, avisa enseguida a tu supervisor.</p>`)),
];
