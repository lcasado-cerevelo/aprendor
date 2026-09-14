import { info, T, S, badgeList, statGrid } from '../_shared/authoring.mjs';

export default [
  info('Si identifica alguno de estos comportamientos',
    T(`${badgeList([
         'Comuníquelo a un familiar',
         'Busque protección',
         'Comuníquelo a su supervisor, al Administrador o Recursos Humanos',
         'Si radica una querella para conseguir protección bajo la Ley Núm. 54, idealmente presente copia a Recursos Humanos o a la Administradora',
         'Provea una foto reciente del agresor(a)',
         'Puede tener derecho a una licencia sin sueldo con protección de empleo para realizar gestiones relacionadas a la situación',
       ], '#9f1239')}
       <div style="${S.call}"><b>Importante:</b> no tiene que haber presentado una querella ante las autoridades
       para activar el protocolo de manejo de violencia doméstica en el empleo.</div>`)),

  info('Qué hará la empresa',
    T(`<p>Si usted es víctima de violencia doméstica, la empresa establecerá un plan para atender y garantizar
       su salud y seguridad, que puede considerar medidas como:</p>
       <div style="${S.grid}">
         <div style="${S.cardS}">📇 Identificar personas de contacto en caso de emergencia</div>
         <div style="${S.cardS}">☎️ Filtrar llamadas o visitas inesperadas</div>
         <div style="${S.cardS}">🏢 Consideraciones de acomodo</div>
         <div style="${S.cardS}">🛡️ Medidas de seguridad particulares</div>
         <div style="${S.cardS}">📋 Activación de licencias elegibles</div>
       </div>`)),

  info('Licencia especial',
    T(`${statGrid([['15 días', 'laborables por año natural'], ['No acumula', 'ni se transfiere al próximo año'], ['Flexible', 'fraccionada o intermitente']], '#9f1239')}
       <p style="${S.muted}">Es adicional a las demás licencias a las que tenga derecho el empleado.</p>
       <p><b>¿Para qué puede utilizarse?</b> El empleado puede utilizar esta licencia para atender asuntos
       relacionados con la situación, incluyendo:</p>
       ${badgeList([
         'Orientarse y solicitar una Orden de Protección',
         'Obtener asistencia legal',
         'Buscar vivienda segura o acudir a un albergue',
         'Visitar una clínica, hospital o acudir a citas médicas',
         'Obtener orientación, servicios o asistencia relacionada con la situación',
       ], '#9f1239')}`)),
];
