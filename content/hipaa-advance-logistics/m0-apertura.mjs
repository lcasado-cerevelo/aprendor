import { info, T, S } from './authoring.mjs';

// Apertura. Mismo arranque que el resto de los cursos del catálogo:
//   1) portada: ícono, descripción centrada y caja de resumen (módulos, tipos de pregunta, aprobación)
//   2) objetivos de aprendizaje
// Después siguen el aviso legal y la introducción del material original (diapositivas 2–3).

const ICONO = `<svg viewBox="0 0 64 64" width="84" height="84" fill="none" stroke="#1d4ed8" stroke-width="3" ` +
  `stroke-linecap="round" stroke-linejoin="round" style="display:block;margin:0 auto 14px">` +
  `<path d="M32 6l20 8v14c0 13-8.5 22-20 26C20.5 50 12 41 12 28V14z"/>` +
  `<path d="M32 22v16M24 30h16"/></svg>`;

const RESUMEN = `<div style="background:#f8fafc;border:1px solid #e2e8f0;border-radius:10px;padding:14px 16px;` +
  `margin:16px auto 0;max-width:440px;color:#334155;font-size:14px">` +
  `<div style="margin:4px 0">📦 <b>7 módulos</b></div>` +
  `<div style="margin:4px 0">📝 Selección múltiple, varias respuestas y pareo (300 puntos)</div>` +
  `<div style="margin:4px 0">✅ Aprobación: <b>70%</b></div></div>`;

export default [
  info('Cumplimiento HIPAA para transporte y logística',
    T(`${ICONO}
       <p style="text-align:center;font-size:15px;color:#475569">Qué es la información de salud protegida, por qué
       HIPAA aplica a Advance Logistics y cómo protegerla en la ruta, el vehículo y la oficina.</p>
       ${RESUMEN}`)),

  info('Objetivos de aprendizaje',
    T(`<p>Al finalizar este curso, el participante será capaz de:</p>
       <ul>
         <li><b>Identificar qué es información de salud protegida (PHI)</b> y reconocer sus 18 identificadores en los
             documentos que maneja a diario.</li>
         <li><b>Explicar por qué HIPAA le aplica a Advance Logistics</b> como asociado de negocios y qué obligaciones
             impone el contrato BAA.</li>
         <li><b>Aplicar el estándar de mínimo necesario</b> al usar o divulgar información de pacientes.</li>
         <li><b>Cumplir con las salvaguardas</b> técnicas, físicas y administrativas de la Regla de Seguridad.</li>
         <li><b>Reconocer y reportar un incumplimiento</b> (<i>breach</i>), y conocer los plazos de notificación y las
             consecuencias de no cumplir.</li>
         <li><b>Adoptar las prácticas diarias</b> de manejo del fax, contraseñas, teléfono, áreas públicas, descarte de
             documentos y redes sociales.</li>
       </ul>`)),

  info('Aviso',
    T(`<p>La información contenida en este adiestramiento se basa en fuentes que se creen precisas al momento en que
       se hizo referencia a ellas. Se ha hecho un esfuerzo razonable para garantizar la exactitud de la información
       presentada; sin embargo, no se ofrece garantía ni representación en cuanto a dicha precisión.</p>
       <p>La información contenida en este documento <strong>no constituye asesoramiento legal ni médico</strong> y
       no debe interpretarse como reglas aplicables a su situación ni como el establecimiento de un estándar de
       atención. Debido a que los hechos pueden diferir y las leyes aplicables en su jurisdicción pueden variar, si
       se requiere asistencia legal experta se deben buscar los servicios de un abogado u otro profesional legal
       competente.</p>
       <p style="${S.muted}">Contenido preparado por Marie Carmen Muntaner, Esq. — MM &amp; Associates, LLC.</p>`)),

  info('Introducción',
    T(`<p>La entrega de medicamentos es un servicio conveniente y beneficioso para los pacientes que necesitan
       acceder a sus medicamentos sin visitar la farmacia en persona. Más allá de eso, las entregas a domicilio
       pueden mejorar la adherencia al tratamiento, la satisfacción y la lealtad del paciente, así como reducir el
       abandono y el desperdicio de recetas.</p>
       <p>Pero la entrega conlleva ciertos desafíos y riesgos. Entre los principales está la protección de la
       privacidad del paciente y el cumplimiento de la <strong>Ley de Portabilidad y Responsabilidad del Seguro
       Médico de 1996</strong>, conocida como <strong>HIPAA</strong> por sus siglas en inglés.</p>
       <p>HIPAA establece estándares para la seguridad y confidencialidad de la información de salud protegida
       (PHI). Esta información incluye cualquier cosa que pueda identificar a un paciente o relacionarse con su
       condición de salud o tratamiento. Mantener protegida esa información es crucial, y la entrega a domicilio
       agrega obstáculos adicionales para cumplir con las pautas de HIPAA.</p>`)),
];
