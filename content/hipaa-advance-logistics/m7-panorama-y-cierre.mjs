import {info, mod, mc, T, S, bar, fig } from './authoring.mjs';

// Módulo 7 — diapositivas 30, 38, 49–51, 55–58 del original.
// Las gráficas del deck se recrean con HTML (barras) en vez de imágenes escaneadas.
export default [
  mod('Módulo 7 — El panorama de las brechas y tu compromiso', 'Duración estimada: 5–7 minutos'),

  info('Las amenazas contra la PHI están creciendo',
    T(`<p>Los ataques de piratería y <em>ransomware</em> dirigidos a la PHI están aumentando, mientras que empleados
       y socios comerciales son cómplices de episodios de acceso no autorizado y divulgación de información.</p>
       <p>Según el HIPAA Journal, el número de violaciones de datos de salud divulgadas en 2021 fue más del doble
       que en 2020, afectando a casi 45 millones de registros de pacientes. Estos riesgos hay que tomarlos en serio y
       seguir construyendo medidas que aseguren nuestra información.</p>
       ${fig('amenazas', 'Phishing, ransomware, DDoS, malware y filtraciones de datos', 460)}`)),

  info('Violaciones de datos por piratería / incidentes de TI',
    T(`<p style="${S.muted}">Incidentes reportados por año. Fuente: HIPAA Journal.</p>
       ${bar('2009', 0, 1, '#cbd5e1')}
       ${bar('2012', 17, 4)}
       ${bar('2015', 56, 10)}
       ${bar('2017', 149, 26)}
       ${bar('2019', 313, 55)}
       ${bar('2020', 457, 80)}
       ${bar('2021', 546, 96)}
       ${bar('2022', 555, 98)}
       <div style="${S.call}">En trece años el problema pasó de ser prácticamente inexistente a más de 500
       incidentes al año. Por eso el adiestramiento se repite todos los años.</div>`)),

  info('Clasificación de las brechas de salud en 2022',
    T(`${bar('Hacking / TI', 555, 96)}
       ${bar('Acceso o divulgación no autorizada', 113, 20, '#0ea5e9')}
       ${bar('Pérdida o robo', 35, 6, '#f59e0b')}
       ${bar('Descarte indebido', 4, 2, '#dc2626')}
       <p style="${S.muted}">Número de brechas reportadas. Fuente: HIPAA Journal.</p>
       <div style="${S.call}">Las tres últimas categorías —divulgación no autorizada, pérdida o robo y descarte
       indebido— son <strong>exactamente los riesgos del trabajo en ruta</strong>, y dependen de la conducta diaria,
       no de la tecnología.</div>`)),

  info('¿Dónde ocurrieron las brechas?',
    T(`${bar('Socios de negocio', 394, 96, '#1e6fd9')}
       ${bar('Proveedores de salud', 252, 61, '#f97316')}
       ${bar('Planes médicos', 61, 15, '#94a3b8')}
       <p style="${S.muted}">Fuente: HIPAA Journal.</p>
       <div style="${S.warn}">La mayor cantidad de brechas ocurre en los <strong>socios de negocio</strong> — la
       categoría en la que estamos nosotros. Somos el eslabón que más se vigila.</div>`)),

  info('Los beneficios del cumplimiento',
    T(`<p>El cumplimiento de HIPAA no es una opción, es un requisito. El principal beneficio de cumplir es que es la
       única manera de evitar multas millonarias, los costos de notificar incumplimientos y mitigar daños, y los
       honorarios legales de las demandas.</p>
       <p>Cumplir con HIPAA también:</p>
       <ul>
         <li>Asegura que los datos de atención médica no se pierdan ni se eliminen accidentalmente.</li>
         <li>Mejora la eficiencia de la empresa.</li>
         <li>Mejora el acceso a la información médica y agiliza la prestación de servicios.</li>
         <li>Permite calificar para incentivos financieros de Uso Significativo.</li>
         <li><strong>Mejora la confianza del paciente</strong> — y la del cliente que nos contrata.</li>
       </ul>`)),

  info('Solo porque puedes, no significa que debas',
    T(`<ul>
         <li>A menos que esté relacionado con tus tareas, no accedas, uses ni divulgues PHI relacionada con
             familiares, amigos, empleados, supervisores u otras personas, aunque ellos te lo soliciten.</li>
         <li>No veas ni imprimas información clínica que no te corresponde.</li>
         <li>No verifiques información financiera de otros.</li>
         <li>No husmees ni compartas información confidencial por curiosidad o solo porque la puedes ver.</li>
       </ul>
       <div style="${S.warn}" ><p style="margin:0;text-align:center;font-weight:700">¿Quisieras que alguien viera tu
       información personal sin tener una razón legítima para hacerlo?</p></div>`)),

  info('Haz tu parte',
    T(`<ul>
         <li>Usa siempre tu identificación durante horas de trabajo.</li>
         <li>Protege la información de los pacientes en áreas accesibles a visitantes u otras personas no
             autorizadas a ver PHI.</li>
         <li>Recuérdale a otros que deben bajar la voz cuando hablen de datos de pacientes frente a terceros.</li>
         <li>Mantén cerradas las puertas con acceso a los <em>counters</em> y áreas de trabajo.</li>
         <li>No uses el altavoz del teléfono cuando estés atendiendo a un paciente o cliente.</li>
         <li>Reporta a tu supervisor cualquier preocupación sobre privacidad o seguridad.</li>
         <li>Evita almacenar información sensitiva en dispositivos portátiles; si los usas, mantenlos seguros y a la
             vista.</li>
         <li>Mantén tus contraseñas confidenciales.</li>
       </ul>`)),

  mc('Según los datos de la industria, ¿dónde ocurre la mayor cantidad de brechas de datos de salud?',
    ['En los planes médicos',
     'En los proveedores de salud',
     'En los socios de negocio',
     'En las agencias de gobierno'], 'c'),

  mc('Un familiar tuyo es paciente de la farmacia y te pide que le confirmes si su medicamento salió en ruta. Tú no tienes esa entrega asignada. ¿Qué haces?',
    ['Se lo confirmo: es mi familiar y me lo pidió',
     'Lo busco en el sistema, pero no se lo digo a nadie más',
     'No accedo a esa información: no está relacionada con mis tareas, y lo refiero a la farmacia',
     'Le pido a un compañero que lo busque por mí'], 'c'),

  info('Cierre',
    T(`<p style="${S.lead}">Gracias por completar el adiestramiento.</p>
       <div style="${S.ok}"><strong>Tres cosas que no se te deben olvidar:</strong>
         <ol style="margin:8px 0 0 18px">
           <li>Solo accedes a la PHI que necesitas para tu trabajo — mínimo necesario.</li>
           <li>Confirmas a quién le entregas y no dejas PHI expuesta ni la descartas en zafacones comunes.</li>
           <li>Si algo se pierde, se entrega mal o se ve comprometido, lo reportas a tu supervisor de inmediato.</li>
         </ol>
       </div>
       <p>Si tienes dudas sobre una situación específica, consulta con tu supervisor o con el Oficial de Privacidad
       y Seguridad antes de actuar.</p>
       <p style="${S.muted}">Contenido basado en el adiestramiento preparado por Marie Carmen Muntaner, Esq. —
       MM &amp; Associates, LLC. Adaptado para Advance Logistics.</p>`)),
];
