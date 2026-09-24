// Láminas 14-20 del deck: las 7 preguntas de evaluación (10 puntos c/u), y una lámina
// final de recursos adicionales (material de enriquecimiento, fuera del deck).
import { slide, T, mc, bullets, heading } from '../_shared/authoring.mjs';

export default [
  mc('¿Cuál describe mejor el hostigamiento sexual?',
    [
      'Cualquier conversación entre compañeros de trabajo',
      'Una conducta de naturaleza sexual no deseada que afecta o interfiere con el ambiente laboral',
      'Una invitación social entre compañeros',
      'Un desacuerdo entre empleados',
    ], 'b'),

  mc('El hostigamiento sexual puede ocurrir:',
    [
      'Solamente entre un supervisor y un empleado',
      'Solamente entre personas de diferente sexo',
      'Entre compañeros, supervisores, clientes, suplidores u otras personas relacionadas con el trabajo',
      'Solamente cuando existe contacto físico',
    ], 'c'),

  mc('¿Cuál de las siguientes puede constituir hostigamiento sexual?',
    [
      'Un comentario profesional relacionado con el desempeño',
      'Una invitación social aceptada por ambas personas',
      'Enviar repetidamente mensajes o imágenes de contenido sexual no deseado',
      'Saludar a un compañero de trabajo',
    ], 'c'),

  mc('¿Qué debe hacer un empleado que experimenta o presencia una situación de posible hostigamiento sexual?',
    [
      'Ignorarla',
      'Publicarla en las redes sociales',
      'Reportarla utilizando los canales establecidos por la empresa',
      'Confrontar públicamente a la persona involucrada',
    ], 'c'),

  mc('¿Cuál de las siguientes acciones está protegida contra represalias?',
    [
      'Tener buen desempeño',
      'Cumplir con las normas de la empresa',
      'Quejarse de conducta de índole sexual',
    ], 'c'),

  mc('¿Quién tiene la responsabilidad de contribuir a un ambiente de trabajo libre de hostigamiento sexual?',
    [
      'Solamente Recursos Humanos',
      'Solamente los supervisores',
      'Solamente la persona que presenta una queja',
      'Todos los empleados y personas que forman parte del ambiente laboral',
    ], 'd'),

  mc('¿Qué se recomienda para evitar situaciones de hostigamiento sexual en el empleo?',
    [
      'Tratar a sus compañeros con respeto',
      'Enviar memes y stickers de "doble sentido"',
      'Decirle a un(a) compañero(a) lo bien que le queda el pantalón',
      'Todas las anteriores',
    ], 'a'),

  // Lámina 21 — recursos adicionales (video como enlace, no incrustado: ver README).
  slide({ layout: 'dark', title: 'Recursos adicionales' },
    T(`${heading('Recursos adicionales')}
       <p>Material complementario, opcional, para profundizar en el tema:</p>
       ${bullets([
         '<a href="https://www.youtube.com/watch?v=nKD7T0CX3XA" target="_blank" rel="noopener">¿Qué es hostigamiento sexual?</a> — video',
         '<a href="https://www.trabajo.pr.gov/docs/Unidad_Antidiscrimen/Ley_17_Hostigamiento_Sexual_Trabajo.pdf" target="_blank" rel="noopener">Ley Núm. 17 de 1988 — texto oficial</a> (Departamento del Trabajo de PR)',
         '<a href="https://docs.pr.gov/files/Mujer/Leyes/Gu%C3%ADas%20para%20la%20Prevenci%C3%B3n%20y%20el%20Manejo%20del%20Hostigamiento%20Sexual%20en%20el%20Empleo.pdf" target="_blank" rel="noopener">Guías para la Prevención y el Manejo del Hostigamiento Sexual en el Empleo</a> (Oficina de la Procuradora de las Mujeres)',
       ])}
       <p><b>Recuerde:</b> una conducta de naturaleza sexual no deseada que afecta o interfiere con el ambiente de trabajo
       es hostigamiento sexual. No tiene que confrontar al hostigador para poder reportar la situación.</p>`)),
];
