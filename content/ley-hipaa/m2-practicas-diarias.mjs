// Láminas 7-9 del deck: entregas, computadoras y sistemas, y redes sociales.
import { slide, T, bullets } from '../_shared/authoring.mjs';

export default [
  // Lámina 7
  slide({ layout: 'split', title: 'Confidencialidad durante las entregas' },
    T(`<p>Las entregas requieren especial atención. El empleado debe:</p>
       ${bullets([
         'Confirmar que está entregando el producto al destinatario correcto',
         'No decir información médica frente a terceros',
         'Mantener documentos y órdenes protegidos',
         'No dejar documentos visibles dentro del vehículo',
         'No discutir información del paciente con personas no autorizadas',
         'Evitar conversaciones sobre pacientes en lugares públicos',
         'No se puede realizar la entrega en la residencia o local de vecinos',
         'No tomar fotografías de documentos de pacientes utilizando teléfonos personales',
       ])}`)),

  // Lámina 8
  slide({ layout: 'split', title: 'Uso de Computadoras y Sistemas' },
    T(`<p>Los sistemas de información también contienen información protegida. Los empleados deben:</p>
       ${bullets([
         'Utilizar su propio usuario y contraseña. Queda prohibido compartir dichas credenciales.',
         'Cerrar sesión cuando no esté utilizando el sistema o va a abandonar su área de trabajo.',
         'No dejar computadoras desbloqueadas.',
         'No descargar información de pacientes a dispositivos personales.',
       ])}`)),

  // Lámina 9
  slide({ layout: 'split', title: 'Redes Sociales y Fotos' },
    T(`<p>Está prohibido publicar o compartir información de pacientes, incluyendo:</p>
       ${bullets([
         'Fotos',
         'Nombres',
         'Direcciones',
         'Órdenes médicas',
         'Productos asociados a un paciente',
         'Capturas de pantalla',
         'Conversaciones relacionadas con pacientes',
         'Incluso si no se menciona el nombre del paciente, una combinación de detalles podría permitir identificarlo',
       ])}`)),
];
