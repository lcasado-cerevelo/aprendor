import {info, mod, mc, ms, T, S, fig } from './authoring.mjs';

// Módulo 5 — diapositivas 22, 25–29 y 54 del original.
export default [
  mod('Módulo 5 — Incumplimiento, notificación y consecuencias', 'Duración estimada: 8–10 minutos'),

  info('La Regla de Cumplimiento de HIPAA',
    T(`<p>A principios de 2005 se introdujo la Regla de Cumplimiento, después de que muchas entidades cubiertas no
       cumplieran plenamente con las Reglas de Privacidad y Seguridad. Esta regla permite al Departamento de Salud y
       Servicios Humanos (HHS) investigar las quejas presentadas sobre entidades que no cumplen con HIPAA, y le dio
       el poder de multar por violaciones de información electrónica protegida que se hubieran podido evitar
       siguiendo las medidas de seguridad requeridas.</p>
       <p>Bajo esta regla, la <strong>Oficina de Derechos Civiles (OCR)</strong> está facultada para imponer
       sanciones económicas contra las entidades que no cumplan. Si la información médica de una persona se comparte
       sin su permiso y le ocasiona un daño grave, esa persona puede iniciar una acción legal civil contra la
       entidad culpable.</p>`)),

  info('La Ley HITECH',
    T(`<p>La <em>Health Information Technology for Economic and Clinical Health Act</em> (HITECH) fue aprobada en
       2009 con el propósito de alentar a los proveedores de atención médica a comenzar el uso de registros
       electrónicos de salud (EHR).</p>
       <p>Ese mismo año se emitió su regla de aplicación, que creó un sistema de sanciones económicas por
       incumplimiento de HIPAA con multas potenciales mucho más altas, aumentando drásticamente el costo del
       incumplimiento.</p>
       ${fig('hitech', 'Logotipos de HIPAA y HITECH', 300)}`)),

  info('Incumplimiento o violación (breach)',
    T(`<p>Ejemplos de incumplimiento pueden ser:</p>
       <ul>
         <li>Pérdida, robo o descarte inapropiado de papel o de dispositivos que contienen información protegida.</li>
         <li>Acceso por parte de personas o programas no autorizados — por ejemplo, un virus o un <em>ransomware</em>.</li>
         <li>Compartir información confidencial, incluyendo contraseñas, y que se produzca pérdida o robo.</li>
         <li>Copiar o remover PHI del área autorizada sin permiso de la institución.</li>
         <li>Perder, extraviar o disponer indebidamente de documentos que contengan PHI.</li>
       </ul>
       <div style="${S.warn}">Cuando esto ocurre se debe <strong>investigar inmediatamente, mitigar la situación,
       documentar</strong> y luego notificar a los potencialmente afectados y a la Oficina de Derechos Civiles del
       Departamento de Salud federal, dependiendo de la magnitud de la brecha.</div>
       <p><strong>Tu primer paso siempre es el mismo: reportarlo a tu supervisor de inmediato.</strong> Un paquete
       entregado en la dirección equivocada o un manifiesto que se voló del vehículo son incidentes reportables.</p>
       ${fig('breach-alerta', 'Señal de alerta', 220)}`)),

  info('La regla de notificación de incumplimiento',
    T(`<p>En septiembre de 2009 se aprobó la regla de notificación de incumplimiento, que exige que cualquier
       infracción de e-PHI por parte de una entidad cubierta que afecte a <strong>más de 500 personas</strong> se
       informe a la OCR, y que se envíe un aviso a cualquier persona que pueda verse afectada por la infracción.</p>
       ${fig('breach-notification', 'Aviso de notificación de incumplimiento de HIPAA', 300)}`)),

  info('Notificación: los plazos',
    T(`<div style="${S.grid}">
         <div style="${S.cardS}">Las infracciones que afectan a <strong>menos de 500 personas</strong> deben
           registrarse e informarse dentro de los <strong>60 días</strong> después del cierre del año calendario en
           que se descubren.</div>
         <div style="${S.cardS}">Las infracciones de <strong>más de 500 personas</strong> deben reportarse
           inmediatamente al HHS y al medio de comunicación local.</div>
         <div style="${S.cardS}">No se requiere ningún cambio en la información que llevan las cartas de
           notificación de incumplimiento.</div>
         <div style="${S.cardS}">Se mantiene el plazo de <strong>60 días</strong> a partir de la fecha de
           descubrimiento — o de cuándo, con diligencia razonable, debió haberse descubierto.</div>
       </div>
       <p style="${S.muted}">HHS: Departamento de Salud y Servicios Humanos de EE. UU.</p>`)),

  info('Consecuencias de violar la Ley HIPAA',
    T(`<p><strong>Penalidades criminales</strong></p>
       <ul>
         <li>Multas desde $50,000 hasta $1.5 millones.</li>
         <li>Hasta 10 años de cárcel.</li>
       </ul>
       <p><strong>Penalidades civiles</strong></p>
       <ul>
         <li>Multas que van desde $100 hasta $25,000 por violación.</li>
         <li>Más multas por violaciones de varios años.</li>
       </ul>
       <div style="${S.warn}">Nota: las penalidades pueden aplicar a los individuos, pero también pueden aplicar a
       la organización o inclusive a sus oficiales.</div>
       ${fig('costo-violaciones', 'El alto costo de las violaciones de HIPAA: acuerdos y sanciones notables', 380)}`)),

  info('Vulnerabilidades comunes que resultan en infracciones',
    T(`<p>Las faltas que con más frecuencia provocan infracciones son no hacer lo siguiente:</p>
       <ul>
         <li>Orientar y capacitar al personal; proporcionar actualizaciones y educación continua.</li>
         <li>Identificar a un Oficial de Privacidad de HIPAA.</li>
         <li>Contar con políticas y procedimientos establecidos.</li>
         <li>Adherirse al estándar de mínimo necesario para acceder y divulgar PHI.</li>
         <li>Identificar a todos los socios de negocio.</li>
         <li>Obtener los contratos BAA correspondientes.</li>
         <li>Tener un proceso de respuesta a incidentes e infracciones.</li>
       </ul>`)),

  mc('¿A partir de cuántas personas afectadas una brecha debe reportarse de inmediato al HHS y al medio de comunicación local?',
    ['50', '100', '500', '1,000'], 'c'),

  mc('Una brecha afecta a 12 pacientes. ¿Qué corresponde?',
    ['No se reporta: son menos de 500',
     'Se registra y se informa dentro de los 60 días siguientes al cierre del año calendario en que se descubrió',
     'Se reporta solo si el paciente se queja',
     'Se reporta en un plazo de cinco años'], 'b'),

  mc('Entregas un paquete en la casa equivocada y el vecino lo abre. ¿Qué haces primero?',
    ['Nada, si el vecino devuelve el paquete',
     'Lo anoto y lo comento al final de la semana',
     'Lo reporto a mi supervisor de inmediato para investigar, mitigar y documentar',
     'Le pido al vecino que no diga nada'], 'c'),

  ms('Marca TODOS los casos que constituyen un incumplimiento (breach).',
    ['Botar en el zafacón común un manifiesto con nombres de pacientes',
     'Prestarle tu contraseña a un compañero para que cierre las entregas',
     'Que te roben la tableta de ruta sin cifrar',
     'Entregar el paquete al paciente correcto y verificar su identificación'], ['a', 'b', 'c']),

  mc('¿Sobre quién pueden recaer las penalidades por violar HIPAA?',
    ['Solo sobre la empresa',
     'Solo sobre el paciente que reclama',
     'Sobre el individuo que cometió la falta, y también sobre la organización y sus oficiales',
     'Solo sobre la entidad cubierta, nunca sobre el socio de negocios'], 'c'),
];
