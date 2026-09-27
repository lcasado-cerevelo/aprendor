// Pantalla de entrada y láminas 1-3 del deck: portada, instrucciones y objetivo.
import { slide, intro, T, photo, bullets, heading } from '../_shared/authoring.mjs';

export default [
  // Pantalla de entrada (no es lámina). Sin `photo`: el reproductor usa la de la portada.
  intro({
    title: 'Protocolo de Manejo de Situaciones de Violencia Doméstica en el Empleo',
    description:
      '<p>En este adiestramiento conocerás qué es la <b>violencia doméstica</b> y sus formas, qué hacer si ' +
      'identificas alguno de esos comportamientos, el <b>plan de seguridad</b> que establece la empresa, la ' +
      '<b>licencia especial</b> a la que puede tener derecho la víctima y las garantías de confidencialidad y ' +
      'no discriminación.</p>',
    minutes: 20,
  }),

  // Lámina 1 — portada: la foto del deck (image12.jpeg) a sangre con la banda de título.
  slide({ layout: 'cover', title: 'Protocolo de Manejo de Situaciones de Violencia Doméstica en el Empleo',
          photo: photo(import.meta.url, './img/portada.jpg') }),

  // Lámina 2 — el SmartArt de instrucciones del deck, rehecho como lista.
  slide({ layout: 'dark', title: 'Instrucciones' },
    T(`${heading('Instrucciones')}
       <p>Bienvenido(a) al adiestramiento virtual sobre el Protocolo de Manejo de Situaciones de Violencia
       Doméstica en el Empleo.</p>
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
       <p>Proveer a los empleados los conocimientos necesarios para conocer el Protocolo establecido por la
       empresa para el manejo de situaciones de violencia doméstica en el lugar de trabajo, incluyendo el proceso
       para reportar una situación.</p>`)),
];
