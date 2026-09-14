import { info, T, S, img, badgeList, resources, mod, mc } from '../_shared/authoring.mjs';

export default [
  info('Importante',
    T(`${img('https://images.unsplash.com/photo-1752213071488-d6417eb6a291?fm=jpg&q=60&w=1200&auto=format&fit=crop',
        'Una mano se extiende hacia la luz del atardecer, en señal de esperanza',
        'Foto: Liana S / Unsplash')}
       ${badgeList([
         'Se trabajará y mantendrá la situación en confidencialidad',
         'Se establecerán las medidas que garanticen la seguridad de la víctima y del resto de los empleados',
         'La empresa no discriminará contra víctimas de violencia doméstica',
       ], '#9f1239')}
       <div style="${S.call}">Si tiene dudas sobre el Protocolo de Manejo de Situaciones de Violencia Doméstica
       en el Empleo, comuníquese con su supervisor, la Administradora o Recursos Humanos.</div>
       ${resources([
         ['Línea de Orientación 24 horas — Oficina de la Procuradora de las Mujeres: 787-722-2977', 'https://www.mujer.pr.gov/'],
         ['Ley Núm. 217-2006 — Protocolo de violencia doméstica en el lugar de trabajo (texto oficial)', 'https://www.lexjuris.com/lexlex/Leyes2006/lexl2006217.htm'],
         ['Ley Núm. 83-2019 — Licencia especial de 15 días (texto oficial)', 'https://www.lexjuris.com/lexlex/Leyes2019/lexl2019083.htm'],
       ])}`)),

  mod('Sesión de preguntas', 'Selecciona la respuesta correcta de acuerdo con el contenido presentado.'),

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
      'Hacer una querella u orden bajo la Ley 54; sin este documento no le evidencio a la empresa la situación',
      'Notificarlo a mi supervisor',
      'Comentárselo a un compañero de trabajo',
      'Ninguna de las anteriores',
    ], 'b'),

  mc('¿Cuántos días de licencia especial se le pueden extender al empleado para atender gestiones relacionadas a la situación de violencia doméstica?',
    ['5 días', '10 días', '15 días', '30 días'], 'c'),

  mc('¿Para cuál de las siguientes actividades puede utilizarse la licencia especial?',
    [
      'Solicitar una Orden de Protección',
      'Vacacionar',
      'Ir a citas de hijos no relacionadas con la situación',
      'Todas las anteriores',
    ], 'a'),

  mc('¿Cuál de las siguientes es una medida que puede formar parte de un plan de seguridad en el trabajo?',
    [
      'Identificar personas de contacto en caso de emergencia',
      'Establecer procedimientos para manejar visitas inesperadas',
      'Consideraciones de acomodo',
      'Todas las anteriores',
    ], 'd'),
];
