// Recorta las ilustraciones de las diapositivas escaneadas.
//
// Detección: los píxeles con saturación de color se agrupan en bloques de 4x4,
// se buscan componentes conectados y se escoge el más grande. Así la ilustración
// (una mancha grande y continua) gana sobre el texto de color, los bullets y la
// banda azul del pie (que se descarta por ser ancha y muy delgada).
import * as mupdf from 'mupdf';
import fs from 'node:fs';

const SRC = process.argv[2];
const OUT = process.argv[3];
const doc = mupdf.Document.openDocument(fs.readFileSync(SRC), 'application/pdf');
fs.mkdirSync(OUT, { recursive: true });

// box: recorte manual en fracciones [x0, y0, x1, y1] de la diapositiva ya rotada.
// Sin box, se detecta automáticamente la mancha de color más grande.
const FIGS = [
  { page: 5,  rot: 90,  name: 'hipaa-logo',           box: [0.50, 0.33, 0.88, 0.65] },
  { page: 9,  rot: -90, name: 'cadena-de-confianza',  box: [0.69, 0.29, 0.96, 0.76] },
  { page: 23, rot: 90,  name: 'salvaguardas-tecnicas' },
  { page: 25, rot: 90,  name: 'hitech' },
  { page: 26, rot: 90,  name: 'breach-alerta' },
  { page: 27, rot: 90,  name: 'breach-notification' },
  { page: 29, rot: 90,  name: 'costo-violaciones' },
  { page: 30, rot: 90,  name: 'amenazas',             box: [0.32, 0.54, 0.69, 0.80] },
  { page: 31, rot: 90,  name: 'entrega-hipaa',        box: [0.36, 0.31, 0.60, 0.55] },
  { page: 42, rot: 90,  name: 'contrasenas',          box: [0.66, 0.32, 0.94, 0.80] },
  { page: 44, rot: 90,  name: 'areas-publicas',       box: [0.55, 0.35, 0.89, 0.78] },
  { page: 45, rot: 90,  name: 'descarte',             box: [0.63, 0.37, 0.89, 0.74] },
  { page: 46, rot: 90,  name: 'redes-sociales' },
  { page: 52, rot: 90,  name: 'telefono' },
];

const SAT = 45;
const ANALYZE_DPI = 80;
const BLOCK = 4;
const CROP_DPI = 170;

function render(page, rot, dpi, bbox = null) {
  const m = mupdf.Matrix.concat(mupdf.Matrix.scale(dpi / 72, dpi / 72), mupdf.Matrix.rotate(rot));
  if (!bbox) return page.toPixmap(m, mupdf.ColorSpace.DeviceRGB, false, true);
  const pix = new mupdf.Pixmap(mupdf.ColorSpace.DeviceRGB, bbox, false);
  pix.clear(255);
  const dev = new mupdf.DrawDevice(mupdf.Matrix.identity, pix);
  page.run(dev, m);
  dev.close();
  return pix;
}

// Mapa de bloques 4x4 marcados si contienen suficiente color.
function colorBlocks(pix) {
  const w = pix.getWidth(), h = pix.getHeight(), n = pix.getNumberOfComponents();
  const stride = pix.getStride(), px = pix.getPixels();
  const bw = Math.ceil(w / BLOCK), bh = Math.ceil(h / BLOCK);
  const count = new Int32Array(bw * bh);
  for (let y = 0; y < h; y++) {
    for (let x = 0; x < w; x++) {
      const i = y * stride + x * n;
      const r = px[i], g = px[i + 1], b = px[i + 2];
      if (Math.max(r, g, b) - Math.min(r, g, b) > SAT) count[((y / BLOCK) | 0) * bw + ((x / BLOCK) | 0)]++;
    }
  }
  const on = new Uint8Array(bw * bh);
  for (let i = 0; i < count.length; i++) on[i] = count[i] >= 6 ? 1 : 0;  // 6 de 16 píxeles
  return { on, bw, bh };
}

// Componente conectado más grande, ignorando bandas (muy anchas y delgadas) y bordes del escáner.
function largestBlob({ on, bw, bh }) {
  const seen = new Uint8Array(bw * bh);
  let best = null;
  const stack = [];
  for (let s = 0; s < on.length; s++) {
    if (!on[s] || seen[s]) continue;
    stack.length = 0; stack.push(s); seen[s] = 1;
    let area = 0, x0 = bw, y0 = bh, x1 = -1, y1 = -1;
    while (stack.length) {
      const i = stack.pop();
      const x = i % bw, y = (i / bw) | 0;
      area++;
      if (x < x0) x0 = x; if (x > x1) x1 = x;
      if (y < y0) y0 = y; if (y > y1) y1 = y;
      for (let dy = -1; dy <= 1; dy++) {
        for (let dx = -1; dx <= 1; dx++) {
          const nx = x + dx, ny = y + dy;
          if (nx < 0 || ny < 0 || nx >= bw || ny >= bh) continue;
          const j = ny * bw + nx;
          if (on[j] && !seen[j]) { seen[j] = 1; stack.push(j); }
        }
      }
    }
    const w = x1 - x0 + 1, h = y1 - y0 + 1;
    const banda = w > bw * 0.7 && h < bh * 0.10;          // banda azul del pie
    const borde = (w < bw * 0.05 && h > bh * 0.5) || (h < bh * 0.05 && w > bw * 0.5); // borde del escáner
    if (banda || borde || area < 40) continue;
    if (!best || area > best.area) best = { area, x0, y0, x1, y1 };
  }
  return best;
}

const report = [];
for (const f of FIGS) {
  const page = doc.loadPage(f.page - 1);
  const small = render(page, f.rot, ANALYZE_DPI);
  const bounds = small.getBounds();
  const k = CROP_DPI / ANALYZE_DPI;
  let dev;

  if (f.box) {
    const w = small.getWidth(), h = small.getHeight();
    dev = [
      Math.floor((bounds[0] + f.box[0] * w) * k),
      Math.floor((bounds[1] + f.box[1] * h) * k),
      Math.ceil((bounds[0] + f.box[2] * w) * k),
      Math.ceil((bounds[1] + f.box[3] * h) * k),
    ];
  } else {
    const blob = largestBlob(colorBlocks(small));
    if (!blob) { report.push(`${f.name}: sin figura`); continue; }
    const pad = 2; // en bloques
    dev = [
      Math.floor((bounds[0] + (blob.x0 - pad) * BLOCK) * k),
      Math.floor((bounds[1] + (blob.y0 - pad) * BLOCK) * k),
      Math.ceil((bounds[0] + (blob.x1 + 1 + pad) * BLOCK) * k),
      Math.ceil((bounds[1] + (blob.y1 + 1 + pad) * BLOCK) * k),
    ];
  }
  const crop = render(page, f.rot, CROP_DPI, dev);
  fs.writeFileSync(`${OUT}/${f.name}.jpg`, crop.asJPEG(78));
  const pctW = (((dev[2]-dev[0])/k) / small.getWidth() * 100).toFixed(0);
  const pctH = (((dev[3]-dev[1])/k) / small.getHeight() * 100).toFixed(0);
  report.push(`${f.name}: ${crop.getWidth()}x${crop.getHeight()}  (${pctW}% x ${pctH}% de la diapositiva)`);
}
console.log(report.join('\n'));
