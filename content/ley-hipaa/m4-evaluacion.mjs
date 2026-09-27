// Láminas 13-18 del deck: las 6 preguntas de evaluación (10 puntos c/u), y una lámina
// final de recursos adicionales (fuera del deck).
import { slide, T, mc, bullets, heading } from '../_shared/authoring.mjs';

export default [
  mc('¿Cuál de las siguientes opciones describe mejor la información protegida de salud (PHI)?',
    [
      'Información personal relacionada únicamente con seguros médicos',
      'Información relacionada con la salud, el tratamiento o los servicios médicos de una persona',
      'Información pública sobre hospitales y clínicas',
      'Información general sobre productos médicos',
    ], 'b'),

  mc('¿Cuál de los siguientes puede considerarse información protegida de salud?',
    [
      'Nombre y dirección de un paciente',
      'Información sobre su diagnóstico',
      'Información relacionada con medicamentos o productos médicos',
      'Todas las anteriores',
    ], 'd'),

  mc('Un empleado recibe una orden médica que contiene información del paciente. ¿Cuál es la forma correcta de manejarla?',
    [
      'Dejarla visible en el vehículo durante la entrega',
      'Compartirla con otros empleados por curiosidad',
      'Mantenerla protegida y utilizarla únicamente para realizar la función autorizada',
      'Tomarle una fotografía con el teléfono personal',
    ], 'c'),

  mc('¿Cuál de las siguientes situaciones puede representar una divulgación no autorizada?',
    [
      'Entregar un paquete al destinatario correcto',
      'Dejar documentos con información de pacientes visibles dentro de un vehículo',
      'Guardar correctamente documentos según el procedimiento de la empresa',
      'Verificar la dirección antes de realizar una entrega',
    ], 'b'),

  mc('¿Cuál de las siguientes prácticas es apropiada?',
    [
      'Compartir la contraseña con un compañero de confianza',
      'Tomar fotografías de órdenes médicas con el teléfono personal',
      'Dejar una computadora desbloqueada mientras se está durante el periodo de almuerzo',
      'Reportar inmediatamente una situación de exposición de información protegida',
    ], 'd'),

  mc('Un empleado toma una foto de una orden médica para enviársela a un compañero mediante su teléfono personal. ¿Es esto apropiado?',
    [
      'Sí, si el compañero también trabaja en la empresa',
      'Sí, siempre que la foto se elimine después',
      'No, porque no se deben utilizar teléfonos personales para tomar o compartir información protegida fuera de los métodos autorizados por la empresa',
      'Sí, si no aparece el nombre completo del paciente',
    ], 'c'),

  // Lámina final — recursos adicionales (video como enlace, no incrustado).
  slide({ layout: 'dark', title: 'Recursos adicionales' },
    T(`${heading('Recursos adicionales')}
       <p>Material complementario, opcional, para profundizar en el tema:</p>
       ${bullets([
         '<a href="https://www.hhs.gov/hipaa/for-professionals/training/index.html" target="_blank" rel="noopener">HHS — Guías y adiestramiento sobre HIPAA</a> (en inglés)',
         '<a href="https://www.youtube.com/shorts/2hYefuexVCA" target="_blank" rel="noopener">¿Qué es la Ley HIPAA? Protección de la privacidad en la salud</a> — video',
       ])}
       <p><b>Recuerde:</b> la información de salud se maneja solo cuando es necesario para el trabajo. Ante una
       posible exposición, no la oculte: repórtela de inmediato a la Administradora o a su supervisor.</p>`)),
];
