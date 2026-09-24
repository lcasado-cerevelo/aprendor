// Láminas 10-13 del deck: proceso de la empresa, deberes, represalias y recomendaciones.
import { slide, T, bullets } from '../_shared/authoring.mjs';

export default [
  // Lámina 10 — el panel del deck dice "Hostigamiento y violencia doméstica en el empleo"
  // (errata heredada de otro deck); aquí se usa el título del panel del curso.
  slide({ layout: 'split', title: '¿Qué hará la empresa?' },
    T(`<p><b>En cumplimiento con la Política de Hostigamiento Sexual:</b></p>
       ${bullets([
         'El empleado no tiene que realizar una querella escrita, puede presentarla de manera verbal.',
         'Se comenzará con un proceso de investigación, aun cuando el empleado(a) indique que no interesa que se proceda con el mismo.',
         'Se entrevistará a todos los posibles testigos identificados por ambas partes (víctima y hostigador)',
         'De acuerdo a la información obtenida se llegará a una conclusión',
         'Se tomarán medidas disciplinarias, que pueden incluir el despido.',
         'Se tomarán medidas que garanticen un ambiente saludable',
       ])}`)),

  // Lámina 11 — panel a la derecha.
  slide({ layout: 'split', variant: 'right', title: '¿Qué debe saber todo empleado?' },
    T(bullets([
      'El patrono tiene cero tolerancia a las conductas de índole sexual',
      'Todo empleado que observe una conducta de índole sexual no deseada, tiene la responsabilidad de informarlo de manera inmediata a su supervisor',
      'El proceso de radicación de querella y de investigación se llevará a cabo de manera confidencial.',
    ]))),

  // Lámina 12 — panel a la derecha.
  slide({ layout: 'split', variant: 'right', title: 'Prohibición de Represalias' },
    T(bullets([
      ['La empresa prohíbe represalias contra una persona que:', [
        'Presenta una queja',
        'Reporta una situación',
        'Participa en una investigación',
        'Provee información o sirve como testigo',
      ]],
      'Cualquier alegación de represalia debe ser reportada inmediatamente.',
    ]))),

  // Lámina 13 — fondo oscuro con el título del panel entre dos líneas de acento.
  slide({ layout: 'split', variant: 'lines', title: 'Recomendaciones' },
    T(bullets([
      'Trate con respeto a sus compañeros y clientes',
      'No tolere conductas ofensivas en ningún momento',
      'No sienta temor en notificar una situación',
      'Acérquese a cualquier supervisor',
    ]))),
];
