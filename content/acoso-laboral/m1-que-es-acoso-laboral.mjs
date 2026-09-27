// Láminas 4-8 del deck: definición, la Ley 90-2020, conductas, lo que no es acoso y la
// reiteración. Las láminas 5, 6 y 8 no tienen encabezado propio en el deck: van sin título.
import { slide, T, bullets } from '../_shared/authoring.mjs';

export default [
  // Lámina 4
  slide({ layout: 'split', title: '¿Qué es el acoso laboral?' },
    T(`<p>Es aquella conducta malintencionada, no deseada, <u>repetitiva</u> y abusiva que:</p>
       ${bullets([
         'Atenta contra la reputación y la vida privada o familiar.',
         'Crea un entorno de trabajo intimidante, humillante, hostil u ofensivo.',
       ])}`)),

  // Lámina 5
  slide({ layout: 'split', title: '' },
    T(bullets([
      'La Ley Núm. 90-2020 establece una política pública para prohibir y prevenir el acoso laboral en Puerto Rico.',
      ['La Ley reconoce que el acoso laboral puede ocurrir:', [
        'Entre supervisor y empleado',
        'Entre empleados del mismo nivel',
        'Incluso cuando existe una relación donde la persona acosadora ocupa una posición inferior',
      ]],
      'El acoso laboral no depende necesariamente de una relación jerárquica',
    ]))),

  // Lámina 6
  slide({ layout: 'split', title: '' },
    T(`<p>Algunas conductas que pueden constituir acoso laboral incluyen:</p>
       ${bullets([
         'Expresiones injuriosas, difamatorias o lesivas.',
         'Palabras soeces dirigidas hacia una persona.',
         'Comentarios hostiles o humillantes sobre su desempeño profesional.',
         'Amenazas injustificadas de despido.',
         'Descalificación humillante de sus opiniones o propuestas de trabajo.',
         'Burlas o comentarios humillantes sobre su apariencia.',
         'Exponer públicamente asuntos relacionados con la intimidad personal o familiar.',
       ])}
       <p>La Ley establece que esta lista <b>no es exclusiva</b>.</p>`)),

  // Lámina 7 — en el deck el «no» va subrayado; el título de la lámina es texto plano.
  slide({ layout: 'split', title: 'Qué no se considera acoso' },
    T(bullets([
      'Actos destinados a ejercer la potestad disciplinaria que legalmente corresponde a los supervisores sobre sus subalternos.',
      'Exigir que el empleado cumpla con sus responsabilidades.',
      'Promulgación de reglamentos y políticas con el fin de maximizar la eficiencia del negocio.',
    ]))),

  // Lámina 8
  slide({ layout: 'split', title: '' },
    T(bullets([
      'Uno de los elementos importantes del concepto de acoso laboral bajo la Ley 90-2020 es que se trata de conductas reiteradas, frecuentes y persistentes.',
      'Una situación aislada puede ser inapropiada y requerir atención, pero no toda conducta inapropiada constituye automáticamente acoso laboral.',
    ]))),
];
