import { info, T, S, badgeList, steps } from '../_shared/authoring.mjs';

export default [
  info('Confidencialidad durante las entregas',
    T(`<p>Las entregas requieren especial atención. El empleado debe:</p>
       ${badgeList([
         'Confirmar que está entregando el producto al destinatario correcto',
         'No decir información médica frente a terceros',
         'Mantener documentos y órdenes protegidos',
         'No dejar documentos visibles dentro del vehículo',
         'No discutir información del paciente con personas no autorizadas',
         'Evitar conversaciones sobre pacientes en lugares públicos',
       ], '#16a34a')}
       <div style="${S.warn}"><b>No se puede</b> realizar la entrega en la residencia o local de vecinos, ni
       tomar fotografías de documentos de pacientes utilizando teléfonos personales.</div>`)),

  info('Uso de computadoras y sistemas',
    T(`<p>Los sistemas de información también contienen información protegida. Los empleados deben:</p>
       ${steps([
         'Utilizar su propio usuario y contraseña — queda prohibido compartir dichas credenciales.',
         'Cerrar sesión cuando no esté utilizando el sistema o vaya a abandonar su área de trabajo.',
         'No dejar computadoras desbloqueadas.',
         'No descargar información de pacientes a dispositivos personales.',
       ])}`)),

  info('Redes sociales y fotos',
    T(`<p>Está prohibido publicar o compartir información de pacientes, incluyendo:</p>
       <div style="${S.grid}">
         <div style="${S.card}">📷 Fotos y capturas de pantalla</div>
         <div style="${S.card}">🏷️ Nombres y direcciones</div>
         <div style="${S.card}">💊 Órdenes médicas y productos asociados a un paciente</div>
         <div style="${S.card}">💬 Conversaciones relacionadas con pacientes</div>
       </div>
       <div style="${S.warn}">Incluso si no se menciona el nombre del paciente, una combinación de detalles
       podría permitir identificarlo.</div>`)),
];
