// Láminas 4-6 del deck: definición, formas y manifestaciones.
import { slide, T, bullets } from '../_shared/authoring.mjs';

export default [
  // Lámina 4
  slide({ layout: 'split', title: '¿Qué es el Hostigamiento Sexual bajo la Ley Núm. 17?' },
    T(bullets([
      'Es una conducta de naturaleza sexual <b>no deseada</b> que afecta o interfiere con el ambiente de trabajo.',
      'Puede ocurrir de forma verbal, física, visual o mediante medios electrónicos.',
      'Puede involucrar a compañeros, supervisores, gerentes, clientes, suplidores u otras personas relacionadas con el trabajo.',
      'No requiere contacto físico.',
    ]))),

  // Lámina 5
  slide({ layout: 'split', title: 'Formas del hostigamiento sexual' },
    T(bullets([
      ['<b>Quid Pro Quo</b>', [
        'Ocurre cuando se condiciona un beneficio o decisión laboral a cambio de favores sexuales.',
        'Ejemplo: “Si sales conmigo, puedo recomendarte para el ascenso.”',
      ]],
      ['<b>Ambiente Hostil</b>', [
        'Conductas sexuales no deseadas que crean un ambiente intimidante, ofensivo, humillante o incómodo.',
        'Puede surgir por comentarios, imágenes, mensajes, bromas o conductas repetitivas.',
      ]],
    ]))),

  // Lámina 6 — en el deck el panel va a la derecha (contenido sobre oscuro).
  slide({ layout: 'split', variant: 'right', title: '¿Cómo se puede manifestar el hostigamiento sexual?' },
    T(`<p><b>Algunos ejemplos de hostigamiento sexual:</b></p>
       ${bullets([
         'Besos, pellizcos, apretones',
         'Piropos',
         'Comentar sobre lo bien que se ve la persona o hacer comentarios sobre alguna parte de su cuerpo',
         'Hacer chistes o bromas de contenido sexual',
         'Conductas de índole sexual en presencia de otras que incomode a la persona que no es directamente hostigada',
         'Preguntas sobre la vida sexual de la persona',
         'Envío de reels, videos, memes con contenido sexual',
         'Uso de aplicaciones como WhatsApp y redes sociales para enviar acercamientos directos o indirectos.',
         'Mensajes con emojis de connotación sexual',
         'Contacto físico no deseado',
         'Otras conductas de índole sexual no deseada',
       ])}`)),
];
