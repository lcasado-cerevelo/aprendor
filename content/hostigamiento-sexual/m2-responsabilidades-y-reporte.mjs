import { info, T, S, chips, badgeList } from '../_shared/authoring.mjs';

export default [
  info('¿Quién puede ser víctima?',
    T(`<ul>
         <li>Cualquier empleado puede experimentar conductas de hostigamiento sexual</li>
         <li>Puede ocurrir entre personas del mismo o diferente sexo</li>
       </ul>
       <p>El hostigador puede ser:</p>
       ${chips(['Supervisor', 'Compañero de trabajo', 'Cliente', 'Contratista o suplidor', 'Cualquier otra persona relacionada con el trabajo'], '#a21caf')}`)),

  info('Responsabilidades',
    T(`<div style="${S.cols2}">
         <div>
           <p><b>Empleados</b></p>
           ${badgeList(['Mantener una conducta profesional y respetuosa', 'No participar ni fomentar conductas inapropiadas', 'Reportar situaciones de hostigamiento'], '#a21caf')}
         </div>
         <div>
           <p><b>Supervisores</b></p>
           ${badgeList(['Recibir cualquier preocupación o queja de algún empleado', 'Informar inmediatamente a Recursos Humanos o al Administrador de la oficina', 'No puede investigar por cuenta propia', 'Evitar cualquier conducta constitutiva de represalia'], '#a21caf')}
         </div>
       </div>`)),

  info('¿Qué hacer si se siente víctima de hostigamiento sexual?',
    T(`${badgeList([
         'Si se siente seguro(a) para hacerlo, puede comunicarle al hostigador que su conducta le incomoda y solicitarle que desista de la misma',
         'Acuda a su supervisor, a cualquier supervisor de la empresa, o a Recursos Humanos',
       ], '#a21caf')}
       <div style="${S.call}">No tiene que confrontar al hostigador para poder reportar la situación.</div>`)),
];
