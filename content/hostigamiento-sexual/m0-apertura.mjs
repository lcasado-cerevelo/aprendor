// Pantalla de entrada y láminas 1-3 del deck: portada, instrucciones y objetivo.
import { slide, intro, T, photo, bullets, heading } from '../_shared/authoring.mjs';

export default [
  // Pantalla de entrada (no es lámina): el resumen del curso que antes daba la página de
  // apertura con resumen(). El reproductor le añade láminas, preguntas y puntos, y en modo
  // presentación la dibuja antes del botón «Comenzar» con la portada atenuada de fondo.
  // Sin `photo`: el reproductor (y seed.mjs --preview) usa la de la primera lámina cover,
  // así la portada no viaja dos veces en course.json ni en cada /take.
  intro({
    title: 'Hostigamiento Sexual en el Empleo',
    description:
      '<p>En este adiestramiento aprenderás qué es el hostigamiento sexual según la <b>Ley Núm. 17 de Puerto ' +
      'Rico</b>, sus dos formas (<b>quid pro quo</b> y <b>ambiente hostil</b>), qué te toca hacer como empleado ' +
      'o supervisor, cómo se reporta y se investiga una situación en la empresa, y por qué está prohibido ' +
      'tomar represalias contra quien reporta.</p>',
    minutes: 25,
  }),

  // Lámina 1 — portada: la foto del deck (image12.jpeg) a sangre con la banda de título.
  slide({ layout: 'cover', title: 'Hostigamiento Sexual en el Empleo',
          photo: photo(import.meta.url, './img/portada.jpg') }),

  // Lámina 2 — el SmartArt de instrucciones del deck, rehecho como lista.
  slide({ layout: 'dark', title: 'Instrucciones' },
    T(`${heading('Instrucciones')}
       <p>Bienvenido(a) al adiestramiento virtual sobre Hostigamiento Sexual en el Empleo.</p>
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
       <p>Este adiestramiento persigue proveer a los empleados los conocimientos necesarios para identificar,
       prevenir y reportar situaciones de hostigamiento sexual en el lugar de trabajo, conforme a la Política
       de Hostigamiento Sexual en el Empleo, establecida en el Manual del Empleado.</p>
       <p>La empresa está comprometida en brindar y garantizarle a todos los empleados un entorno seguro y
       saludable, lejos de comportamientos que alteren este predicamento.</p>`)),
];
