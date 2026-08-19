import {info, mod, mc, ms, T, S, fig } from './authoring.mjs';

// Módulo 6 — diapositivas 30–32, 37–48, 52–53 y 56–57 del original.
export default [
  mod('Módulo 6 — Prácticas seguras en el día a día', 'Duración estimada: 12–15 minutos'),

  info('¿Qué podemos hacer?',
    T(`<p style="${S.lead}">El cumplimiento no se sostiene con documentos: se sostiene con lo que cada persona hace
       todos los días.</p>
       <p>En este módulo vemos las prácticas concretas de Advance Logistics: cadena de custodia, manejo del fax y
       del teléfono, contraseñas, phishing, conversaciones en áreas públicas, descarte de documentos y redes
       sociales.</p>
       ${fig('entrega-hipaa', 'Entrega de un paquete a domicilio con sello HIPAA Compliant', 400)}`)),

  info('Prácticas seguras de transporte',
    T(`<p>Se debe monitorear de cerca a cada conductor en cada etapa de su viaje y saber exactamente dónde está en
       todo momento, de manera que el equipo de despacho de <strong>Advance Logistics</strong>, el proveedor médico o
       cliente y el paciente se mantengan al tanto del estado de la entrega.</p>
       <p>Es importante reconocer la importancia de una <strong>cadena de custodia segura</strong>, que implica
       alertar al cliente con confirmaciones una vez se hayan completado los pedidos. Este proceso de verificación
       actúa como prueba de que la entrega se realizó a la persona adecuada, a tiempo y en perfectas condiciones.</p>
       <div style="${S.call}"><strong>En la puerta:</strong> confirma la identidad de quien recibe, entrega solo a la
       persona autorizada, y no dejes paquetes con PHI a la vista de terceros ni con vecinos que no estén
       autorizados.</div>`)),

  info('Los carreros deben estar certificados que conocen la Ley Federal "HIPAA"',
    T(`<p>Uno de los mayores beneficios de que los carreros de servicios médicos estén certificados por HIPAA es que
       puede estar seguro de que la información confidencial de los pacientes está protegida. Las certificaciones
       HIPAA se utilizan para implementar múltiples salvaguardas en las organizaciones de atención médica para
       proteger la información confidencial de salud personal.</p>
       <p>Sin HIPAA no habría ningún requisito para que los servicios de mensajería médica salvaguardaran esos
       datos.</p>`)),

  info('Mejores prácticas de privacidad para mitigar el riesgo',
    T(`<p>Garantizar la orientación del proveedor y del personal, y la educación continua. Implementar, como mínimo,
       políticas y procedimientos organizacionales sobre:</p>
       <ul>
         <li>Aviso de prácticas de privacidad.</li>
         <li>Publicación de registros.</li>
         <li>Solicitudes de restricciones y de modificación.</li>
         <li>Contabilización de la información revelada.</li>
         <li>Medidas correctivas.</li>
         <li>Respuesta a violaciones y respuesta a quejas.</li>
         <li>Identificación de un Oficial de Privacidad y Seguridad.</li>
         <li>Cumplimiento del estándar de mínimo necesario para acceder y divulgar PHI.</li>
       </ul>`)),

  info('Prácticas recomendadas para mitigar las brechas de seguridad',
    T(`<ul>
         <li>Capacitar al personal sobre la identificación y notificación de posibles incidentes e infracciones,
             usos y riesgos.</li>
         <li>Cifrar todos los dispositivos electrónicos, incluidos los portátiles y las memorias USB.</li>
         <li>Implementar cronogramas y asignar responsabilidad para las actualizaciones y parches de programas.</li>
         <li>Cuidar los equipos médicos conectados ("Internet de las cosas"): marcapasos, dispositivos de
             administración de medicamentos, monitoreo, bombas de infusión, desfibriladores, glucómetros y equipos de
             presión arterial.</li>
         <li>Establecer salvaguardas físicas adecuadas.</li>
       </ul>`)),

  info('Uso del facsímil (fax)',
    T(`<p>HIPAA permite el uso del fax, pero es un medio de <strong>alto riesgo</strong>. Antes de transmitir
       información de pacientes:</p>
       <ul>
         <li>La máquina no debe estar localizada en áreas públicas y debe estar centralizada.</li>
         <li>Utiliza un <em>cover sheet</em> con el mensaje de confidencialidad.</li>
         <li>Verifica el número al cual vas a enviar la información y llama antes de enviar para corroborar que es
             la persona correcta.</li>
         <li>Confirma que el recipiente tiene su fax en un lugar seguro.</li>
         <li>Llama una vez enviado para corroborar que fue recibido por la persona autorizada.</li>
         <li>Recoge los documentos de la máquina inmediatamente.</li>
       </ul>
       <div style="${S.warn}">Un fax que cae en manos equivocadas es una posible violación a la privacidad.</div>`)),

  info('Manejo de contraseñas',
    T(`<ul>
         <li>No permitas que otras personas ni tus compañeros de trabajo utilicen tu cuenta sin antes cerrar sesión
             (<em>log off</em>) del sistema.</li>
         <li>No compartas contraseñas ni reutilices contraseñas expiradas.</li>
         <li>Utiliza contraseñas que no sean fáciles de adivinar (cumpleaños, mascotas, hijos).</li>
         <li>Escoge contraseñas nuevas cuando te toque renovar.</li>
         <li>No escribas las contraseñas en lugares visibles.</li>
         <li>Cambia la contraseña si sospechas que alguien la conoce.</li>
       </ul>
       <div style="${S.call}"><strong>Recomendaciones:</strong> mínimo 8 caracteres, que incluya mayúscula, número y
       carácter especial. Mientras más larga, más fuerte.</div>
       ${fig('contrasenas', 'Afiche: contraseñas más largas hacen contraseñas más fuertes', 250)}`)),

  info('Spam y phishing',
    T(`<p>El <strong>spam</strong> son correos electrónicos no solicitados o "basura". Usualmente toma la forma de
       un anuncio y puede contener virus, <em>spyware</em> o material no apropiado; además obstruye los sistemas de
       correo.</p>
       <p>El <strong>phishing</strong> es una forma particularmente peligrosa de spam que busca engañar a los
       usuarios para que revelen sus contraseñas o información sensitiva.</p>
       <div style="${S.warn}"><strong>Alerta:</strong> nunca divulgues tu contraseña, número de Seguro Social u otra
       información sensitiva por correo, mensaje o teléfono. Ante la duda, repórtalo.</div>`)),

  info('Comunicación en áreas públicas',
    T(`<p>Sé consciente de tu entorno al hablar de información sensitiva que involucre PHI.
       <strong>No discutas información confidencial o PHI en áreas públicas</strong> tales como:</p>
       <ul>
         <li>Pasillos semiprivados</li>
         <li>Elevadores y escaleras</li>
         <li>Salas de espera</li>
         <li>Áreas de tratamiento abiertas</li>
       </ul>
       <div style="${S.call}"><strong>En nuestra operación</strong> añade: el <em>lobby</em> del edificio, el
       estacionamiento, la puerta del cliente y la radio o el altavoz del teléfono dentro del vehículo.</div>
       ${fig('areas-publicas', 'Personas conversando y escuchando en un área pública', 380)}`)),

  info('Eliminación apropiada de documentos con PHI',
    T(`<p>Todo documento que contenga PHI o información confidencial debe destruirse de manera segura.</p>
       <p>Desecha toda copia de documentos confidenciales (faxes, correos impresos, notas informales o notas de
       pacientes) depositándolos en los contenedores designados para ese uso. Los CD-ROM pueden triturarse, rayarse
       en toda su superficie o romperse.</p>
       <div style="${S.warn}"><strong>Nunca dispongas de este tipo de información en los zafacones comunes de la
       facilidad</strong> — ni en el zafacón del vehículo, ni en el de la casa del cliente.</div>
       ${fig('descarte', 'Contenedor designado para destrucción segura de documentos', 290)}`)),

  info('Redes sociales: violaciones comunes',
    T(`<p>Las violaciones más comunes en redes sociales son:</p>
       <ul>
         <li>Publicar imágenes o videos de pacientes sin su consentimiento.</li>
         <li>Publicar chismes sobre los beneficiarios o pacientes.</li>
         <li>Publicar cualquier información que pueda identificar a un paciente.</li>
         <li>Compartir fotografías o imágenes tomadas dentro de la institución en las que sean visibles pacientes o
             información de pacientes.</li>
       </ul>
       <div style="${S.warn}">Han ocurrido despidos y acciones legales por publicar fotos de pacientes en redes
       sociales. Una foto de una entrega puede mostrar una etiqueta con nombre, dirección y medicamento: eso es una
       divulgación de PHI.</div>
       ${fig('redes-sociales', 'Logotipos de redes sociales', 440)}`)),

  info('Realidad: un caso que llegó a la prensa',
    T(`<p style="${S.lead}"><strong>"Despiden a médico por publicar fotos de pacientes que cambiaron de sexo"</strong></p>
       <p>El cirujano publicó en su cuenta de Instagram, entre otras, imágenes de un órgano que había sido retirado a
       un paciente transgénero y al que enmarcó en forma de corazón, junto con el mensaje: "Hay muchas maneras de
       mostrar tu AMOR".</p>
       <div style="${S.warn}">Publicar imágenes de un paciente sin su consentimiento cuesta el empleo — y expone a la
       institución a sanciones y demandas.</div>`)),

  info('Recuerda la privacidad por teléfono',
    T(`<ul>
         <li>Confirma que estás hablando con el paciente o con alguien que tiene permiso para comunicarse por él.</li>
         <li>Para dejar mensajes, provee solo tu nombre, de dónde estás llamando, para quién va dirigido el mensaje
             y la solicitud de que te devuelvan la llamada.</li>
         <li>No uses el altavoz del teléfono mientras atiendes a un paciente o cliente.</li>
       </ul>
       <div style="${S.call}">En un mensaje de voz <strong>no</strong> se menciona el medicamento, la condición ni el
       nombre de la farmacia especializada.</div>
       ${fig('telefono', 'Empleada atendiendo el teléfono con documentos en mano', 260)}`)),

  info('Recordatorios generales',
    T(`<ul>
         <li>Utiliza en todo momento tu identificación.</li>
         <li>Evita almacenar información sensitiva en tus dispositivos y tabletas portátiles.</li>
         <li>Si vas a utilizar una tableta o celular, mantenlo físicamente seguro y a la vista en todo momento.</li>
         <li>Mantén tus contraseñas confidenciales.</li>
         <li>No tomes fotos ni videos bajo ningún concepto en áreas clínicas o donde estén ubicados los pacientes.</li>
         <li>Mantén tu celular en modo de vibración.</li>
         <li>No entres con dispositivos a las áreas de procedimiento: pueden afectar el funcionamiento de los
             equipos.</li>
         <li>Cumple con la política de <strong>uso de dispositivos móviles personales</strong>.</li>
       </ul>`)),

  mc('Vas a enviar un fax con información de un paciente. ¿Cuál es la práctica correcta?',
    ['Enviarlo y confirmar al final del día',
     'Verificar el número, llamar antes de enviar, usar cover sheet de confidencialidad y confirmar el recibo',
     'Enviarlo sin cover sheet para que llegue más rápido',
     'Dejarlo en la máquina para que el destinatario lo recoja cuando pueda'], 'b'),

  mc('Recibes un correo que parece de la empresa pidiéndote confirmar tu usuario y contraseña por un "problema del sistema". ¿Qué haces?',
    ['Contesto con mis credenciales: viene de la empresa',
     'Se las doy solo a mi supervisor por mensaje de texto',
     'No respondo y lo reporto: es un intento de phishing',
     'Cambio mi contraseña por una más corta para recordarla'], 'c'),

  mc('Llegas a la dirección y el paciente no está. Un vecino se ofrece a recibir el medicamento. ¿Qué corresponde?',
    ['Entregarlo al vecino y anotar su nombre',
     'Dejarlo en la puerta con la etiqueta visible',
     'No entregarlo a quien no está autorizado y seguir el procedimiento de reintento o devolución',
     'Llamar al paciente y, si no contesta, dejarlo con el vecino'], 'c'),

  mc('Terminas una entrega y quieres publicar en redes sociales una foto del paquete rotulado. ¿Se puede?',
    ['Sí, si no aparece el paciente en la foto',
     'Sí, si le tapo la dirección con un dedo',
     'No: la etiqueta contiene PHI y publicarla es una divulgación no autorizada',
     'Sí, si la publico en una cuenta privada'], 'c'),

  mc('¿Cómo se descartan los manifiestos y documentos con datos de pacientes?',
    ['En el zafacón del vehículo, doblados',
     'En los contenedores designados para destrucción segura',
     'En el zafacón de la casa, si están rotos por la mitad',
     'Se guardan en la gaveta hasta fin de año'], 'b'),

  ms('Marca TODAS las prácticas correctas al manejar tu tableta de ruta.',
    ['Cerrar sesión cuando termino de usarla',
     'Mantenerla físicamente segura y a la vista',
     'Prestársela a un compañero con mi sesión abierta',
     'Reportar de inmediato si se pierde o me la roban'], ['a', 'b', 'd']),
];
