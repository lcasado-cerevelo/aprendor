// Pantalla de entrada y láminas 1-3 del deck: portada, instrucciones y objetivo.
import { slide, intro, T, photo, bullets, heading } from '../_shared/authoring.mjs';

export default [
  // Pantalla de entrada (no es lámina). Sin `photo`: el reproductor usa la de la portada.
  intro({
    title: 'Cumplimiento con la Ley HIPAA',
    description:
      '<p>En este adiestramiento aprenderás qué es la <b>Ley HIPAA</b>, qué información de salud está ' +
      'protegida, cómo protegerla durante las <b>entregas</b>, en las <b>computadoras</b> y en las ' +
      '<b>redes sociales</b>, qué es una divulgación no autorizada y cómo reportar un posible incidente.</p>',
    minutes: 20,
  }),

  // Lámina 1 — portada: la foto del deck (image12.jpg) a sangre con la banda de título.
  slide({ layout: 'cover', title: 'Cumplimiento con la Ley HIPAA',
          photo: photo(import.meta.url, './img/portada.jpg') }),

  // Lámina 2 — el SmartArt de instrucciones del deck, rehecho como lista.
  slide({ layout: 'dark', title: 'Instrucciones' },
    T(`${heading('Instrucciones')}
       <p>Bienvenido(a) al adiestramiento virtual sobre Cumplimiento con la Ley HIPAA.</p>
       <p>Para completar satisfactoriamente este adiestramiento:</p>
       ${bullets([
         'Lea detenidamente todo el contenido presentado en cada sección.',
         'Procure comprender la información, incluyendo los conceptos, ejemplos, responsabilidades y procedimientos discutidos.',
         'Una vez haya completado la presentación, responda las preguntas de evaluación al final del adiestramiento.',
         'Seleccione la respuesta correcta de acuerdo con el contenido presentado.',
       ])}
       <p><b>Importante:</b> la evaluación está diseñada para validar la comprensión del contenido presentado.</p>`)),

  // Lámina 3 — objetivo.
  slide({ layout: 'dark', title: 'Objetivo' },
    T(`${heading('Objetivo')}
       <p>Se persigue proveer los principios fundamentales de la Ley HIPAA, relacionados con la privacidad y
       seguridad de la información de salud protegida para proteger la confidencialidad de la información de los
       pacientes durante el manejo, transporte y entrega de medicamentos, equipos médicos o materiales de salud.</p>`)),

  // Continuación del objetivo: en el deck va en la misma lámina, pero junta no cabe.
  slide({ layout: 'dark', title: 'Objetivo' },
    T(`${heading('Objetivo')}
       <p>Debido a la naturaleza de las operaciones del negocio, todos los empleados pueden estar expuestos de
       manera directa o indirecta a información de salud protegida. A tales efectos, este adiestramiento permitirá:</p>
       ${bullets([
         'Reconocer qué información se considera protegida bajo HIPAA.',
         'Comprender sus responsabilidades en la protección de la información de los pacientes.',
         'Manejar adecuadamente los documentos, órdenes y entregas.',
         'Evitar situaciones que puedan representar una violación de privacidad y seguridad.',
         'Conocer cómo reportar oportunamente una posible violación de HIPAA.',
       ])}`)),
];
