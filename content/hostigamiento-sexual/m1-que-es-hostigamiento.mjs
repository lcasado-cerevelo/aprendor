import { info, T, S, img, fig, quote, chips } from '../_shared/authoring.mjs';

export default [
  info('¿Qué es el hostigamiento sexual bajo la Ley Núm. 17?',
    T(`${img('https://images.unsplash.com/photo-1758873269276-9518d0cb4a0b?fm=jpg&q=60&w=1200&auto=format&fit=crop',
        'Compañeros de trabajo interactuando de forma profesional en la oficina',
        'Foto: Vitaly Gariev / Unsplash')}
       ${quote('Una conducta de naturaleza sexual no deseada que afecta o interfiere con el ambiente de trabajo.', '#a21caf')}
       ${chips(['Verbal', 'Física', 'Visual', 'Por medios electrónicos'], '#a21caf')}
       <p>Puede involucrar a compañeros, supervisores, gerentes, clientes, suplidores u otras personas
       relacionadas con el trabajo.</p>
       <div style="${S.call}">No requiere contacto físico.</div>`)),

  info('Formas del hostigamiento sexual',
    T(`<div style="${S.cols2}">
         <div>
           <p><b>Quid pro quo</b></p>
           <p>Ocurre cuando se condiciona un beneficio o decisión laboral a cambio de favores sexuales.</p>
           <p style="${S.muted}">Ejemplo: "Si sales conmigo, puedo recomendarte para el ascenso."</p>
         </div>
         <div>
           <p><b>Ambiente hostil</b></p>
           <p>Conductas sexuales no deseadas que crean un ambiente intimidante, ofensivo, humillante o incómodo.
           Puede surgir por comentarios, imágenes, mensajes, bromas o conductas repetitivas.</p>
         </div>
       </div>`)),

  info('¿Cómo se puede manifestar?',
    T(`${fig(import.meta.url, './img/incomodidad.jpg',
        'Ilustración de una sombra amenazante e intimidante detrás de una persona',
        'Ilustración: Fast Ink / Unsplash')}
       <p>Algunos ejemplos de hostigamiento sexual:</p>
       <ul>
         <li>Besos, pellizcos, apretones</li>
         <li>Piropos</li>
         <li>Comentar sobre lo bien que se ve la persona o hacer comentarios sobre alguna parte de su cuerpo</li>
         <li>Hacer chistes o bromas de contenido sexual</li>
         <li>Conductas de índole sexual en presencia de otras personas que incomoden a quien no es directamente
             hostigada</li>
         <li>Preguntas sobre la vida sexual de la persona</li>
         <li>Envío de reels, videos o memes con contenido sexual</li>
         <li>Uso de aplicaciones como WhatsApp y redes sociales para enviar acercamientos directos o indirectos</li>
         <li>Mensajes con emojis de connotación sexual</li>
         <li>Contacto físico no deseado</li>
         <li>Otras conductas de índole sexual no deseada</li>
       </ul>`)),
];
