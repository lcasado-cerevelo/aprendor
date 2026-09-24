// Láminas 7-9 del deck: quién puede ser víctima, responsabilidades y qué hacer.
import { slide, T, photo, bullets, heading } from '../_shared/authoring.mjs';

export default [
  // Lámina 7
  slide({ layout: 'split', title: '¿Quién puede ser víctima de hostigamiento sexual?' },
    T(bullets([
      'Cualquier empleado puede experimentar conductas de hostigamiento sexual',
      'Puede ocurrir entre personas del mismo o diferente sexo',
      ['El hostigador puede ser:', [
        'Supervisor',
        'Compañero de trabajo',
        'Cliente',
        'Contratista o suplidor',
        'Cualquier otra persona relacionada con el trabajo',
      ]],
    ]))),

  // Lámina 8
  slide({ layout: 'split', title: 'Responsabilidades' },
    T(`${heading('Empleados')}
       ${bullets([
         'Mantener una conducta profesional y respetuosa',
         'No participar ni fomentar conductas inapropiadas',
         'Reportar situaciones de hostigamiento',
       ])}
       ${heading('Supervisores')}
       ${bullets([
         'Recibir cualquier preocupación o queja de algún empleado',
         'Informar inmediatamente a Recursos Humanos o al Administrador de la oficina',
         'No puede investigar por cuenta propia',
         'Evitar cualquier conducta constitutiva de represalia.',
       ])}`)),

  // Lámina 9 — foto del deck (image13.jpeg) a la izquierda. En el pptx la imagen está
  // recortada al 38 % central (srcRect l=25.6 % r=36.4 %); photoPos reproduce ese encuadre.
  slide({ layout: 'photo-left', title: '¿Qué hacer cuándo el empleado siente que está siendo víctima de hostigamiento sexual?',
          photo: photo(import.meta.url, './img/victima.jpg'), photoPos: '41% 50%' },
    T(bullets([
      'Si se siente seguro para hacerlo, puede comunicarle al hostigador que su conducta le incomoda y solicitarle que desista de la misma',
      'Acuda a su supervisor, cualquier supervisor de la empresa o a Recursos Humanos',
    ]))),
];
