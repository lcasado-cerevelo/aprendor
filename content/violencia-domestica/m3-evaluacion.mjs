// Láminas 11-17 del deck: la portadilla «Sesión de preguntas» y las 6 preguntas de
// evaluación (10 puntos c/u), y una lámina final de recursos adicionales (fuera del deck).
import { slide, T, mc, bullets, heading } from '../_shared/authoring.mjs';

export default [
  // Lámina 11 — portadilla de la evaluación.
  slide({ layout: 'dark', title: '', kicker: 'SESIÓN DE PREGUNTAS' },
    T(`<p>A continuación, 6 preguntas sobre el contenido presentado. Seleccione la respuesta correcta en cada una.</p>`)),

  mc('¿Cuál es el propósito principal de este adiestramiento?',
    [
      'Identificar problemas personales de los empleados',
      'Proveer información sobre el protocolo de manejo de situaciones de violencia doméstica en el empleo',
      'Enseñar a los supervisores a investigar relaciones personales',
      'Conocer los problemas personales de los empleados',
    ], 'b'),

  mc('¿Cuál de las siguientes puede ser una manifestación de violencia doméstica?',
    [
      'Violencia física',
      'Abuso emocional o psicológico',
      'Control económico',
      'Todas las anteriores',
    ], 'd'),

  mc('¿Qué necesito hacer para informarle a mi patrono que soy víctima de violencia doméstica?',
    [
      'Hacer una querella u orden bajo la Ley 54. Sin este documento no le evidencio a la empresa la situación.',
      'Notificarlo a mi supervisor',
      'Comentárselo a un compañero de trabajo',
      'Ninguna de las anteriores',
    ], 'b'),

  mc('¿Cuántos días de licencia especial se le puede extender al empleado para atender gestiones relacionadas a la situación de violencia doméstica?',
    ['5 días', '10 días', '15 días', '30 días'], 'c'),

  mc('¿Para cuál de las siguientes actividades puede utilizarse la licencia especial?',
    [
      'Solicitar una Orden de Protección',
      'Vacacionar',
      'Ir a citas de hijos',
      'Todas las anteriores',
    ], 'a'),

  mc('¿Cuál de las siguientes es una medida que puede formar parte de un plan de seguridad en el trabajo?',
    [
      'Identificar personas de contacto en caso de emergencia',
      'Establecer procedimientos para manejar visitas inesperadas',
      'Consideraciones de acomodos',
      'Todas las anteriores',
    ], 'd'),

  // Lámina final — recursos adicionales.
  slide({ layout: 'dark', title: 'Recursos adicionales' },
    T(`${heading('Recursos adicionales')}
       <p>Si necesita ayuda o quiere profundizar en el tema:</p>
       ${bullets([
         'Línea de Orientación 24 horas de la Oficina de la Procuradora de las Mujeres: <b>787-722-2977</b> (<a href="https://www.mujer.pr.gov/" target="_blank" rel="noopener">mujer.pr.gov</a>)',
         '<a href="https://www.lexjuris.com/lexlex/Leyes2006/lexl2006217.htm" target="_blank" rel="noopener">Ley Núm. 217-2006 — Protocolo de violencia doméstica en el lugar de trabajo</a> (texto oficial)',
         '<a href="https://www.lexjuris.com/lexlex/Leyes2019/lexl2019083.htm" target="_blank" rel="noopener">Ley Núm. 83-2019 — Licencia especial de 15 días</a> (texto oficial)',
       ])}
       <p><b>Recuerde:</b> no tiene que haber presentado una querella ante las autoridades para activar el protocolo.
       Hable con su supervisor, la Administradora o Recursos Humanos.</p>`)),
];
