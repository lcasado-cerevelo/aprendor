// ============================================================================
// Helpers de autoría — construyen los payloads que espera la plataforma.
//
// Tipos y formas de payload (ver wwwroot/index.html y Phase2.cs):
//   ModuleHeader   { title, subtitle }
//   Info           { title, blocks:[{type:'text',html,span}|{type:'media',...}] }
//   MultipleChoice { question, options:[{id,text}], correctOptionId }
//   MultiSelect    { question, options:[{id,text}], correctOptionIds }   (todo o nada)
//   Matching       { question, choices:[{id,text}], prompts:[{id,text,correctChoiceId}] }
// El servidor corrige (Phase2.cs:Score) y nunca envía la respuesta correcta al learner.
// ============================================================================

import fs from 'node:fs';

export const T = (html, span = 'full') => ({ type: 'text', html, span });

// Ilustraciones recortadas del PDF original (ver tools/extraer-figuras.mjs).
// Se incrustan como data URL para que course.json sea autosuficiente: así el
// servidor de producción solo necesita ese archivo, sin subir medios aparte.
export const fig = (name, alt, maxWidth = 420) => {
  const b64 = fs.readFileSync(new URL(`./img/${name}.jpg`, import.meta.url)).toString('base64');
  return `<img src="data:image/jpeg;base64,${b64}" alt="${alt}" ` +
         `style="max-width:${maxWidth}px;width:100%;height:auto;border-radius:10px;display:block;margin:16px auto">`;
};

export const info = (title, ...blocks) => ({ type: 'Info', payload: { title, blocks } });

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

// Barra horizontal para recrear las gráficas del deck sin imágenes.
export const bar = (label, value, pct, color = '#2563eb') =>
  `<div style="display:flex;align-items:center;gap:10px;margin:7px 0">
     <span style="width:70px;flex:none;font-size:13px;color:#475569">${label}</span>
     <span style="flex:none;width:${pct}%;height:16px;background:${color};border-radius:4px"></span>
     <span style="font-size:13px;font-weight:700;color:#0f172a">${value}</span>
   </div>`;
