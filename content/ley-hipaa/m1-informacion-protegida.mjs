import { info, T, S, img, quote, chips } from '../_shared/authoring.mjs';

export default [
  info('¿Qué es HIPAA?',
    T(`${img('https://images.unsplash.com/photo-1758691462814-485c3672e447?fm=jpg&q=60&w=1200&auto=format&fit=crop',
        'Un profesional de salud documentando información en un expediente médico',
        'Foto: Vitaly Gariev / Unsplash')}
       <p>HIPAA significa <i>Health Insurance Portability and Accountability Act</i>.</p>
       <p>Es una ley federal que establece protecciones para la información de salud de los pacientes y
       establece requisitos relacionados con su uso y divulgación.</p>
       <p>Para los empleados que trabajan en la distribución y entrega de productos médicos, la protección de la
       información es particularmente importante porque pueden tener acceso a información relacionada con
       pacientes, órdenes médicas, proveedores de salud, entre otra.</p>
       ${quote('La información de salud debe manejarse únicamente cuando sea necesario para realizar las funciones autorizadas del trabajo.')}`)),

  info('Qué es información de salud protegida',
    T(`<p>La información de salud protegida (PHI) incluye, entre otros:</p>
       ${chips([
         'Nombre del paciente', 'Dirección', 'Teléfono', 'Fecha de nacimiento', 'Diagnóstico o condición',
         'Medicamentos', 'Órdenes médicas', 'Información de seguros', 'Número de identificación', 'Tratamientos',
       ])}
       <div style="${S.warn}">Un empleado no necesita conocer detalles adicionales sobre el diagnóstico o
       tratamiento del paciente si esa información no es necesaria para realizar su trabajo. <b>No se debe
       acceder a información simplemente por curiosidad.</b></div>`)),

  info('Ejemplos de información protegida en la industria de distribución',
    T(`<ul>
         <li>Tener acceso a una factura con información de medicamentos o equipo médico</li>
         <li>Preparar una entrega a un paciente</li>
         <li>Leer una etiqueta de envío con el nombre del paciente</li>
         <li>Entregar equipos o materiales médicos</li>
         <li>Manejar documentos de entrega</li>
         <li>Contestar llamadas relacionadas con una orden de paciente</li>
       </ul>
       <div style="${S.call}">Si la información permite relacionar a una persona con su salud, tratamiento,
       servicios o productos médicos, debe manejarse con cuidado.</div>`)),
];
