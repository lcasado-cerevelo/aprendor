import { info, T, fig, steps, badgeList, resources, mod, mc } from '../_shared/authoring.mjs';

export default [
  info('¿Qué debe hacer un empleado?',
    T(`<p>Si una persona entiende que está experimentando una situación de acoso laboral:</p>
       ${steps([
         '<b>Reconozca la conducta</b> — identifique qué ocurrió.',
         '<b>Utilice los canales establecidos</b> — reporte la situación ante su supervisor, Administrador o Recursos Humanos.',
         '<b>Coopere con la investigación</b> — provea información veraz y relevante.',
       ], '#4338ca')}`)),

  info('Prevenir el acoso laboral es responsabilidad de todos',
    T(`${fig(import.meta.url, './img/respeto.jpg',
        'Compañeros de trabajo diversos chocando las manos y sonriendo',
        'Foto: Ruliff Andrean / Unsplash')}
       <p>Cada empleado debe:</p>
       ${badgeList([
         'Tratar a los demás con respeto',
         'Evitar conductas humillantes, intimidantes u hostiles',
         'No participar ni fomentar conductas de acoso',
         'Reportar situaciones que puedan constituir acoso laboral',
         'Cooperar con las investigaciones',
         'Mantener la confidencialidad de la información que corresponda',
       ], '#4338ca')}
       ${resources([
         ['Ley Núm. 90-2020 — texto oficial (LexJuris)', 'https://www.lexjuris.com/lexlex/leyes2020/lexl2020090.htm'],
         ['Guías sobre el Acoso Laboral en el Sector Privado — Departamento del Trabajo y Recursos Humanos de PR', 'https://aldia.microjuris.com/wp-content/uploads/2021/02/guicc81as_sobre_el_acoso_laboral_en_el_sector_privado_de_puerto_rico.pdf'],
         ['Conferencia sobre la Ley de Acoso Laboral (Ley Núm. 90-2020) — video completo, opcional', 'https://www.youtube.com/watch?v=G7ZeCWCuqfc'],
       ])}`)),

  mod('Evaluación', 'Selecciona la respuesta correcta de acuerdo con el contenido presentado.'),

  mc('¿Cuál de las siguientes describe mejor el concepto de acoso laboral bajo la Ley Núm. 90-2020?',
    [
      'Cualquier desacuerdo entre empleados',
      'Es aquella conducta malintencionada, no deseada, repetitiva y abusiva que atenta contra la reputación y la vida privada o familiar',
      'Cualquier decisión tomada por un supervisor que no sea del agrado del empleado',
      'Una evaluación de desempeño negativa',
    ], 'b'),

  mc('¿Qué debe hacer un empleado que entiende que está siendo víctima de una posible situación de acoso laboral?',
    [
      'Dialogar directamente con la persona involucrada y tratar de resolver la situación por su cuenta',
      'Esperar a que la conducta se repita varias veces antes de tomar alguna acción',
      'Reportar la situación con su supervisor, Administrador o Recursos Humanos',
      'Solicitar a sus compañeros que intervengan para resolver la situación',
    ], 'c'),

  mc('¿Cuál de las siguientes acciones puede ayudar a prevenir el acoso laboral?',
    [
      'Ignorar conductas inapropiadas',
      'Promover un ambiente de respeto y reportar situaciones que puedan constituir acoso',
      'Participar en burlas para evitar conflictos',
      'Compartir rumores sobre compañeros',
    ], 'b'),

  mc('¿Cuál de las siguientes acciones no se considera acoso laboral?',
    [
      'El requerimiento de un supervisor a un empleado de cumplir con sus funciones',
      'Promover la burla entre empleados',
      'Utilizar palabras soeces para referirse a un compañero',
      'Burlarse de la apariencia de un compañero',
    ], 'a'),

  mc('Cierto o falso: una situación aislada puede ser inapropiada y requerir atención, pero no necesariamente es acoso laboral.',
    ['Cierto', 'Falso'], 'a'),

  mc('Cierto o falso: la empresa podrá recurrir al despido de un empleado como parte de las medidas disciplinarias por violentar las normas de la Política para Prohibir el Acoso Laboral.',
    ['Cierto', 'Falso'], 'a'),
];
