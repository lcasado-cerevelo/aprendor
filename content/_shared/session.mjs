// ============================================================================
// Inicio de sesión de los seeds contra la API de Aprendor (común a todos los cursos).
//
// - Turnstile: la API lo exige en /auth/login. Los seeds no pueden resolver el widget,
//   así que solo entran por una conexión DIRECTA (sin túnel ni proxy) desde una red de
//   Turnstile:ExemptNetworks: en el servidor, contra http://localhost:8086 con
//   APRENDOR_Turnstile__ExemptNetworks="127.0.0.1/32,::1/128"; en desarrollo ya viene
//   en appsettings.Development.json.
// - Doble factor: si la cuenta lo pide, se usa --totp 123456 (o TP_TOTP) o se pregunta
//   en la consola.
// - La sesión tiene que ser completa (scope full): si la cuenta debe cambiar la clave,
//   dar de alta el autenticador o validar el correo, hay que hacerlo antes en la app.
// ============================================================================

import readline from 'node:readline/promises';

const MENSAJE_TURNSTILE =
  'El servidor pidió la verificación de Turnstile, que un script no puede resolver. ' +
  'Ejecuta el seed en el propio servidor contra http://localhost:<puerto> (sin pasar por el túnel ni por un proxy) ' +
  'y con APRENDOR_Turnstile__ExemptNetworks="127.0.0.1/32,::1/128" en la configuración del sitio.';

const MENSAJE_ALCANCE = {
  'change-password': 'La cuenta tiene que cambiar su contraseña: entra una vez en la app, cámbiala y vuelve a correr el seed.',
  'enroll-2fa': 'La compañía exige verificación en dos pasos y la cuenta no tiene app autenticadora: dala de alta en la app y vuelve a correr el seed.',
  'verify-email': 'La cuenta tiene que validar su correo: hazlo en la app y vuelve a correr el seed.',
};

async function preguntar(texto) {
  if (!process.stdin.isTTY) return null;
  const rl = readline.createInterface({ input: process.stdin, output: process.stdout });
  try { return (await rl.question(texto)).trim(); } finally { rl.close(); }
}

// api(pathname, method, body) es el helper de cada seed (lanza Error con el estado y el cuerpo).
export async function iniciarSesion(api, { email, password, totp = null }) {
  let r;
  try {
    r = await api('/auth/login', 'POST', { email, password });
  } catch (e) {
    if (/turnstileFailed/.test(String(e.message))) throw new Error(MENSAJE_TURNSTILE);
    throw e;
  }

  if (r && r.requires2fa) {
    const code = (totp || process.env.TP_TOTP || await preguntar('Código de la app autenticadora: ') || '').trim();
    if (!code) throw new Error('La cuenta pide el código de la app autenticadora: pásalo con --totp 123456 (o TP_TOTP).');
    r = await api('/auth/2fa/verify', 'POST', { challengeId: r.challengeId, code });
  }

  if (!r || !r.token) throw new Error('El servidor no devolvió una sesión.');
  if (r.scope && r.scope !== 'full')
    throw new Error(MENSAJE_ALCANCE[r.scope] || `La sesión es restringida (${r.scope}): complétala en la app antes de sembrar.`);
  return r;
}
