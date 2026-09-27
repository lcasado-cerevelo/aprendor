// Láminas 4-6 del deck: qué es, quién puede ser víctima y sus formas. La lámina 5 no tiene
// encabezado propio en el deck: va sin título.
import { slide, T, bullets } from '../_shared/authoring.mjs';

export default [
  // Lámina 4
  slide({ layout: 'split', title: '¿Qué es Violencia Doméstica?' },
    T(`<p>La violencia doméstica es un patrón de comportamiento utilizado para ejercer poder y control sobre otra
       persona dentro de una relación de pareja.</p>
       <p>Puede incluir:</p>
       ${bullets([
         'Violencia física',
         'Violencia emocional o psicológica',
         'Amenazas e intimidación',
         'Abuso económico',
         'Acecho o vigilancia',
         'Violencia sexual',
         'Control excesivo de las actividades y relaciones de la persona',
       ])}`)),

  // Lámina 5
  slide({ layout: 'split', title: '' },
    T(bullets([
      'La víctima de violencia doméstica proviene de eventos violentos entre cónyuges, excónyuges, noviazgos o relación consensual íntima',
      'No tiene género',
    ]))),

  // Lámina 6
  slide({ layout: 'split', title: 'Formas de Violencia Doméstica' },
    T(`<p>La violencia doméstica se puede manifestar de diferentes formas:</p>
       ${bullets([
         ['<b>Física</b>', ['Golpes, empujones, mordidas u otras agresiones.']],
         ['<b>Emocional o psicológica</b>', ['Humillaciones, insultos, persecución, amenazas o manipulación.']],
         ['<b>Económica</b>', ['Control del dinero en cuentas bancarias o recursos económicos de la persona.']],
         ['<b>Sexual</b>', ['Cualquier acto sexual impuesto o no consentido.']],
         ['<b>Control</b>', ['Revisar teléfonos, controlar redes sociales, rastrear ubicación o enviar mensajes amenazantes.']],
       ])}`)),
];
