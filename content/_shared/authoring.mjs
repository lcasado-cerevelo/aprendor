// ============================================================================
// Helpers de autoría — construyen los payloads que espera la plataforma.
// Compartido por todos los cursos en content/*/ (ver README de cada curso).
//
// Tipos y formas de payload (ver wwwroot/index.html y Phase2.cs):
//   ModuleHeader   { title, subtitle }
//   Info           { title, blocks:[{type:'text',html,span}|{type:'media',...}],
//                    layout?, photo?, photoPos?, variant?, kicker? }   (ver slide() más abajo)
//   MultipleChoice { question, options:[{id,text}], correctOptionId }
//   MultiSelect    { question, options:[{id,text}], correctOptionIds }   (todo o nada)
//   Matching       { question, choices:[{id,text}], prompts:[{id,text,correctChoiceId}] }
// El servidor corrige (Phase2.cs:Score) y nunca envía la respuesta correcta al learner.
// ============================================================================

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

export const T = (html, span = 'full') => ({ type: 'text', html, span });

// Imagen o video ya subido a la plataforma (/media/{id}, del mismo tenant): se reutiliza
// tal cual. Con span 'half' y otro bloque 'half' al lado, van en dos columnas.
export const media = (mediaUrl, mediaType = 'image/jpeg', span = 'full') => ({ type: 'media', mediaUrl, mediaType, span });

// Ítem que se sube DESACTIVADO: queda en el borrador (se ve en la app y se puede activar)
// pero el empleado no lo ve ni cuenta para la nota. Para el banco de preguntas que queda
// fuera del subconjunto.
export const off = item => ({ ...item, active: false });

// Ilustración local embebida como data URL (para que course.json sea autosuficiente,
// igual que el curso HIPAA original). `moduleUrl` es el import.meta.url del archivo
// que llama a fig(); `relPath` es la ruta al archivo de imagen relativa a ese módulo
// (normalmente './img/nombre.jpg').
export const fig = (moduleUrl, relPath, alt, credit, maxWidth = 900) => {
  const filePath = path.join(path.dirname(fileURLToPath(moduleUrl)), relPath);
  const ext = path.extname(filePath).slice(1).toLowerCase();
  const mime = ext === 'jpg' ? 'jpeg' : ext;
  const b64 = fs.readFileSync(filePath).toString('base64');
  return `<figure style="margin:16px 0">
     <img src="data:image/${mime};base64,${b64}" alt="${alt}"
          style="width:100%;max-height:340px;object-fit:cover;border-radius:12px;display:block">
     ${credit ? `<figcaption style="font-size:11px;color:#94a3b8;margin-top:4px;text-align:right">${credit}</figcaption>` : ''}
   </figure>`;
};

export const info = (title, ...blocks) => ({ type: 'Info', payload: { title, blocks } });

// ---- Modo presentación (PlayerConfig.presentation) -------------------------
// Cuando el curso tiene `presentation.enabled`, el reproductor dibuja cada pantalla
// Info como una lámina 16:9 (1280×720) según su `layout`:
//   cover       foto a sangre + banda blanca con el título (portada)
//   dark        fondo oscuro, `kicker` en mayúsculas arriba (por defecto el título del
//               panel), línea de acento, y el título + cuerpo debajo
//   split       panel diagonal oscuro a la izquierda con el título del panel, contenido
//               a la derecha sobre blanco. `variant`: 'right' (panel blanco a la izquierda
//               y contenido sobre oscuro a la derecha) o 'lines' (fondo oscuro, título
//               del panel entre dos líneas de acento).
//   photo-left  foto a la izquierda (~40 %), contenido a la derecha. `photoPos` es el
//               object-position CSS para elegir qué parte de la foto se ve.
//   band        franja de acento arriba con el rótulo (`kicker`) y el título, contenido
//               debajo.
//   cards       rótulo y título arriba con una barra corta de acento; cada viñeta de la
//               primera lista se dibuja como tarjeta (2-3 por fila).
//   callout     una idea destacada: el título y el cuerpo en una caja de acento centrada.
// Con theme.mode 'light' (cursos de Adiestramiento) todo va sobre blanco: dark, pregunta y
// resumen pasan a fondo blanco con una barra de acento, el panel diagonal se pinta con el
// acento y las listas numeradas (<ol>) salen como pasos en círculos. Conviene alternar
// composiciones y no abusar del panel diagonal.
//   intro       PANTALLA DE ENTRADA, no una lámina: sólo puede ser el primer ítem del
//               curso (ver intro() más abajo). En modo presentación el reproductor la
//               saca de la numeración y la usa para dibujar la pantalla previa al botón
//               «Comenzar» (foto atenuada, título, descripción y datos del curso).
// Sin modo presentación, el reproductor ignora estos campos y la pantalla se ve como
// cualquier otra Info (título, foto si la hay, bloques). El cuerpo de la lámina se
// escribe en HTML semántico (p, ul/li, b, h3) sin estilos inline: la hoja de estilos
// del reproductor lo viste como el deck; en modo clásico se ve como texto normal.

// Foto local como data URL (sin <figure>): para `photo` de cover / photo-left.
export const photo = (moduleUrl, relPath) => {
  const filePath = path.join(path.dirname(fileURLToPath(moduleUrl)), relPath);
  const ext = path.extname(filePath).slice(1).toLowerCase();
  const mime = ext === 'jpg' ? 'jpeg' : ext;
  return `data:image/${mime};base64,${fs.readFileSync(filePath).toString('base64')}`;
};

export const LAYOUTS = ['cover', 'dark', 'split', 'photo-left', 'band', 'cards', 'callout', 'intro'];
export const SPLIT_VARIANTS = ['left', 'right', 'lines'];

export const slide = ({ layout = 'dark', title = '', photo, photoPos, variant, kicker }, ...blocks) => {
  if (!LAYOUTS.includes(layout)) throw new Error(`layout desconocido: ${layout}`);
  if (layout === 'intro') throw new Error('la pantalla de entrada se escribe con intro(), no con slide()');
  if (variant && !SPLIT_VARIANTS.includes(variant)) throw new Error(`variant desconocida: ${variant}`);
  const payload = { title, layout, blocks };
  if (photo) payload.photo = photo;
  if (photoPos) payload.photoPos = photoPos;
  if (variant) payload.variant = variant;
  if (kicker) payload.kicker = kicker;
  return { type: 'Info', payload };
};

// Pantalla de entrada del curso (layout 'intro'). Va SIEMPRE como primer ítem (seed.mjs
// lo valida) y no lleva bloques: el reproductor calcula por su cuenta los datos del curso
// (láminas, preguntas, puntos y la aprobación si la conoce) y los muestra junto a:
//   title        título grande del curso
//   description  resumen para el empleado, en HTML (p, b…) o texto plano
//   minutes      tiempo estimado en minutos (opcional)
//   photo        data URL de la foto de fondo, atenuada (opcional; si falta, el
//                reproductor usa la de la primera lámina 'cover')
// En modo presentación no cuenta como lámina (queda fuera del contador «n / N»); en modo
// clásico se ve como primera página con una caja de datos del mismo estilo que resumen().
export const intro = ({ title, description, minutes, photo }) => {
  if (!title) throw new Error('intro(): falta el título');
  if (!description) throw new Error('intro(): falta la descripción');
  const payload = { layout: 'intro', title, description };
  if (minutes) payload.minutes = minutes;
  if (photo) payload.photo = photo;
  return { type: 'Info', payload };
};

// Viñetas al estilo del deck. Cada elemento es texto, o [texto, [sub-viñetas]].
export const bullets = items =>
  `<ul>${items.map(it => Array.isArray(it)
    ? `<li>${it[0]}${bullets(it[1])}</li>`
    : `<li>${it}</li>`).join('')}</ul>`;

// Subtítulo de sección dentro de una lámina (p. ej. "INSTRUCCIONES", "Empleados").
export const heading = text => `<h3>${text}</h3>`;

export const mod = (title, subtitle = '') => ({ type: 'ModuleHeader', payload: { title, subtitle } });

const opts = arr => arr.map((text, i) => ({ id: String.fromCharCode(97 + i), text }));

export const mc = (question, options, correctOptionId, points = 10) =>
  ({ type: 'MultipleChoice', points, payload: { question, options: opts(options), correctOptionId } });

export const ms = (question, options, correctOptionIds, points = 10) =>
  ({ type: 'MultiSelect', points, payload: { question, options: opts(options), correctOptionIds } });

export const match = (question, choices, prompts, points = 10) => ({
  type: 'Matching', points,
  payload: {
    question,
    choices: choices.map(([id, text]) => ({ id, text })),
    prompts: prompts.map(([id, text, correctChoiceId]) => ({ id, text, correctChoiceId })),
  },
});

// ---- Estilos inline: el player inyecta el HTML tal cual, sin hoja de estilo propia ----
export const S = {
  lead:   'font-size:17px;line-height:1.6',
  call:   'background:#eff6ff;border-left:4px solid #2563eb;padding:12px 16px;border-radius:8px;margin:14px 0',
  warn:   'background:#fef2f2;border-left:4px solid #dc2626;padding:12px 16px;border-radius:8px;margin:14px 0',
  ok:     'background:#f0fdf4;border-left:4px solid #16a34a;padding:12px 16px;border-radius:8px;margin:14px 0',
  grid:   'display:grid;grid-template-columns:repeat(auto-fit,minmax(230px,1fr));gap:12px;margin-top:14px',
  card:   'background:#1e6fd9;color:#fff;padding:14px 16px;border-radius:10px;font-size:15px;line-height:1.45',
  cardS:  'background:#e0f2fe;color:#0c4a6e;padding:14px 16px;border-radius:10px;font-size:15px;line-height:1.45',
  cardG:  'background:#16a34a;color:#fff;padding:14px 16px;border-radius:10px;font-size:15px;line-height:1.45',
  cols2:  'display:grid;grid-template-columns:repeat(auto-fit,minmax(260px,1fr));gap:0 28px',
  muted:  'color:#64748b;font-size:14px',
};

// Caja de resumen del curso para la portada (mismo patrón en todo el catálogo:
// portada con ícono + descripción + esta caja, luego Instrucciones/Objetivo).
// `minutos` es una estimación de duración (lectura de pantallas + preguntas).
export const resumen = ({ modulos, tipos, puntos, aprobacion = 70, minutos }) =>
  `<div style="background:#f8fafc;border:1px solid #e2e8f0;border-radius:10px;padding:14px 16px;` +
  `margin:16px auto 0;max-width:440px;color:#334155;font-size:14px">` +
  `<div style="margin:4px 0">📦 <b>${modulos} módulos</b></div>` +
  (minutos ? `<div style="margin:4px 0">⏱ Duración estimada: <b>${minutos} min</b></div>` : '') +
  `<div style="margin:4px 0">📝 ${tipos} (${puntos} puntos)</div>` +
  `<div style="margin:4px 0">✅ Aprobación: <b>${aprobacion}%</b></div></div>`;

// Barra horizontal para gráficas simples sin imágenes.
export const bar = (label, value, pct, color = '#2563eb') =>
  `<div style="display:flex;align-items:center;gap:10px;margin:7px 0">
     <span style="width:70px;flex:none;font-size:13px;color:#475569">${label}</span>
     <span style="flex:none;width:${pct}%;height:16px;background:${color};border-radius:4px"></span>
     <span style="font-size:13px;font-weight:700;color:#0f172a">${value}</span>
   </div>`;

// ---- Bloques dinámicos añadidos para los 4 cursos de 2026 ------------------

// Foto externa (Unsplash) con crédito al fotógrafo. `url` debe ser el enlace
// directo a la imagen (images.unsplash.com/photo-...), no la página del sitio.
export const img = (url, alt, credit) =>
  `<figure style="margin:16px 0">
     <img src="${url}" alt="${alt}" loading="lazy"
          style="width:100%;max-height:320px;object-fit:cover;border-radius:12px;display:block">
     ${credit ? `<figcaption style="font-size:11px;color:#94a3b8;margin-top:4px;text-align:right">${credit}</figcaption>` : ''}
   </figure>`;

// Video de YouTube incrustado (modo privacidad ampliada), responsivo 16:9.
// Se marca como material complementario opcional: no todos los decks fuente
// traían video, así que no debe leerse como requisito para aprobar.
//
// OJO: muchos YouTube Shorts devuelven "Error 153" al incrustarse así, aunque
// el enlace normal (youtube.com/shorts/<id> o /watch?v=<id>) sí funcione sin
// iniciar sesión. Antes de usar este helper, prueba el ID en
// https://www.youtube-nocookie.com/embed/<id> — si da Error 153, usa
// `resources()` con el enlace normal en vez de incrustarlo.
export const video = (youtubeId, title) =>
  `<div style="margin:16px 0">
     <div style="position:relative;padding-top:56.25%;border-radius:12px;overflow:hidden;background:#0f172a">
       <iframe src="https://www.youtube-nocookie.com/embed/${youtubeId}" title="${title}"
               style="position:absolute;inset:0;width:100%;height:100%;border:0"
               loading="lazy" allow="accelerometer; encrypted-media; gyroscope; picture-in-picture"
               allowfullscreen></iframe>
     </div>
     <p style="${S.muted};margin-top:6px">🎥 Video complementario (opcional): ${title}</p>
   </div>`;

// Caja de "Recursos adicionales" con enlaces externos verificados.
export const resources = (items) =>
  `<div style="background:#f8fafc;border:1px solid #e2e8f0;border-radius:10px;padding:14px 16px;margin:14px 0">
     <div style="font-weight:700;margin-bottom:6px">🔗 Recursos adicionales</div>
     <ul style="margin:0;padding-left:20px">
       ${items.map(([label, url]) => `<li style="margin:4px 0"><a href="${url}" target="_blank" rel="noopener">${label}</a></li>`).join('')}
     </ul>
   </div>`;

// Lista de pasos numerados (procedimientos: qué hacer en orden).
export const steps = (items, color = '#2563eb') =>
  `<div style="margin:14px 0">
     ${items.map((text, i) => `
       <div style="display:flex;gap:12px;align-items:flex-start;margin:10px 0">
         <span style="flex:none;width:26px;height:26px;border-radius:50%;background:${color};color:#fff;
                      display:flex;align-items:center;justify-content:center;font-size:13px;font-weight:700">${i + 1}</span>
         <span style="padding-top:3px">${text}</span>
       </div>`).join('')}
   </div>`;

// Fila de tarjetas de estadística/dato clave (números que resaltan).
export const statGrid = (items, color = '#2563eb') =>
  `<div style="display:grid;grid-template-columns:repeat(auto-fit,minmax(140px,1fr));gap:10px;margin:14px 0">
     ${items.map(([value, label]) => `
       <div style="background:${color}12;border:1px solid ${color}33;border-radius:10px;padding:12px;text-align:center">
         <div style="font-size:22px;font-weight:800;color:${color}">${value}</div>
         <div style="font-size:12px;color:#475569;margin-top:2px">${label}</div>
       </div>`).join('')}
   </div>`;

// Lista con marcadores circulares (más visual que un <ul> plano).
export const badgeList = (items, color = '#2563eb', icon = '✓') =>
  `<div style="margin:12px 0">
     ${items.map(text => `
       <div style="display:flex;gap:10px;align-items:flex-start;margin:8px 0">
         <span style="flex:none;width:22px;height:22px;border-radius:50%;background:${color}1a;color:${color};
                      display:flex;align-items:center;justify-content:center;font-size:12px;font-weight:700">${icon}</span>
         <span>${text}</span>
       </div>`).join('')}
   </div>`;

// Cita/definición destacada.
export const quote = (text, color = '#2563eb') =>
  `<blockquote style="margin:16px 0;padding:14px 18px;border-left:4px solid ${color};
               background:${color}0d;border-radius:0 10px 10px 0;font-size:16px;font-style:italic;color:#1e293b">
     ${text}
   </blockquote>`;

// Fila de chips/etiquetas pequeñas.
export const chips = (items, color = '#2563eb') =>
  `<div style="display:flex;flex-wrap:wrap;gap:8px;margin:12px 0">
     ${items.map(text => `<span style="background:${color}1a;color:${color};padding:5px 12px;border-radius:999px;
                                 font-size:13px;font-weight:600">${text}</span>`).join('')}
   </div>`;
