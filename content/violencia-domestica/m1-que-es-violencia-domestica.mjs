import { info, T, S, fig, quote, chips } from '../_shared/authoring.mjs';

export default [
  info('¿Qué es violencia doméstica?',
    T(`${fig(import.meta.url, './img/apoyo.jpg',
        'Una persona apoya la mano sobre el hombro de una compañera preocupada',
        'Foto de referencia')}
       ${quote('Un patrón de comportamiento utilizado para ejercer poder y control sobre otra persona dentro de una relación de pareja.', '#9f1239')}
       <p>Puede incluir:</p>
       ${chips([
         'Violencia física', 'Violencia emocional o psicológica', 'Amenazas e intimidación', 'Abuso económico',
         'Acecho o vigilancia', 'Violencia sexual', 'Control excesivo',
       ], '#9f1239')}`)),

  info('¿Quién puede ser víctima?',
    T(`${fig(import.meta.url, './img/sola.jpg',
        'Una persona sentada sola en una escalinata, cabizbaja',
        'Foto: Zhivko Minkov / Unsplash')}
       <p>La víctima de violencia doméstica proviene de eventos violentos entre cónyuges, excónyuges, noviazgos
       o relación consensual íntima.</p>
       <div style="${S.call}"><b>No tiene género.</b> Cualquier persona, independientemente de su género, puede
       ser víctima de violencia doméstica.</div>`)),

  info('Formas de violencia doméstica',
    T(`<div style="${S.grid}">
         <div style="background:#fff1f2;border:1px solid #fecdd3;border-radius:10px;padding:12px 14px">
           <b>Física</b><br><span style="font-size:14px;color:#475569">Golpes, empujones, mordidas u otras agresiones.</span>
         </div>
         <div style="background:#fff1f2;border:1px solid #fecdd3;border-radius:10px;padding:12px 14px">
           <b>Emocional o psicológica</b><br><span style="font-size:14px;color:#475569">Humillaciones, insultos, persecución, amenazas o manipulación.</span>
         </div>
         <div style="background:#fff1f2;border:1px solid #fecdd3;border-radius:10px;padding:12px 14px">
           <b>Económica</b><br><span style="font-size:14px;color:#475569">Control del dinero en cuentas bancarias o recursos económicos.</span>
         </div>
         <div style="background:#fff1f2;border:1px solid #fecdd3;border-radius:10px;padding:12px 14px">
           <b>Sexual</b><br><span style="font-size:14px;color:#475569">Cualquier acto sexual impuesto o no consentido.</span>
         </div>
         <div style="background:#fff1f2;border:1px solid #fecdd3;border-radius:10px;padding:12px 14px">
           <b>Control</b><br><span style="font-size:14px;color:#475569">Revisar teléfonos, controlar redes sociales, rastrear ubicación o enviar mensajes amenazantes.</span>
         </div>
       </div>`)),
];
