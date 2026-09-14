import { mod, mc } from '../_shared/authoring.mjs';

export default [
  mod('Evaluación', 'Selecciona la respuesta correcta de acuerdo con el contenido presentado.'),

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
];
