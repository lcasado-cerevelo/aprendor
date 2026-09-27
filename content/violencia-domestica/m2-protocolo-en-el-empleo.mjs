// Láminas 7-10 del deck: qué hacer, el plan de seguridad de la empresa, la licencia especial
// e «Importante». Se corrigen erratas del deck («Si es usted identifica», «antes las
// autoridades», «domética») y se completa el último punto de la lámina 10, que en el deck
// quedó cortado («Si tiene dudas sobre el Protocolo de»).
import { slide, T, bullets, heading } from '../_shared/authoring.mjs';

export default [
  // Lámina 7
  slide({ layout: 'split', title: 'Si usted identifica algunos de los comportamientos antes indicados:' },
    T(`${bullets([
         'Comuníquelo a un familiar',
         'Busque protección',
         'Comuníquelo a su supervisor, al Administrador o Recursos Humanos',
         'Si radica una querella para conseguir protección bajo la Ley Núm. 54, idealmente presente copia a Recursos Humanos o a la Administradora',
         'Provea una foto reciente del agresor(a)',
         'Puede tener derecho a una licencia sin sueldo con protección de Empleo para realizar gestiones relacionadas a la situación.',
       ])}
       <p><b>Importante: No tiene que haber presentado una querella ante las autoridades para activar el protocolo de
       manejo de violencia doméstica en el empleo.</b></p>`)),

  // Lámina 8
  slide({ layout: 'split', title: '' },
    T(`<p>Si usted es víctima de violencia doméstica la empresa establecerá un plan para atender y garantizar la
       salud y seguridad de la víctima que puede considerar medidas como:</p>
       ${bullets([
         'Identificar personas de contacto en caso de emergencia',
         'Establecer procedimientos para filtrar llamadas o visitas inesperadas',
         'Consideraciones de acomodos',
         'Medidas de seguridad particulares',
         'Activación de licencias elegibles',
       ])}`)),

  // Lámina 9 — en el deck es una sola lámina oscura con el encabezado «Licencia especial»;
  // aquí va en dos (condiciones y usos) porque junta no cabe en la lámina de 1280×720.
  slide({ layout: 'dark', title: 'Licencia especial' },
    T(`${heading('Licencia especial')}
       <p>La víctima puede tener derecho a una licencia sin sueldo:</p>
       ${bullets([
         'Hasta 15 días laborables por año natural. Es adicional a las demás licencias a las que tenga derecho el empleado.',
         'Los días no se acumulan ni se transfieren al próximo año.',
         'Puede utilizarla de manera fraccionada, flexible o intermitente.',
       ])}`)),

  slide({ layout: 'dark', title: '¿Para qué puede utilizarse?' },
    T(`${heading('¿Para qué puede utilizarse?')}
       <p>El empleado puede utilizar esta licencia para atender asuntos relacionados con la situación, incluyendo:</p>
       ${bullets([
         'Orientarse y solicitar una Orden de Protección',
         'Obtener asistencia legal',
         'Buscar vivienda segura o acudir a un albergue',
         'Visitar una clínica, hospital o acudir a citas médicas',
         'Obtener orientación, servicios o asistencia relacionada con la situación',
       ])}`)),

  // Lámina 10
  slide({ layout: 'split', title: 'Importante' },
    T(bullets([
      'Se trabajará y mantendrá la situación en confidencialidad',
      'Se establecerán las medidas que garanticen la seguridad de la víctima y del resto de los empleados',
      'La empresa no discriminará contra víctimas de violencia doméstica',
      'Si tiene dudas sobre el Protocolo de Manejo de Situaciones de Violencia Doméstica en el Empleo, comuníquese con su supervisor, la Administradora o Recursos Humanos.',
    ]))),
];
