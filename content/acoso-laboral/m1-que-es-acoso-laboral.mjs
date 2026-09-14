import { info, T, S, img, fig, quote, chips } from '../_shared/authoring.mjs';

export default [
  info('¿Qué es el acoso laboral?',
    T(`${img('https://images.unsplash.com/photo-1758518731706-be5d5230e5a5?fm=jpg&q=60&w=1200&auto=format&fit=crop',
        'Compañeros de trabajo conversando de forma respetuosa en la oficina',
        'Foto: Vitaly Gariev / Unsplash')}
       ${quote('Conducta malintencionada, no deseada, repetitiva y abusiva que atenta contra la reputación y la vida privada o familiar, y crea un entorno de trabajo intimidante, humillante, hostil u ofensivo.', '#4338ca')}`)),

  info('La Ley Núm. 90-2020',
    T(`${fig(import.meta.url, './img/exclusion.jpg',
        'Un grupo de fichas del mismo color y una ficha distinta apartada del grupo',
        'Foto: Markus Spiske / Unsplash')}
       <p>La Ley Núm. 90-2020 establece una política pública para prohibir y prevenir el acoso laboral en
       Puerto Rico. La Ley reconoce que el acoso laboral puede ocurrir:</p>
       ${chips(['Entre supervisor y empleado', 'Entre empleados del mismo nivel', 'Aun con acosador en posición inferior'], '#4338ca')}
       <div style="${S.call}">El acoso laboral no depende necesariamente de una relación jerárquica.</div>`)),

  info('Conductas que pueden constituir acoso laboral',
    T(`${fig(import.meta.url, './img/senalando.jpg',
        'Varias manos señalando con el dedo hacia una misma persona',
        'Foto: Maulana Ahmad / Unsplash')}
       <ul>
         <li>Expresiones injuriosas, difamatorias o lesivas</li>
         <li>Palabras soeces dirigidas hacia una persona</li>
         <li>Comentarios hostiles o humillantes sobre su desempeño profesional</li>
         <li>Amenazas injustificadas de despido</li>
         <li>Descalificación humillante de sus opiniones o propuestas de trabajo</li>
         <li>Burlas o comentarios humillantes sobre su apariencia</li>
         <li>Exponer públicamente asuntos relacionados con la intimidad personal o familiar</li>
       </ul>
       <p style="${S.muted}">La Ley establece que esta lista no es exclusiva.</p>`)),
];
