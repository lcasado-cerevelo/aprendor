#!/usr/bin/env node
// ============================================================================
// Siembra o actualiza este curso en la base del tenant a través de la API.
//
//   node seed.mjs --url http://localhost:52045 --email autor@cliente.com --password ***
//   node seed.mjs ... --update         actualiza el curso YA EXISTENTE (por título
//                                      exacto) en vez de crear uno nuevo: reemplaza
//                                      todos sus ítems y refresca título, descripción,
//                                      recurrencia y certificado. No crea un Training
//                                      nuevo ni duplica el curso.
//   node seed.mjs ... --publish        publica la versión al terminar
//   node seed.mjs --dry-run            no llama a la API; escribe course.json
//   node seed.mjs --preview            escribe preview.html (revisión sin servidor)
//
// Requiere Node 18+ (usa fetch nativo). El usuario debe tener rol Admin, Author
// o Moderator en el tenant destino: los ítems se crean en la base del tenant
// que resuelve su token.
// ============================================================================

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import course from './course.mjs';
import { LAYOUTS, SPLIT_VARIANTS } from '../_shared/authoring.mjs';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const PLAYER_HTML = path.join(HERE, '..', '..', 'wwwroot', 'player.html');

// ---- Argumentos -------------------------------------------------------------
const args = process.argv.slice(2);
const flag = name => args.includes(`--${name}`);
const opt = (name, fallback = null) => {
  const i = args.indexOf(`--${name}`);
  return i >= 0 && args[i + 1] ? args[i + 1] : fallback;
};

const BASE = (opt('url', process.env.TP_URL || 'http://localhost:52045')).replace(/\/$/, '');
const EMAIL = opt('email', process.env.TP_EMAIL);
const PASSWORD = opt('password', process.env.TP_PASSWORD);
const PUBLISH = flag('publish');
const DRY = flag('dry-run');
const PREVIEW = flag('preview');
const UPDATE = flag('update');

// ---- Validación del contenido (corre siempre, antes de tocar nada) ---------
validate();

// ---- Vista previa estática: revisión sin levantar el servidor --------------
if (PREVIEW) {
  const out = opt('out', path.join(HERE, 'preview.html'));
  fs.writeFileSync(out, buildPreview(), 'utf8');
  summary();
  console.log(`\nVista previa escrita en ${out} — ábrela en el navegador.`);
  if (!DRY) process.exit(0);
}

// ---- Salida seca: solo genera el JSON del curso ----------------------------
if (DRY) {
  const out = opt('out', path.join(HERE, 'course.json'));
  const payload = {
    training: course.training,
    items: course.items.map(it => ({ type: it.type, points: it.points ?? 0, payload: it.payload })),
  };
  fs.writeFileSync(out, JSON.stringify(payload, null, 2), 'utf8');
  summary();
  console.log(`\nJSON escrito en ${out} (no se llamó a la API).`);
  process.exit(0);
}

if (!EMAIL || !PASSWORD) {
  console.error('Faltan credenciales. Usa --email y --password (o TP_EMAIL / TP_PASSWORD).');
  process.exit(1);
}

// ---- Cliente HTTP -----------------------------------------------------------
let token = null;
async function api(pathname, method = 'GET', body = null) {
  const res = await fetch(BASE + pathname, {
    method,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: 'Bearer ' + token } : {}),
    },
    body: body ? JSON.stringify(body) : null,
  });
  const text = await res.text();
  const data = text ? JSON.parse(text) : null;
  if (!res.ok) throw new Error(`${method} ${pathname} → ${res.status} ${text || res.statusText}`);
  return data;
}

// ---- Subida de ítems, en orden (sin afterItemId se añaden al final) --------
async function uploadItems(trainingId) {
  let n = 0;
  for (const it of course.items) {
    await api(`/trainings/${trainingId}/items`, 'POST', {
      type: it.type,
      payloadJson: JSON.stringify(it.payload),
      points: it.points ?? 0,
      required: true,
      active: true,
    });
    n++;
    const label = it.payload.title || it.payload.question || '';
    process.stdout.write(`\r  ${String(n).padStart(3)}/${course.items.length}  ${it.type.padEnd(14)} ${label.slice(0, 48).padEnd(48)}`);
  }
  console.log('');
  return n;
}

// ---- Opciones del reproductor (modo presentación, volver atrás, etc.) -------
// PUT /trainings/{id}/player-config exige allowBack; el bloque presentation es opcional
// y reemplaza al guardado. Si el curso no define playerConfig, no se toca nada.
async function playerConfig(trainingId) {
  const pc = course.training.playerConfig;
  if (!pc) return;
  await api(`/trainings/${trainingId}/player-config`, 'PUT', { allowBack: true, ...pc });
}

// ---- Modo --update: reemplaza el contenido del curso YA EXISTENTE ----------
// No llama a POST /trainings ni POST /categories — nunca crea un curso nuevo.
async function updateExisting() {
  const trainings = await api('/trainings');
  const existing = (trainings || []).find(t => t.title === course.training.title);
  if (!existing) {
    throw new Error(
      `No hay ningún curso con el título exacto "${course.training.title}" en este tenant. ` +
      `Corre sin --update si quieres crearlo de nuevo.`
    );
  }
  console.log(`Curso existente: ${existing.title} (${existing.id}), status actual: ${existing.status}`);

  await api(`/trainings/${existing.id}`, 'PUT', {
    title: course.training.title,
    description: course.training.description,
  });
  await api(`/trainings/${existing.id}/recurrence`, 'POST', {
    recurrenceMonths: course.training.recurrenceMonths,
    renewLeadDays: course.training.renewLeadDays,
  });
  await api(`/trainings/${existing.id}/certificate-config`, 'PUT', course.training.certificate);
  await playerConfig(existing.id);
  console.log('Título, descripción, recurrencia, certificado y opciones del reproductor actualizados.');

  const draft = await api(`/trainings/${existing.id}/draft`);
  console.log(`Borrando ${draft.items.length} ítems existentes del borrador...`);
  for (const it of draft.items) {
    await api(`/items/${it.id}`, 'DELETE');
  }

  console.log('Subiendo el contenido actualizado:');
  const n = await uploadItems(existing.id);
  console.log(`${n} ítems nuevos creados (reemplazan a los anteriores).`);

  if (PUBLISH) {
    const v = await api(`/trainings/${existing.id}/publish`, 'POST');
    console.log(`Publicado: versión ${v.versionNumber}.`);
  } else {
    console.log('Queda en BORRADOR con el contenido actualizado. Publícalo desde la app, o corre de nuevo con --publish.');
  }
}

// ---- Modo por defecto: crea un curso nuevo ----------------------------------
async function createNew() {
  // Categoría (reutiliza la existente si ya está creada)
  const wanted = course.training.category;
  const categories = await api('/categories');
  let category = (categories || []).find(c => c.name?.toLowerCase() === wanted.toLowerCase());
  if (!category) {
    category = await api('/categories', 'POST', { name: wanted, parentId: null });
    console.log(`Categoría creada: ${wanted}`);
  } else {
    console.log(`Categoría existente: ${wanted}`);
  }

  const training = await api('/trainings', 'POST', {
    title: course.training.title,
    description: course.training.description,
    categoryId: category.id,
  });
  console.log(`Curso creado: ${training.title} (${training.id})`);

  await api(`/trainings/${training.id}/recurrence`, 'POST', {
    recurrenceMonths: course.training.recurrenceMonths,
    renewLeadDays: course.training.renewLeadDays,
  });
  await api(`/trainings/${training.id}/certificate-config`, 'PUT', course.training.certificate);
  await playerConfig(training.id);
  console.log(`Recurrencia ${course.training.recurrenceMonths} meses, certificado y opciones del reproductor configurados.`);

  const n = await uploadItems(training.id);
  console.log(`${n} ítems creados en el borrador.`);

  if (PUBLISH) {
    const v = await api(`/trainings/${training.id}/publish`, 'POST');
    console.log(`Publicado: versión ${v.versionNumber}.`);
  } else {
    console.log('Queda en BORRADOR. Revísalo en la app y publícalo desde ahí, o corre de nuevo con --publish.');
  }
}

// ---- Siembra ----------------------------------------------------------------
async function main() {
  summary();
  console.log(`\nServidor: ${BASE}`);

  const login = await api('/auth/login', 'POST', { email: EMAIL, password: PASSWORD });
  token = login.token;
  console.log(`Autenticado como ${login.user.name || login.user.email} (${login.user.role}).`);
  if (!login.user.tenantId) throw new Error('Ese usuario no pertenece a un tenant; usa un autor del tenant destino.');

  if (UPDATE) await updateExisting();
  else await createNew();

  console.log(`\nVista previa: ${BASE}/index.html → Contenido del curso "${course.training.title}".`);
}

// ---- Resumen del contenido --------------------------------------------------
function summary() {
  const count = t => course.items.filter(i => i.type === t).length;
  const questions = course.items.filter(i => i.points > 0);
  const points = questions.reduce((s, i) => s + i.points, 0);
  const pres = course.training.playerConfig?.presentation?.enabled;
  const entry = introOf();
  console.log(`Curso: ${course.training.title}`);
  if (entry) console.log(`  Entrada:   pantalla de entrada${entry.minutes ? ` (${entry.minutes} min estimados)` : ''}, fuera de la numeración`);
  if (count('ModuleHeader')) console.log(`  Módulos:   ${count('ModuleHeader')}`);
  console.log(`  Contenido: ${count('Info') - (entry ? 1 : 0)} ${pres ? 'láminas (modo presentación)' : 'pantallas'}`);
  console.log(`  Preguntas: ${questions.length} (${count('MultipleChoice')} selección única, ` +
              `${count('MultiSelect')} selección múltiple, ${count('Matching')} pareo) — ${points} puntos`);
  console.log(`  Aprobación: 70% → ${Math.ceil(points * 0.7)} puntos`);
}

// ---- Validación -------------------------------------------------------------
// Comprueba lo que la plataforma da por sentado al corregir (Phase2.cs:Score):
// que las respuestas correctas existan entre las opciones y que los pareos
// apunten a opciones válidas. Un error aquí sería una pregunta imposible de
// aprobar en producción.
function validate() {
  const errs = [];
  course.items.forEach((it, i) => {
    const p = it.payload, where = `ítem #${i + 1} (${it.type})`;
    if (it.type === 'Info' && p.layout === 'intro') {
      // Pantalla de entrada (intro()): sólo como primer ítem; no lleva bloques.
      if (i !== 0) errs.push(`${where}: la pantalla de entrada (layout intro) sólo puede ser el primer ítem del curso.`);
      if (!p.title) errs.push(`${where}: la pantalla de entrada no tiene título.`);
      if (!p.description) errs.push(`${where}: la pantalla de entrada no tiene descripción.`);
      if (p.minutes !== undefined && !(Number(p.minutes) > 0)) errs.push(`${where}: minutes debe ser un número positivo.`);
    } else if (it.type === 'Info') {
      // Una portada (cover) puede no tener bloques: la foto y el título son la lámina.
      if ((!p.blocks || !p.blocks.length) && p.layout !== 'cover') errs.push(`${where}: sin bloques.`);
      if (p.layout && !LAYOUTS.includes(p.layout)) errs.push(`${where}: layout desconocido "${p.layout}".`);
      if ((p.layout === 'cover' || p.layout === 'photo-left') && !p.photo) errs.push(`${where}: el layout ${p.layout} necesita foto.`);
      if (p.variant && !SPLIT_VARIANTS.includes(p.variant)) errs.push(`${where}: variant desconocida "${p.variant}".`);
    }
    if (it.type === 'ModuleHeader' && !p.title) errs.push(`${where}: sin título.`);
    if (it.type === 'MultipleChoice' || it.type === 'MultiSelect') {
      const ids = (p.options || []).map(o => o.id);
      if (ids.length < 2) errs.push(`${where}: necesita 2+ opciones.`);
      if (!p.question) errs.push(`${where}: sin pregunta.`);
      if (!it.points) errs.push(`${where}: sin puntos.`);
      const correct = it.type === 'MultipleChoice' ? [p.correctOptionId] : (p.correctOptionIds || []);
      if (!correct.length) errs.push(`${where}: sin respuesta correcta.`);
      correct.forEach(c => { if (!ids.includes(c)) errs.push(`${where}: la correcta "${c}" no está entre las opciones.`); });
    }
    if (it.type === 'Matching') {
      const ids = (p.choices || []).map(c => c.id);
      if (ids.length < 2) errs.push(`${where}: necesita 2+ opciones.`);
      (p.prompts || []).forEach(pr => {
        if (!pr.correctChoiceId) errs.push(`${where}: el par "${pr.text}" no tiene opción correcta.`);
        else if (!ids.includes(pr.correctChoiceId)) errs.push(`${where}: el par "${pr.text}" apunta a una opción inexistente.`);
      });
      if (!(p.prompts || []).length) errs.push(`${where}: sin pares.`);
    }
  });
  if (errs.length) {
    console.error('El contenido tiene problemas:\n  ' + errs.join('\n  '));
    process.exit(1);
  }
}

// ---- Vista previa estática --------------------------------------------------
function esc(s) { return String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;'); }

// Pantalla de entrada (ítem Info con layout 'intro', siempre el primero) o null.
function introOf() {
  const first = course.items[0];
  return first && first.type === 'Info' && first.payload.layout === 'intro' ? first.payload : null;
}
// Réplica de introStats/introScreenHtml de player.html: mismos datos que calcula el
// reproductor (láminas = Info sin el intro, preguntas, suma de puntos, aprobación y
// minutos). La aprobación del reproductor sale de la configuración si la trae; aquí se
// usa el 70 % que asume el resumen de este script. Si cambia una, cambiar la otra.
function introDescHtml(d) { return /<[a-z][\s\S]*>/i.test(d || '') ? d : `<p>${esc(d || '')}</p>`; }
function introStats(p) {
  return {
    slides: course.items.filter(it => it.type === 'Info' && it.payload.layout !== 'intro').length,
    questions: course.items.filter(it => ['MultipleChoice', 'MultiSelect', 'Matching', 'OpenResponse'].includes(it.type)).length,
    points: course.items.reduce((s, it) => s + (Number(it.points) || 0), 0),
    pass: 70,
    minutes: Number(p.minutes) || 0,
  };
}
function introScreenHtml(p, st) {
  const pl = (n, uno, varios) => n === 1 ? uno : varios;
  const cells = [[st.slides, pl(st.slides, 'lámina', 'láminas')], [st.questions, pl(st.questions, 'pregunta', 'preguntas')], [st.points, 'puntos']];
  if (st.pass != null) cells.push([st.pass + ' %', 'para aprobar']);
  if (st.minutes) cells.push([st.minutes + ' min', 'tiempo estimado']);
  return `<div class="ps-rule"></div><div class="ps-desc">${introDescHtml(p.description)}</div>` +
         `<div class="ps-stats">${cells.map(([v, l]) => `<div class="ps-stat"><b>${v}</b><span>${l}</span></div>`).join('')}</div>`;
}

// Con modo presentación, la vista previa dibuja cada lámina en un escenario 16:9 con
// la misma hoja de estilos del reproductor (bloque PRES-CSS de wwwroot/player.html),
// marcando la respuesta correcta en verde y avisando «Excede la lámina» si el cuerpo
// no cabe en los 720 px. Sin modo presentación, la vista previa clásica de siempre.
function buildPreview() {
  const pres = course.training.playerConfig?.presentation;
  return pres?.enabled ? buildSlidePreview(pres) : buildClassicPreview();
}

// Hoja de estilos del reproductor, para que la revisión se vea igual que el curso real.
function playerCss() {
  try {
    const html = fs.readFileSync(PLAYER_HTML, 'utf8');
    const m = html.match(/\/\*PRES-CSS-START\*\/([\s\S]*?)\/\*PRES-CSS-END\*\//);
    if (m) return m[1];
  } catch { /* sin player.html a mano: se avisa abajo */ }
  console.warn(`Aviso: no se encontró el bloque PRES-CSS en ${PLAYER_HTML}; la vista previa saldrá sin estilo de lámina.`);
  return '';
}

// Réplica en Node de presInfoSlide/presPageHtml de player.html (misma estructura de
// clases, para que la CSS copiada aplique igual). Si cambia una, cambiar la otra.
function slideHtml(it, ctx) {
  const p = it.payload;
  const wrap = (cls, chrome, body, style = '') =>
    `<div class="sl ${cls}"${style ? ` style="${style}"` : ''}>${chrome}<div class="sl-body"><div class="sl-fit">${body}</div></div></div>`;
  const panel = ctx.panelTitle;
  if (it.type === 'ModuleHeader')
    return wrap('sl-dark sl-mod', `<h1 class="sl-kicker">${esc(p.title)}</h1><div class="sl-rule"></div>`, `<p>${esc(p.subtitle || '')}</p>`);
  if (it.type === 'Info') {
    const layout = p.layout || 'dark';
    const body = (p.blocks || []).map(b => b.html || '').join('');
    const tt = p.title ? `<h2>${esc(p.title)}</h2>` : '';
    if (layout === 'cover')
      return wrap('sl-cover', `<img class="cover-photo" src="${p.photo || ''}" alt="" /><div class="cover-band"><h1>${esc(p.title || '')}</h1></div>`, body);
    if (layout === 'photo-left')
      return wrap('sl-photo', `<img class="photo" src="${p.photo || ''}" alt="" /><h1 class="sl-kicker">${esc(p.kicker || panel)}</h1><div class="sl-rule"></div>`,
                  tt + body, p.photoPos ? `--ppos:${p.photoPos}` : '');
    if (layout === 'split') {
      const v = (p.variant && p.variant !== 'left') ? ' ' + p.variant : '';
      const np = ctx.panel === false ? ' no-panel' : '';
      return wrap('sl-split' + v + np, `<div class="panel"></div><h1 class="ptitle">${esc(panel)}</h1>${np ? `<div class="sl-kicker">${esc(panel)}</div>` : ''}`, tt + body);
    }
    const sub = (p.title && !/<h3[\s>]/i.test(body)) ? `<h3>${esc(p.title)}</h3>` : '';
    return wrap('sl-dark', `<h1 class="sl-kicker">${esc(p.kicker || panel)}</h1><div class="sl-rule"></div>`, sub + body);
  }
  // Pregunta: como la ve el learner, pero con la correcta marcada en verde.
  const n = ++ctx.q;
  const correct = it.type === 'MultipleChoice' ? [p.correctOptionId] : (p.correctOptionIds || []);
  const kind = it.type === 'MultipleChoice' ? '' : it.type === 'MultiSelect' ? ' <span class="mk">(varias correctas)</span>' : ' <span class="mk">(empareja cada uno)</span>';
  const rows = it.type === 'Matching'
    ? (p.prompts || []).map(pr => {
        const c = (p.choices || []).find(x => x.id === pr.correctChoiceId);
        return `<div class="matchrow"><div class="mp">${esc(pr.text)}</div><div class="opt correct" style="flex:1">${esc(c ? c.text : '?')}</div></div>`;
      }).join('')
    : (p.options || []).map(o => `<label class="opt ${correct.includes(o.id) ? 'correct' : ''}">${esc(o.text)}${correct.includes(o.id) ? '<span class="mk">✔ correcta</span>' : ''}</label>`).join('');
  return wrap('sl-dark sl-q', `<div class="sl-frame"></div><h1 class="sl-kicker">Pregunta ${n}</h1><div class="sl-rule"></div>`,
              `<div class="qwrap"><b>${esc(p.question)}</b>${kind}<div>${rows}</div></div>`);
}

function buildSlidePreview(pres) {
  const theme = { bg: '#0d0d0d', accent: '#f97316', panel: true, panelTitle: '', ...(pres.theme || {}) };
  const ctx = { q: 0, panel: theme.panel, panelTitle: (theme.panelTitle || '').trim() || course.training.title.toUpperCase() };
  // La pantalla de entrada va aparte, antes de las láminas: no se numera ni entra en el
  // chequeo de desborde (no es una .sl), igual que en el reproductor.
  const entry = introOf();
  const entryHtml = entry ? (() => {
    const ph = entry.photo || course.items.find(it => it.type === 'Info' && it.payload.layout === 'cover' && it.payload.photo)?.payload.photo;
    return `<section><span class="tag">Pantalla de entrada · no cuenta como lámina</span>
      <div class="frame entry"><div class="pres-start intro">${ph ? `<img class="ps-photo" src="${ph}" alt="" /><div class="ps-shade"></div>` : ''}
        <div class="ps-inner"><h1>${entry.title}</h1>${introScreenHtml(entry, introStats(entry))}
          <button class="pbtn" type="button">Comenzar</button>
          <div class="ps-hint">Pantalla completa · avanza con → o Enter</div></div></div></div></section>`;
  })() : '';
  const slides = course.items.filter(it => it.payload !== entry).map((it, i) => {
    const label = it.type === 'Info' ? `Lámina ${i + 1} · ${it.payload.layout || 'dark'}${it.payload.variant ? ' / ' + it.payload.variant : ''}`
                : it.type === 'ModuleHeader' ? `Lámina ${i + 1} · módulo`
                : `Lámina ${i + 1} · ${it.type} · ${it.points} pts`;
    return `<section><span class="tag">${label}</span>
      <div class="frame"><div class="pstage"><div class="player"><div class="slide">${slideHtml(it, ctx)}</div></div></div></div></section>`;
  }).join('\n');
  const questions = course.items.filter(i => i.points > 0);
  const points = questions.reduce((s, i) => s + i.points, 0);

  return `<!doctype html><html lang="es"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>${esc(course.training.title)} — vista previa</title>
<style>
  :root{color-scheme:light}
  body{margin:0;background:#1a1a1a;color:#e5e5e5;font-family:system-ui,-apple-system,Segoe UI,Roboto,sans-serif;line-height:1.5}
  header{background:#000;color:#fff;padding:22px 20px;border-bottom:2px solid ${theme.accent}}
  header h1{margin:0 0 6px;font-size:20px}
  header p{margin:0;opacity:.8;font-size:14px;max-width:900px}
  main{max-width:1000px;margin:0 auto;padding:20px}
  section{margin:22px 0}
  .tag{display:block;font-size:12px;text-transform:uppercase;letter-spacing:.05em;color:#9a9a9a;margin-bottom:6px}
  .frame{position:relative;width:100%;aspect-ratio:16/9;overflow:hidden;background:${theme.bg};box-shadow:0 8px 30px rgba(0,0,0,.5)}
  .note{background:#fef9c3;color:#713f12;border-radius:10px;padding:12px 16px;font-size:14px}
  /* Estilos del reproductor (copiados de wwwroot/player.html) */
  ${playerCss()}
  /* La vista previa escala cada escenario al ancho de su marco, no a la ventana */
  .frame .pstage{left:0;top:0;transform:scale(var(--pk,1));transform-origin:top left}
  /* La pantalla de entrada ocupa el marco (en el reproductor cubre la ventana) */
  .frame.entry .pres-start{position:absolute;overflow:hidden}
</style></head><body>
<header>
  <h1>${esc(course.training.title)}</h1>
  <p>${esc(course.training.description)}</p>
</header>
<main>
  <div class="note"><strong>Vista previa para revisión (modo presentación).</strong> Cada lámina se ve como en el
  reproductor, con la respuesta correcta marcada en verde. Si el cuerpo de una lámina no cabe, aparece el aviso
  <b>«Excede la lámina»</b> — hay que recortar el texto o repartirlo. ${questions.length} preguntas · ${points} puntos · aprueba con 70%.</div>
  ${entryHtml}
  ${slides}
</main>
<script>
  // Escala cada escenario al ancho del marco y marca las láminas que desbordan
  // (misma regla que presFitOverflow en player.html, sin encoger: aquí se avisa).
  const root = document.documentElement;
  root.style.setProperty('--pbg', ${JSON.stringify(theme.bg)});
  root.style.setProperty('--pacc', ${JSON.stringify(theme.accent)});
  function fit(){ document.querySelectorAll('.frame').forEach(f => f.querySelector('.pstage').style.setProperty('--pk', f.clientWidth / 1280)); }
  function check(){
    document.querySelectorAll('.sl').forEach(s => {
      const body = s.querySelector('.sl-body'), fitEl = s.querySelector('.sl-fit');
      if (!body || !fitEl || s.querySelector('.sl-over')) return;
      if (fitEl.scrollHeight > body.clientHeight + 1) { const b = document.createElement('div'); b.className = 'sl-over'; b.textContent = 'Excede la lámina'; s.appendChild(b); }
    });
  }
  fit(); addEventListener('resize', fit); addEventListener('load', check); setTimeout(check, 300);
</script>
</body></html>`;
}

function buildClassicPreview() {
  let n = 0;
  const body = course.items.map(it => {
    const p = it.payload;
    if (it.type === 'ModuleHeader')
      return `<section class="mod"><h2>${esc(p.title)}</h2>${p.subtitle ? `<p>${esc(p.subtitle)}</p>` : ''}</section>`;
    if (it.type === 'Info' && p.layout === 'intro') {
      // Pantalla de entrada: en modo clásico el reproductor la muestra como primera página.
      const st = introStats(p);
      return `<section class="screen"><span class="tag">Pantalla de entrada</span>
                <h3>${esc(p.title || '')}</h3>${introDescHtml(p.description)}
                <p class="tag">${st.slides} pantallas · ${st.questions} preguntas (${st.points} puntos) · aprueba con ${st.pass}%${st.minutes ? ` · ${st.minutes} min` : ''}</p></section>`;
    }
    if (it.type === 'Info')
      return `<section class="screen"><span class="tag">Pantalla ${++n}</span>
                <h3>${esc(p.title || '')}</h3>${(p.blocks || []).map(b => b.html || '').join('')}</section>`;
    const kind = it.type === 'MultipleChoice' ? 'Selección única'
              : it.type === 'MultiSelect' ? 'Selección múltiple (todo o nada)' : 'Pareo';
    const correct = it.type === 'MultipleChoice' ? [p.correctOptionId] : (p.correctOptionIds || []);
    const rows = it.type === 'Matching'
      ? (p.prompts || []).map(pr => {
          const c = (p.choices || []).find(x => x.id === pr.correctChoiceId);
          return `<li>${esc(pr.text)} <span class="ok">→ ${esc(c ? c.text : '?')}</span></li>`;
        }).join('')
      : (p.options || []).map(o =>
          `<li class="${correct.includes(o.id) ? 'ok' : ''}">${correct.includes(o.id) ? '✔ ' : ''}${esc(o.text)}</li>`).join('');
    return `<section class="q"><span class="tag">${kind} · ${it.points} pts</span>
              <h3>${esc(p.question)}</h3><ul>${rows}</ul></section>`;
  }).join('\n');

  return `<!doctype html><html lang="es"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>${esc(course.training.title)} — vista previa</title>
<style>
  :root{color-scheme:light}
  body{margin:0;background:#f1f5f9;color:#0f172a;font-family:system-ui,-apple-system,Segoe UI,Roboto,sans-serif;line-height:1.55}
  header{background:#1e6fd9;color:#fff;padding:26px 20px}
  header h1{margin:0 0 6px;font-size:22px}
  header p{margin:0;opacity:.9;font-size:14px;max-width:820px}
  main{max-width:860px;margin:0 auto;padding:20px}
  section{background:#fff;border:1px solid #e2e8f0;border-radius:12px;padding:20px 22px;margin:14px 0}
  section.mod{background:#0f172a;color:#fff;border:0}
  section.mod h2{margin:0;font-size:19px}
  section.mod p{margin:6px 0 0;opacity:.8;font-size:14px}
  section.q{border-left:4px solid #16a34a}
  h3{margin:6px 0 12px;font-size:18px}
  .tag{display:inline-block;font-size:12px;text-transform:uppercase;letter-spacing:.04em;color:#64748b}
  ul{margin:0;padding-left:20px}
  section.q li{margin:4px 0}
  .ok{color:#15803d;font-weight:600}
  .note{background:#fef9c3;border:1px solid #fde047;border-radius:10px;padding:12px 16px;font-size:14px}
</style></head><body>
<header>
  <h1>${esc(course.training.title)}</h1>
  <p>${esc(course.training.description)}</p>
</header>
<main>
  <div class="note"><strong>Vista previa para revisión.</strong> Muestra todo el contenido en orden y las respuestas
  correctas marcadas en verde — no es la pantalla que ve el learner. ${course.items.filter(i => i.points > 0).length}
  preguntas · ${course.items.filter(i => i.points > 0).reduce((s, i) => s + i.points, 0)} puntos · aprueba con 70%.</div>
  ${body}
</main></body></html>`;
}

main().catch(e => { console.error('\nError:', e.message); process.exit(1); });
