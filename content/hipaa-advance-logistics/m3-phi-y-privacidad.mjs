import { info, mod, mc, ms, T, S } from './authoring.mjs';

// Módulo 3 — diapositivas 15–19 y 24 del original.
export default [
  mod('Módulo 3 — La PHI y la Regla de Privacidad', 'Duración estimada: 7–9 minutos'),

  info('HIPAA establece estándares mínimos para:',
    T(`<div style="${S.grid}">
         <div style="${S.card}"><strong>Confidencialidad</strong><br>Evitar divulgaciones no autorizadas: la
           información solo debe estar accesible a quienes tienen autoridad y necesidad de saber.</div>
         <div style="${S.card}"><strong>Integridad</strong><br>Evitar alteraciones que atenten contra la
           credibilidad, certidumbre y confianza sobre la información de salud.</div>
         <div style="${S.card}"><strong>Disponibilidad</strong><br>Que la información esté disponible en todo
           momento, en el lugar donde hace falta.</div>
         <div style="${S.card}"><strong>Políticas y procedimientos</strong><br>Documentos internos que atiendan los
           requisitos de privacidad y seguridad.</div>
       </div>`)),

  info('La Regla de Privacidad y la PHI',
    T(`<p>Un hito importante en la historia de HIPAA fue la Regla de Privacidad, propuesta por primera vez en 1999.
       Esta regla gira en torno a los estándares relacionados con la salvaguarda de la información de salud
       protegida (PHI).</p>
       <div style="${S.call}"><strong>PHI</strong> es cualquier información dentro del expediente médico de una
       persona que pueda identificarla y que esté en manos de una entidad cubierta o de su socio de negocios.</div>
       <p>Bajo HIPAA y la Regla de Privacidad hay <strong>18 identificadores específicos</strong> que deben manejarse
       con salvaguardas.</p>`)),

  info('Los 18 identificadores que se consideran PHI',
    T(`<div style="${S.cols2}">
         <ol>
           <li>Nombre</li>
           <li>Dirección (cualquier dato más localizado que el estado)</li>
           <li>Cualquier fecha relacionada con el individuo, excepto el año: cumpleaños, fecha de muerte, fecha de
               admisión o alta</li>
           <li>Número de teléfono</li>
           <li>Número de fax</li>
           <li>Dirección de correo electrónico</li>
           <li>Número de Seguro Social</li>
           <li>Número de expediente médico</li>
           <li>Número de beneficiario del plan de salud</li>
         </ol>
         <ol start="10">
           <li>Número de cuenta</li>
           <li>Número de certificado o licencia</li>
           <li>Identificadores de vehículos, números de serie y números de matrícula</li>
           <li>Identificadores de dispositivos y números de serie</li>
           <li>Direcciones web (URL)</li>
           <li>Dirección IP</li>
           <li>Identificadores biométricos, como huellas dactilares o de voz</li>
           <li>Fotos de cara completa</li>
           <li>Cualquier otro número, característica o código de identificación único</li>
         </ol>
       </div>
       <div style="${S.call}">En nuestra operación, el <strong>manifiesto de entrega</strong> ya contiene varios de
       estos identificadores a la vez: nombre, dirección, teléfono y, muchas veces, el nombre del medicamento. Ese
       papel es PHI.</div>`)),

  info('Reglas básicas sobre la divulgación de información médica protegida',
    T(`<p>La información de los pacientes puede divulgarse sin autorización si el propósito es
       <strong>tratamiento, pago u operaciones de atención médica</strong>.</p>
       <p>La divulgación de PHI para cualquier cosa que no sea tratamiento, pago u operaciones de atención médica
       <strong>requiere una autorización completada</strong>.</p>
       <p>Existen ciertas excepciones para actividades de monitoreo de salud pública (por ejemplo, informes de
       enfermedades), supervisión gubernamental y algunas investigaciones de aplicación de la ley. El personal
       siempre debe consultar con el Oficial de Privacidad antes de divulgar.</p>`)),

  info('El estándar de mínimo necesario',
    T(`<p style="${S.lead}">Siempre que se use o divulgue PHI, ya sea a otra entidad cubierta o a un socio de
       negocios, <strong>solo se debe divulgar la información necesaria para lograr el propósito previsto</strong>.</p>
       <div style="${S.cardS}"><strong>Ejemplo:</strong> la práctica utiliza una agencia de cobro que solicitó
       información de facturación de varios pacientes. La práctica envía la información de facturación, pero incluye
       también el diagnóstico de los pacientes. La agencia de cobro no necesita el diagnóstico para hacer su trabajo;
       por lo tanto, la práctica violó el estándar de mínimo necesario.</div>
       <div style="${S.call}"><strong>En ruta:</strong> para entregar un paquete necesitas nombre y dirección. No
       necesitas saber para qué condición es el medicamento — y no debes comentarlo con nadie.</div>`)),

  info('Lo que no está permitido',
    T(`<div style="${S.warn}" ><p style="${S.lead};margin:0"><strong>No está permitido que accedas, obtengas,
       divulgues o discutas la PHI</strong> a menos que sea necesaria para las operaciones o que lo requiera la ley
       federal y/o estatal.</p></div>
       <p>Esto incluye mirar información que no te corresponde, buscar el expediente de un conocido, comentar una
       entrega con otro compañero que no participa en ella, o repetir en casa lo que viste en la ruta.</p>`)),

  mc('¿Cuántos identificadores define la Regla de Privacidad como información de salud protegida?',
    ['10', '14', '18', '25'], 'c'),

  mc('¿En cuáles casos puede divulgarse PHI sin autorización del paciente?',
    ['Cuando lo pide un familiar del paciente',
     'Para tratamiento, pago u operaciones de atención médica',
     'Cuando la información ya es conocida en la comunidad',
     'Cuando el paciente no contesta el teléfono'], 'b'),

  mc('El despacho le pide a un cliente la lista de entregas del día e incluye el diagnóstico de cada paciente, aunque solo hacía falta nombre y dirección. ¿Qué estándar se violó?',
    ['El estándar de mínimo necesario',
     'La regla de notificación de incumplimiento',
     'El requisito de cifrado en tránsito',
     'Ninguno: es información de la misma empresa'], 'a'),

  ms('Marca TODO lo que se considera PHI cuando aparece junto a información de salud del paciente.',
    ['El nombre y la dirección de entrega',
     'El número de teléfono del paciente',
     'La foto de cara completa del paciente',
     'El modelo del camión de reparto'], ['a', 'b', 'c']),

  mc('Un compañero te pide ver el manifiesto de una ruta que no es la suya "por curiosidad". ¿Qué haces?',
    ['Se lo enseño: somos de la misma compañía',
     'Se lo enseño si no menciona diagnósticos',
     'No se lo enseño: solo accede a PHI quien la necesita para su trabajo',
     'Le tomo una foto y se la envío por mensaje de texto'], 'c'),
];
