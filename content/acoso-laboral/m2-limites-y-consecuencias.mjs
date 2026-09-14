import { info, T, S, badgeList } from '../_shared/authoring.mjs';

export default [
  info('Qué no se considera acoso',
    T(`${badgeList([
         'Actos destinados a ejercer la potestad disciplinaria que legalmente corresponde a los supervisores sobre sus subalternos',
         'Exigir que el empleado cumpla con sus responsabilidades',
         'Promulgación de reglamentos y políticas con el fin de maximizar la eficiencia del negocio',
       ], '#16a34a')}`)),

  info('Conducta reiterada, frecuente y persistente',
    T(`<p>Uno de los elementos importantes del concepto de acoso laboral bajo la Ley 90-2020 es que se trata de
       conductas <b>reiteradas, frecuentes y persistentes</b>.</p>
       <div style="${S.call}">Una situación aislada puede ser inapropiada y requerir atención, pero no toda
       conducta inapropiada constituye automáticamente acoso laboral.</div>`)),

  info('¿Qué hará el patrono?',
    T(`<div style="${S.grid}">
         <div style="${S.card}">🔍 Investigar las alegaciones de acoso laboral</div>
         <div style="${S.card}">🛠️ Tomar medidas correctivas y/o preventivas para garantizar el respeto y el orden</div>
         <div style="${S.card}">⚖️ Tomar las medidas disciplinarias necesarias, incluyendo el despido</div>
         <div style="${S.card}">🛡️ Garantizar un ambiente libre de represalias por presentar una queja</div>
       </div>`)),
];
