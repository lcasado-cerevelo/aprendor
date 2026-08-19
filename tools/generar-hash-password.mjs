#!/usr/bin/env node
// ============================================================================
// Genera un hash de contraseña compatible con PasswordHasher (Auth.cs):
// PBKDF2-SHA256, 100,000 iteraciones, sal de 16 bytes, clave de 32 bytes,
// almacenado como "base64(sal).base64(hash)".
//
// ÚLTIMO RECURSO. El camino normal para recuperar acceso es que el admin de
// plataforma resetee la contraseña desde la app (Usuarios → Resetear). Esto es
// para cuando también se perdió la contraseña del admin de plataforma y hay que
// escribir el hash directo en la base del catálogo.
//
//   node tools/generar-hash-password.mjs "MiClaveTemporal123"
//   node tools/generar-hash-password.mjs "MiClaveTemporal123" --email admin@local
//   node tools/generar-hash-password.mjs "MiClaveTemporal123" --verify "sal.hash"
// ============================================================================

import crypto from 'node:crypto';

const ITERATIONS = 100_000;
const SALT_BYTES = 16;
const KEY_BYTES = 32;

const hash = (password, salt) =>
  crypto.pbkdf2Sync(password, salt, ITERATIONS, KEY_BYTES, 'sha256');

function create(password) {
  const salt = crypto.randomBytes(SALT_BYTES);
  return `${salt.toString('base64')}.${hash(password, salt).toString('base64')}`;
}

function verify(password, stored) {
  const [saltB64, expectedB64] = String(stored).split('.');
  if (!saltB64 || !expectedB64) return false;
  const actual = hash(password, Buffer.from(saltB64, 'base64'));
  const expected = Buffer.from(expectedB64, 'base64');
  return actual.length === expected.length && crypto.timingSafeEqual(actual, expected);
}

const args = process.argv.slice(2);
const password = args.find(a => !a.startsWith('--'));
const opt = name => { const i = args.indexOf(`--${name}`); return i >= 0 ? args[i + 1] : null; };

if (!password) {
  console.error('Uso: node tools/generar-hash-password.mjs "NuevaClave" [--email correo@dominio] [--verify "sal.hash"]');
  process.exit(1);
}

const toVerify = opt('verify');
if (toVerify) {
  console.log(verify(password, toVerify)
    ? 'La contraseña SÍ corresponde a ese hash.'
    : 'La contraseña NO corresponde a ese hash.');
  process.exit(0);
}

const value = create(password);
// Comprobación de ida y vuelta: si esto fallara, el hash no serviría para entrar.
if (!verify(password, value)) { console.error('Error interno: el hash generado no se verifica.'); process.exit(1); }

const email = opt('email') || 'CORREO_DEL_USUARIO';
console.log(`\nHash (PBKDF2-SHA256, ${ITERATIONS.toLocaleString('es')} iteraciones):\n${value}\n`);
console.log('SQL para la base del CATÁLOGO (no la del tenant):\n');
console.log(`UPDATE [User]`);
console.log(`   SET PasswordHash = '${value}',`);
console.log(`       MustChangePassword = 1`);
console.log(` WHERE Email = '${email}';\n`);
console.log('Después entra con esa contraseña y cámbiala desde la app (te la va a pedir por MustChangePassword).');
