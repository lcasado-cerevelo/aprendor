// Entrada, portada, objetivos y módulos 1 (la tableta y la contraseña) y 2 (la información
// que se lleva, con WhatsApp y el teléfono personal).
import { slide, intro, T, mod, mc, bullets, photo } from '../_shared/authoring.mjs';

const caso = texto => slide({ layout: 'callout', kicker: 'Caso práctico' }, T(`<p>${texto}</p>`));

export const m0 = [
  intro({
    title: 'Seguridad Digital para Choferes',
    description:
      '<p>Tu tableta, tu teléfono y WhatsApp son herramientas de trabajo, y por ellos pasa información de clientes y ' +
      'pacientes. En este adiestramiento verás cómo cuidarlos, cómo reconocer mensajes y llamadas falsas (también las ' +
      'hechas con <b>inteligencia artificial</b>), cómo usar la IA sin poner en riesgo a nadie y qué hacer si algo pasa.</p>',
    minutes: 18,
  }),

  // Portada: foto de Hazel J en Unsplash (licencia de Unsplash: uso comercial sin atribución),
  // recortada a 16:9 y reducida a 1600 px. También es la foto de la tarjeta del curso y,
  // atenuada, el fondo de la pantalla de entrada.
  slide({ layout: 'cover', title: 'Seguridad Digital para Choferes',
          photo: photo(import.meta.url, './img/portada.jpg') }),

  slide({ layout: 'cards', title: 'Lo que vas a aprender', kicker: 'Al terminar este curso podrás' },
    T(`<ul>
         <li><b>Cuidar tu tableta y tu contraseña</b> y saber qué hacer si se pierden.</li>
         <li><b>Proteger la información que llevas</b>: direcciones, órdenes y datos de pacientes.</li>
         <li><b>Usar WhatsApp con cuidado</b> en el trabajo.</li>
         <li><b>Reconocer mensajes y llamadas falsas</b>, también las hechas con IA.</li>
         <li><b>Usar la IA sin darle información</b> de la empresa ni de los clientes.</li>
         <li><b>Saber a quién avisar</b> si algo pasa.</li>
       </ul>`)),
];

export const m1 = [
  mod('Módulo 1 — Tu tableta y tu contraseña',
    'La tableta es una herramienta de trabajo y tu contraseña es la llave. Cuidarlas es lo primero.'),

  // Foto generada con Gemini (panel izquierdo de la imagen de tres).
  slide({ layout: 'photo-left', title: 'Tu tableta de trabajo', kicker: 'Tu herramienta',
          photo: photo(import.meta.url, './img/tableta.jpg'), photoPos: 'center top' },
    T(`<p>En la tableta están tus rutas, las órdenes y los datos de los clientes. Trátala como trataría la empresa un documento confidencial:</p>
       ${bullets([
         'Ponle bloqueo con PIN o huella, y bloquéala cada vez que la sueltes.',
         'No se la prestes a nadie, ni a un familiar ni a un compañero.',
         'No le instales aplicaciones que la empresa no te dio.',
         'Acepta las actualizaciones: arreglan fallas de seguridad.',
         'No la dejes a la vista dentro del vehículo.',
       ])}`)),

  slide({ layout: 'callout', title: 'Tu contraseña es solo tuya', kicker: 'Nadie te la pide' },
    T(`<p>No se la des a nadie: ni a un compañero, ni a tu supervisor, ni a alguien que diga ser de sistemas. <b>Nadie de la
       empresa te va a pedir tu contraseña por teléfono, mensaje ni correo.</b> Usa una frase larga, fácil de recordar para ti,
       y distinta de la de tus cuentas personales.</p>`)),

  slide({ layout: 'cards', title: 'Verificación en dos pasos', kicker: 'Un candado más' },
    T(`<ul>
         <li><b>Qué es</b><br>Además de la contraseña, un código de una app en tu teléfono. Si alguien roba tu contraseña, igual no puede entrar.</li>
         <li><b>En la plataforma</b><br>Cuando te la pidan, actívala. El código cambia cada 30 segundos.</li>
         <li><b>En tu WhatsApp</b><br>Actívala también: Ajustes, Cuenta, Verificación en dos pasos. Así nadie te roba la cuenta.</li>
       </ul>`)),

  slide({ layout: 'band', title: 'Si se pierde o te la roban' },
    T(`<ol>
         <li>Avisa enseguida a tu supervisor, aunque creas que la vas a encontrar.</li>
         <li>Di qué se perdió, dónde y cuándo.</li>
         <li>Si alguien pudo ver tu contraseña, cámbiala.</li>
       </ol>
       <p>Avisar rápido permite bloquear la tableta y quitarle el acceso antes de que alguien la use.</p>`)),

  caso('Un compañero te pide la tableta y tu contraseña para «terminar unas entregas» porque la suya se quedó sin batería.'),

  mc('¿Qué debes hacer cuando te bajas del vehículo y dejas la tableta?',
    ['Dejarla encendida para no perder la ruta', 'Bloquearla y no dejarla a la vista', 'Dejarla en el asiento', 'Dársela al cliente para que firme sin mirar'], 'b', 1),
  mc('Alguien llama diciendo que es de sistemas y te pide tu contraseña para «arreglar tu cuenta». ¿Qué haces?',
    ['Se la das, porque es de la empresa', 'No se la das y avisas a tu supervisor', 'Se la das si te sabe tu nombre', 'Le das solo la mitad'], 'b', 1),
  mc('En el caso del compañero sin batería, ¿qué es lo correcto?',
    ['Prestarle la tableta y tu contraseña', 'Darle solo la contraseña', 'No prestarle tu tableta ni tu contraseña; que avise al supervisor', 'Hacerle tú todas sus entregas sin decir nada'], 'c', 1),
];

export const m2 = [
  mod('Módulo 2 — La información que llevas',
    'En cada ruta llevas información de clientes y pacientes. Por WhatsApp, por la tableta o en papel, se cuida igual.'),

  slide({ layout: 'cards', title: 'Qué información llevas', kicker: 'Toda se cuida' },
    T(`<ul>
         <li><b>Direcciones y teléfonos</b><br>De clientes y pacientes.</li>
         <li><b>Órdenes y facturas</b><br>Qué se entrega, a quién y cuándo.</li>
         <li><b>Datos de salud</b><br>Un nombre junto a un medicamento o equipo médico es información protegida por HIPAA.</li>
         <li><b>Firmas y fotos de entrega</b><br>También son datos de los clientes.</li>
       </ul>`)),

  slide({ layout: 'split', title: 'WhatsApp en el trabajo' },
    T(`<p><b>Sí:</b> avisar que vas en camino, que llegaste o que hay un retraso; coordinar con tu supervisor por el medio que te indique la empresa.</p>
       <p><b>No:</b></p>
       ${bullets([
         'Mandar fotos de órdenes, etiquetas o documentos con datos de pacientes.',
         'Reenviar información de clientes a grupos o a personas que no la necesitan.',
         'Escribir el nombre de un paciente junto con lo que se le entrega.',
       ])}
       <p>Lo que mandas por WhatsApp queda en teléfonos que la empresa no controla.</p>`)),

  slide({ layout: 'callout', title: 'Las fotos se quedan en el teléfono', kicker: 'Teléfono personal' },
    T(`<p>No tomes fotos de órdenes médicas, recetas ni documentos de clientes con tu teléfono personal: se guardan en tu galería,
       se copian a tu nube y se pueden ver o enviar sin querer. Si hace falta una foto de la entrega, usa la tableta o el medio que indique la empresa.</p>`)),

  slide({ layout: 'dark', title: 'En la entrega' },
    T(bullets([
      'Entrega a la persona correcta y confirma la dirección.',
      'No dejes papeles con datos del paciente a la vista en el vehículo.',
      'No comentes el pedido de un paciente frente a vecinos u otras personas.',
      'Si algo se entregó donde no era, avísalo enseguida.',
    ]))),

  caso('Un cliente te pide por WhatsApp una foto de la orden de su vecina «para ver si ya le llegó lo suyo».'),

  mc('¿Cuál de estas es información protegida de un paciente?',
    ['El nombre de la calle', 'El nombre del paciente junto con el medicamento que se le entrega', 'La hora de tu almuerzo', 'El color del vehículo'], 'b', 1),
  mc('¿Qué sí puedes mandar por WhatsApp en el trabajo?',
    ['Una foto de la orden médica del paciente', 'El aviso de que vas llegando', 'La lista de clientes con sus medicamentos', 'La receta de un paciente'], 'b', 1),
  mc('En el caso del vecino que pide la foto de la orden, ¿qué haces?',
    ['Se la mandas, porque son vecinos', 'No se la mandas: es información de otra persona', 'Le mandas solo el nombre del medicamento', 'Se la mandas y la borras después'], 'b', 1),
];
