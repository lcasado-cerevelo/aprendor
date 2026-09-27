// Pantalla de entrada y láminas 1-3 del deck: portada, instrucciones y objetivo.
import { slide, intro, T, photo, bullets, heading } from '../_shared/authoring.mjs';

export default [
  // Pantalla de entrada (no es lámina). Sin `photo`: el reproductor usa la de la portada.
  intro({
    title: 'Acoso Laboral en el Empleo',
    description:
      '<p>En este adiestramiento aprenderás qué es el acoso laboral según la <b>Ley Núm. 90-2020 de Puerto ' +
      'Rico</b>, qué conductas pueden constituirlo y cuáles no, por qué se trata de conductas <b>reiteradas</b>, ' +
      'qué hacer si lo enfrentas y qué hará la empresa ante una situación.</p>',
    minutes: 20,
  }),

  // Lámina 1 — portada: la foto del deck (image12.jpeg) a sangre con la banda de título.
  slide({ layout: 'cover', title: 'Acoso Laboral',
          photo: photo(import.meta.url, './img/portada.jpg') }),

  // Lámina 2 — el SmartArt de instrucciones del deck, rehecho como lista. En el deck el
  // encabezado de estas dos láminas dice solo «ACOSO LABORAL».
  slide({ layout: 'dark', title: 'Instrucciones', kicker: 'ACOSO LABORAL' },
    T(`${heading('Instrucciones')}
       <p>Bienvenido(a) al adiestramiento virtual sobre Acoso Laboral.</p>
       <p>Para completar satisfactoriamente este adiestramiento:</p>
       ${bullets([
         'Lea detenidamente todo el contenido presentado en cada sección.',
         'Procure comprender la información, incluyendo los conceptos, ejemplos, responsabilidades y procedimientos discutidos.',
         'Una vez haya completado la presentación, responda las preguntas de evaluación al final del adiestramiento.',
         'Seleccione la respuesta correcta de acuerdo con el contenido presentado.',
       ])}
       <p><b>Importante:</b> la evaluación está diseñada para validar la comprensión del contenido presentado.</p>`)),

  // Lámina 3 — objetivo.
  slide({ layout: 'dark', title: 'Objetivo', kicker: 'ACOSO LABORAL' },
    T(`${heading('Objetivo')}
       <p>Se persigue que los empleados puedan reconocer conductas inapropiadas, canalizar las situaciones que
       ocurran y comprender los procedimientos establecidos para el manejo de situaciones de acoso laboral.</p>
       <p>A través de sus prácticas y políticas, la empresa promueve el respeto, la cordialidad y las relaciones
       de trabajo saludables.</p>`)),
];
