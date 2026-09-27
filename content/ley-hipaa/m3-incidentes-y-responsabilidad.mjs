// Láminas 10-12 del deck: divulgación no autorizada, qué hacer ante un incidente y la
// responsabilidad del empleado.
import { slide, T, bullets } from '../_shared/authoring.mjs';

export default [
  // Lámina 10
  slide({ layout: 'split', title: '¿Qué es una Divulgación No Autorizada?' },
    T(`<p>Una divulgación no autorizada ocurre cuando información protegida se utiliza o se comparte sin autorización.</p>
       <p><b>Ejemplos:</b></p>
       ${bullets([
         'Entregar documentos del paciente a la persona equivocada',
         'Hablar sobre el diagnóstico de un paciente frente a terceros',
         'Enviar información a un número de teléfono incorrecto',
         'Compartir información con un familiar que no está autorizado',
         'Publicar una fotografía de una orden médica',
         'Dejar documentos con información protegida en un lugar donde otras personas puedan verlos.',
       ])}
       <p><b>Una divulgación puede ocurrir intencionalmente o por accidente.</b></p>`)),

  // Lámina 11 — los pasos van numerados, como en el deck.
  slide({ layout: 'split', title: '¿Qué hacer ante un incidente?' },
    T(`<p>Si cree que ocurrió una posible violación de privacidad:</p>
       <ol>
         <li>No lo oculte. Informe la situación inmediatamente a la Administradora.</li>
         <li>Notifique al supervisor o a la persona designada por la empresa.</li>
         <li>Provea todos los detalles que tenga disponibles.</li>
         <li>No investigue o corrija el incidente por su cuenta.</li>
       </ol>
       <p><b>Importante:</b> reportar rápidamente un posible incidente permite que la empresa evalúe la situación.</p>`)),

  // Lámina 12
  slide({ layout: 'split', title: 'Responsabilidad del Empleado' },
    T(bullets([
      'Cada empleado tiene una responsabilidad en la protección de la información de salud.',
      'Utilizar correctamente la información',
      'Seguir las políticas y procedimientos de la empresa',
      'Proteger documentos y sistemas',
      'Evitar divulgaciones innecesarias',
      'Reportar inmediatamente posibles incidentes',
      'La privacidad del paciente es responsabilidad de todos.',
    ]))),
];
