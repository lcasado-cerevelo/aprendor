// Láminas 12-17 del deck: las 6 preguntas de evaluación (10 puntos c/u; la 5 y la 6 son
// cierto o falso), y una lámina final de recursos adicionales (fuera del deck).
import { slide, T, mc, bullets, heading } from '../_shared/authoring.mjs';

export default [
  mc('¿Cuál de las siguientes describe mejor el concepto de acoso laboral bajo la Ley Núm. 90-2020?',
    [
      'Cualquier desacuerdo entre empleados.',
      'Es aquella conducta malintencionada, no deseada, repetitiva y abusiva que atenta contra la reputación y la vida privada o familiar.',
      'Cualquier decisión tomada por un supervisor que no sea del agrado del empleado.',
      'Una evaluación de desempeño negativa.',
    ], 'b'),

  mc('¿Qué debe hacer un empleado que entiende que está siendo víctima de una posible situación de acoso laboral?',
    [
      'Dialogar directamente con la persona involucrada y tratar de resolver la situación por su cuenta.',
      'Esperar a que la conducta se repita varias veces antes de tomar alguna acción.',
      'Reportar la situación con mi supervisor, Administrador o Recursos Humanos.',
      'Solicitar a sus compañeros que intervengan para resolver la situación.',
    ], 'c'),

  mc('¿Cuál de las siguientes acciones puede ayudar a prevenir el acoso laboral?',
    [
      'Ignorar conductas inapropiadas.',
      'Promover un ambiente de respeto y reportar situaciones que puedan constituir acoso.',
      'Participar en burlas para evitar conflictos.',
      'Compartir rumores sobre compañeros.',
    ], 'b'),

  mc('¿Cuál de las siguientes acciones no se considera acoso laboral?',
    [
      'El requerimiento de un supervisor a un empleado de cumplir con sus funciones.',
      'Promover la burla entre empleados.',
      'Utilizar palabras soeces para referirse a un compañero.',
      'Burlarse de la apariencia de un compañero.',
    ], 'a'),

  mc('Cierto o falso: una situación aislada puede ser inapropiada y requerir atención, pero no necesariamente es acoso laboral.',
    ['Cierto', 'Falso'], 'a'),

  mc('Cierto o falso: la empresa podrá recurrir al despido de un empleado como parte de las medidas disciplinarias por violentar las normas de la Política para Prohibir el Acoso Laboral.',
    ['Cierto', 'Falso'], 'a'),

  // Lámina final — recursos adicionales (videos como enlace, no incrustados).
  slide({ layout: 'dark', title: 'Recursos adicionales' },
    T(`${heading('Recursos adicionales')}
       <p>Material complementario, opcional, para profundizar en el tema:</p>
       ${bullets([
         '<a href="https://www.lexjuris.com/lexlex/leyes2020/lexl2020090.htm" target="_blank" rel="noopener">Ley Núm. 90-2020 — texto oficial</a> (LexJuris)',
         '<a href="https://aldia.microjuris.com/wp-content/uploads/2021/02/guicc81as_sobre_el_acoso_laboral_en_el_sector_privado_de_puerto_rico.pdf" target="_blank" rel="noopener">Guías sobre el Acoso Laboral en el Sector Privado</a> (Departamento del Trabajo y Recursos Humanos de PR)',
         '<a href="https://www.youtube.com/watch?v=G7ZeCWCuqfc" target="_blank" rel="noopener">Conferencia sobre la Ley de Acoso Laboral (Ley Núm. 90-2020)</a> — video',
       ])}
       <p><b>Recuerde:</b> el acoso laboral es una conducta reiterada. Si lo enfrenta o lo presencia, repórtelo a su
       supervisor, al Administrador o a Recursos Humanos.</p>`)),
];
