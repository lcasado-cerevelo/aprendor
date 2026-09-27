// Módulo 1 — Introducción a la Seguridad de la Información (11 → 5 láminas).
// Salen «Temas y subtemas», «Introducción» (pasa al subtítulo del módulo), «Resumen del
// módulo» y «Comprensión del Módulo». Casos reales y costos van en una lámina. Se corrige
// la cifra del factor humano (Verizon DBIR 2024: 68 %).
import { slide, T, media, mod, mc, off, bullets } from '../_shared/authoring.mjs';

export default [
  mod('Módulo 1 — Introducción a la Seguridad de la Información',
    'No se trata solo de proteger computadoras o redes: se trata de que los datos, el activo más valioso de la empresa, se mantengan seguros, correctos y disponibles.'),

  slide({ layout: 'dark', title: '¿Qué es la seguridad de la información?' },
    T(`<p>Se basa en tres principios, conocidos como el <a href="https://www.youtube.com/watch?v=fkrsaWn5tjo" target="_blank" rel="noopener">triángulo CIA</a>:</p>
       ${bullets([
         '<b>Confidencialidad:</b> solo la ven quienes están autorizados.',
         '<b>Integridad:</b> los datos se mantienen correctos, sin cambios no autorizados.',
         '<b>Disponibilidad:</b> la información está ahí cuando se necesita.',
       ])}
       <p>Si uno falla, la seguridad se compromete: un ransomware afecta la disponibilidad y una filtración, la confidencialidad.</p>`, 'half'),
    media('/media/0dc74e51-536a-47c6-916a-a5a5288e7637', 'image/png', 'half')),

  slide({ layout: 'dark', title: 'Clasificación de la información' },
    T(`<p>Clasificar la información según qué tan sensible es permite protegerla como corresponde:</p>
       ${bullets([
         '<b>Público:</b> se puede compartir sin riesgo. Manuales publicados, el sitio web.',
         '<b>Interno:</b> solo para empleados. Políticas internas, organigramas.',
         '<b>Confidencial:</b> causa daño si se divulga. Datos personales, información financiera.',
         '<b>Altamente confidencial:</b> limitada a ciertos puestos. Contraseñas, planes estratégicos.',
       ])}`, 'half'),
    media('/media/9e3449fd-8c30-402f-8991-938a678ad5e2', 'image/png', 'half')),

  slide({ layout: 'split', title: '¿Qué es información confidencial?' },
    T(`<p>Es cualquier dato que, si se divulga sin autorización, puede causar daño económico, legal o a la reputación de la empresa.</p>
       <p>Un error común es pensar «yo no manejo información importante». Cualquier empleado puede ser la puerta de entrada de un
       atacante, aunque solo maneje datos de la operación: una dirección de entrega, una factura, una contraseña.</p>
       <p>Video: <a href="https://www.youtube.com/watch?v=g6tO3ObVvEc" target="_blank" rel="noopener">El factor humano en la seguridad informática</a></p>`)),

  slide({ layout: 'band', title: '¿Por qué ocurren los ataques?' },
    T(`<p>Los atacantes buscan un beneficio. Lo más común:</p>
       ${bullets([
         'Dinero, por ejemplo extorsionando con ransomware.',
         'Información valiosa para venderla.',
         'Espionaje entre empresas de la competencia.',
         'Sabotaje, para afectar la operación o la reputación.',
       ])}
       <p><b>Alrededor de 2 de cada 3 incidentes involucran un error o una decisión humana</b> (68 % según el informe
       Verizon DBIR 2024). El ejemplo más común es el <a href="https://www.youtube.com/watch?v=s6xbg6jbTKo" target="_blank" rel="noopener">phishing</a>.
       Por eso la capacitación es una de las mejores defensas.</p>`)),

  slide({ layout: 'photo-left', title: 'Casos reales de empresas afectadas', kicker: 'Casos reales',
          photo: '/media/98930468-a76e-4f4b-8a7e-cdc31e047314' },
    T(`${bullets([
         '<b>Equifax (2017):</b> una vulnerabilidad sin corregir expuso los datos de 147 millones de personas.',
         '<b>Colonial Pipeline (2021):</b> un ransomware detuvo el suministro de combustible en la costa este de EE. UU.',
         '<b>Uber (2022):</b> un atacante entró a sistemas internos engañando a un empleado.',
       ])}
       <p>Además de reparar los sistemas, un incidente trae multas, demandas, pérdida de clientes y daño a la marca.</p>`)),

  slide({ layout: 'callout', kicker: 'Caso práctico' },
    T('<p>Un empleado descargó información de clientes en un USB personal para «trabajar desde casa». El USB no está cifrado y se perdió en un taxi.</p>')),

  mc('El manual de usuario publicado en la página web de la empresa es…',
    ['Confidencial', 'Interno', 'Público', 'Altamente confidencial'], 'c', 1),
  mc('El número de cuenta bancaria de un cliente es…',
    ['Público', 'Confidencial', 'Interno', 'No clasificado'], 'b', 1),
  mc('En el caso del USB perdido, ¿qué tipo de información está comprometida?',
    ['Información pública', 'Información de clientes', 'Información irrelevante', 'Información de marketing'], 'b', 1),
  mc('En el caso del USB perdido, ¿qué principio del triángulo CIA se violó?',
    ['Integridad', 'Disponibilidad', 'Confidencialidad', 'Ninguno'], 'c', 1),
  off({ type: 'OpenResponse', points: 0, payload: { question: '¿Qué tipo de información manejas en tu trabajo que podría considerarse confidencial? ¿Qué medidas aplicas para protegerla?', graded: false } }),
];
