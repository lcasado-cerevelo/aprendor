// Pantalla de entrada, portada y objetivos. La pantalla 0 del curso original (ícono y caja
// de resumen con «80 puntos») la reemplaza la pantalla de entrada, que calcula los datos
// sola; «Bienvenido al curso» pasa a ser la portada con su foto, y su cita va en la entrada.
import { slide, intro, T } from '../_shared/authoring.mjs';

export default [
  intro({
    title: 'Seguridad de la Información para Empleados',
    description:
      '<p>«El mayor riesgo para una organización no siempre está en la tecnología. Muchas veces comienza con una ' +
      'decisión humana: abrir un archivo, compartir una contraseña o ignorar una alerta.»</p>' +
      '<p>En este adiestramiento aprenderás a reconocer las amenazas más comunes, a proteger tus contraseñas y la ' +
      'información de la empresa y de los clientes, y a <b>reportar a tiempo</b> cuando algo no se ve bien.</p>',
    minutes: 30,
  }),

  // Foto de «Bienvenido al curso», ya subida a la plataforma.
  slide({ layout: 'cover', title: 'Seguridad de la Información para Empleados',
          photo: '/media/2f9fc393-23f7-464e-ba98-711be70b356d' }),

  slide({ layout: 'cards', title: 'Objetivos de aprendizaje', kicker: 'Al terminar este curso podrás' },
    T(`<ul>
         <li><b>Identificar los principios básicos</b> de la seguridad de la información y por qué importan en el trabajo.</li>
         <li><b>Reconocer amenazas comunes</b> como phishing, malware, ingeniería social y fugas de datos.</li>
         <li><b>Aplicar buenas prácticas</b> para proteger la información digital y física.</li>
         <li><b>Detectar correos sospechosos</b> y responder de forma adecuada.</li>
         <li><b>Proteger tus credenciales</b> con contraseñas seguras y verificación en dos pasos.</li>
         <li><b>Actuar correctamente ante un incidente</b> siguiendo los pasos de la empresa.</li>
       </ul>`)),
];
