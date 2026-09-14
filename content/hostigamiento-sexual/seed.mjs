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

const HERE = path.dirname(fileURLToPath(import.meta.url));

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
  console.log('Título, descripción, recurrencia y certificado actualizados.');

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
  console.log(`Recurrencia ${course.training.recurrenceMonths} meses y certificado configurados.`);

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
  console.log(`Curso: ${course.training.title}`);
  console.log(`  Módulos:   ${count('ModuleHeader')}`);
  console.log(`  Contenido: ${count('Info')} pantallas`);
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
    if (it.type === 'Info' && (!p.blocks || !p.blocks.length)) errs.push(`${where}: sin bloques.`);
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

function buildPreview() {
  let n = 0;
  const body = course.items.map(it => {
    const p = it.payload;
    if (it.type === 'ModuleHeader')
      return `<section class="mod"><h2>${esc(p.title)}</h2>${p.subtitle ? `<p>${esc(p.subtitle)}</p>` : ''}</section>`;
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
