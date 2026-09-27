// Láminas 4-6 del deck: qué es HIPAA, qué es información de salud protegida y ejemplos
// en la distribución. La lámina 5 no tiene encabezado propio en el deck: va sin título.
import { slide, T, bullets, heading } from '../_shared/authoring.mjs';

export default [
  // Lámina 4
  slide({ layout: 'split', title: '¿Qué es HIPAA?' },
    T(`<p>HIPAA significa Health Insurance Portability and Accountability Act</p>
       ${bullets([
         'Es una ley federal que establece protecciones para la información de salud de los pacientes y establece requisitos relacionados con su uso y divulgación.',
         'Para los empleados que trabajan en la distribución y entrega de productos médicos, la protección de la información es particularmente importante porque pueden tener acceso a información relacionada con pacientes, órdenes médicas, proveedores de salud, entre otra.',
       ])}
       <p><b>Regla fundamental:</b></p>
       ${bullets([
         'La información de salud debe manejarse únicamente cuando sea necesario para realizar las funciones autorizadas del trabajo.',
       ])}`)),

  // Lámina 5
  slide({ layout: 'split', title: '' },
    T(`<p>Qué es información de salud protegida:</p>
       ${bullets([
         'Nombre del paciente',
         'Dirección',
         'Número de teléfono',
         'Fecha de nacimiento',
         'Información sobre diagnósticos o condiciones de salud',
         'Medicamentos o productos médicos relacionados con un paciente',
         'Órdenes médicas',
         'Información de seguros',
         'Número de identificación del paciente',
         'Información relacionada con tratamientos o servicios de salud',
       ])}`)),

  // Cierre de la lámina 5: en el deck va debajo de la lista, pero junta no cabe.
  slide({ layout: 'dark', title: 'Importante' },
    T(`${heading('Importante')}
       <p>Un empleado no necesita conocer detalles adicionales sobre el diagnóstico o tratamiento del paciente si
       esa información no es necesaria para realizar su trabajo.</p>
       <p>No se debe acceder a información simplemente por curiosidad.</p>`)),

  // Lámina 6
  slide({ layout: 'split', title: 'Ejemplos de información protegida en la Industria de Distribución' },
    T(`${bullets([
         'Tener acceso a una factura con información de medicamentos o equipo médico',
         'Preparar una entrega a un paciente',
         'Leer una etiqueta de envío con el nombre del paciente',
         'Entregar equipos o materiales médicos',
         'Manejar documentos de entrega',
         'Contestar llamadas relacionadas con una orden de paciente',
       ])}
       <p>Si la información permite relacionar a una persona con su salud, tratamiento, servicios o productos
       médicos, debe manejarse con cuidado.</p>`)),
];
