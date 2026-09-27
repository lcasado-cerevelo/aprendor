// Láminas 9-11 del deck: qué hace el empleado, qué hará el patrono y la prevención.
import { slide, T, bullets } from '../_shared/authoring.mjs';

export default [
  // Lámina 9 — los tres pasos van numerados, como en el deck.
  slide({ layout: 'split', title: '¿Qué debe hacer un empleado?' },
    T(`<p>Si una persona entiende que está experimentando una situación de acoso laboral:</p>
       <ol>
         <li><b>Reconozca la conducta</b><br>Identifique qué ocurrió</li>
         <li><b>Utilice los canales establecidos</b><br>Reporte la situación ante su supervisor, Administrador o Recursos Humanos.</li>
         <li><b>Coopere con la investigación</b><br>Proporcione información veraz y relevante.</li>
       </ol>`)),

  // Lámina 10 — se corrige la errata «entono» del deck.
  slide({ layout: 'split', title: '¿Qué hará el patrono?' },
    T(bullets([
      'Investigar las alegaciones de acoso laboral.',
      'Tomar las medidas correctivas y/o preventivas que sean necesarias para garantizar el respeto y el orden en el entorno laboral.',
      'Tomar las medidas disciplinarias que sean necesarias, incluyendo el despido.',
      'Garantizar un ambiente libre de represalias por presentar una queja de hostigamiento laboral.',
    ]))),

  // Lámina 11
  slide({ layout: 'split', title: 'Prevenir el acoso laboral es responsabilidad de todos' },
    T(`<p>Cada empleado debe:</p>
       ${bullets([
         'Tratar a los demás con respeto.',
         'Evitar conductas humillantes, intimidantes u hostiles.',
         'No participar ni fomentar conductas de acoso.',
         'Reportar situaciones que puedan constituir acoso laboral.',
         'Cooperar con las investigaciones.',
         'Mantener la confidencialidad de la información que corresponda.',
       ])}`)),
];
