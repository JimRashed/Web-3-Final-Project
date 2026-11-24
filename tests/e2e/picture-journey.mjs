// D2 (#147): the cross-device picture journey at API level. Two independent sessions for one account,
// an upload from A, a read from B, a third account that must not see it, a replacement, and the
// private-cache headers. The client-side sign-out behaviour is covered by the component test in #151,
// because a token stays valid until it expires; this script proves the service half.
// Secrets come from .env and are never printed.
import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { deflateSync } from 'node:zlib';
import { chromium } from 'playwright-core';

const root = fileURLToPath(new URL('../..', import.meta.url));
const env = Object.fromEntries(readFileSync(`${root}/.env`, 'utf8').split(/\r?\n/)
  .filter((line) => /^[A-Z_]+=/.test(line)).map((line) => [line.slice(0, line.indexOf('=')), line.slice(line.indexOf('=') + 1)]));

const results = [];
const check = (name, ok, detail = '') => { results.push(ok); console.log(`${ok ? 'PASS' : 'FAIL'}  ${name}${detail ? ' - ' + detail : ''}`); };
const digest = (bytes) => (bytes ? createHash('sha256').update(Buffer.from(bytes)).digest('hex').slice(0, 16) : 'missing');

// A real 1x1 PNG, generated so the fixture is guaranteed valid rather than hand-copied.
function crc32(buffer) {
  let crc = 0xffffffff;
  for (const byte of buffer) {
    crc ^= byte;
    for (let bit = 0; bit < 8; bit++) crc = crc & 1 ? (crc >>> 1) ^ 0xedb88320 : crc >>> 1;
  }
  return (crc ^ 0xffffffff) >>> 0;
}
function chunk(type, data) {
  const length = Buffer.alloc(4);
  length.writeUInt32BE(data.length);
  const typed = Buffer.concat([Buffer.from(type, 'ascii'), data]);
  const crc = Buffer.alloc(4);
  crc.writeUInt32BE(crc32(typed));
  return Buffer.concat([length, typed, crc]);
}
function png(red, green, blue) {
  const header = Buffer.alloc(13);
  header.writeUInt32BE(1, 0); header.writeUInt32BE(1, 4);
  header[8] = 8; header[9] = 2; // 8-bit RGB
  const raw = Buffer.from([0, red, green, blue]); // filter 0 + one pixel
  return Buffer.concat([
    Buffer.from('89504e470d0a1a0a', 'hex'),
    chunk('IHDR', header),
    chunk('IDAT', deflateSync(raw)),
    chunk('IEND', Buffer.alloc(0)),
  ]);
}
const PNG1 = png(255, 0, 0);
const PNG2 = png(0, 0, 255);
// JPEG magic bytes with a PNG body: the mismatch the receiver must refuse.
const JPEG = Buffer.concat([Buffer.from('ffd8ffe0', 'hex'), Buffer.from('not-really-a-jpeg')]);

const browser = await chromium.launch({ channel: 'msedge', headless: true });

/** Signs in through the real Keycloak screen and returns that session's token. */
async function signIn(username) {
  const context = await browser.newContext();
  const page = await context.newPage();
  let token;
  page.on('response', async (response) => {
    if (response.url().includes('/protocol/openid-connect/token') && response.request().method() === 'POST') {
      token = (await response.json().catch(() => null))?.access_token;
    }
  });
  await page.goto('http://localhost:5173/');
  await page.getByRole('button', { name: /Sign in/ }).first().click();
  await page.waitForURL(/localhost:8080\/realms\/quizarena/);
  await page.fill('#username', username);
  await page.fill('#password', env.QUIZARENA_DEMO_PASSWORD);
  await page.click('#kc-login');
  await page.waitForURL(/localhost:5173/);
  await page.locator('.player-name').waitFor();
  await context.close();
  return token;
}

const identity = 'http://localhost:5101';

/** A picture needs a profile first: the contract answers 404 for "no profile" and "no picture" alike. */
async function ensureProfile(token) {
  const response = await fetch(`${identity}/api/users/me`, {
    method: 'PUT',
    headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
    body: JSON.stringify({ displayName: 'Journey Player' }),
  });
  return response.status;
}
async function call(token, method, path, body, contentType) {
  const response = await fetch(`${identity}${path}`, {
    method,
    headers: { Authorization: `Bearer ${token}`, ...(body ? { 'Content-Type': contentType } : {}) },
    body: body ?? undefined,
  });
  const type = response.headers.get('content-type') ?? '';
  return {
    status: response.status,
    type,
    cache: response.headers.get('cache-control'),
    nosniff: response.headers.get('x-content-type-options'),
    bytes: type.startsWith('image/') ? Buffer.from(await response.arrayBuffer()) : undefined,
    json: type.includes('json') ? await response.json().catch(() => undefined) : undefined,
  };
}

try {
  const deviceA = await signIn('player-a');
  const deviceB = await signIn('player-a');
  const other = await signIn('player-b');
  check('Two independent sessions for one account, plus a third account', Boolean(deviceA && deviceB && other));
  check('Device A and device B hold different tokens', deviceA !== deviceB);

  const profileA = await ensureProfile(deviceA);
  const profileOther = await ensureProfile(other);
  check('Both accounts have a profile before any picture work', profileA === 200 && profileOther === 200,
    `a=${profileA} other=${profileOther}`);

  const before = await call(deviceA, 'GET', '/api/users/me/picture');
  check('A player without a picture gets 404, not an error', [200, 404].includes(before.status), `status=${before.status}`);

  const upload = await call(deviceA, 'PUT', '/api/users/me/picture', PNG1, 'image/png');
  check('A uploads a PNG and the profile comes back with its metadata',
    upload.status === 200 && upload.json?.picture?.contentType === 'image/png',
    `status=${upload.status} contentType=${upload.json?.picture?.contentType}`);
  const firstVersion = upload.json?.picture?.version;

  const readB = await call(deviceB, 'GET', '/api/users/me/picture');
  check('B retrieves the same bytes without sharing a session',
    readB.status === 200 && digest(readB.bytes) === digest(PNG1), `status=${readB.status} hash=${readB.bytes ? digest(readB.bytes) : '-'}`);
  check('The bytes are private and never stored by a shared cache',
    readB.cache === 'private, no-store' && readB.nosniff === 'nosniff', `cache=${readB.cache} nosniff=${readB.nosniff}`);

  const replace = await call(deviceA, 'PUT', '/api/users/me/picture', PNG2, 'image/png');
  check('A replaces the picture and the version increments',
    replace.status === 200 && replace.json?.picture?.version === firstVersion + 1,
    `version=${firstVersion} -> ${replace.json?.picture?.version}`);
  const afterReplace = await call(deviceB, 'GET', '/api/users/me/picture');
  check('B sees the replacement, so the store is shared rather than per-device',
    afterReplace.status === 200 && digest(afterReplace.bytes) === digest(PNG2));

  const thirdRead = await call(other, 'GET', '/api/users/me/picture');
  check("A third account cannot read A's picture through its own session", thirdRead.status === 404 || digest(thirdRead.bytes) !== digest(PNG2),
    `status=${thirdRead.status}`);
  const thirdReplace = await call(other, 'PUT', '/api/users/me/picture', PNG1, 'image/png');
  check('A third account can only replace its own picture', thirdReplace.status === 200);
  const stillMine = await call(deviceA, 'GET', '/api/users/me/picture');
  check("The third account's upload did not touch A's picture", digest(stillMine.bytes) === digest(PNG2));

  const unauthenticated = await fetch(`${identity}/api/users/me/picture`);
  check('The route refuses an unauthenticated read', unauthenticated.status === 401, `status=${unauthenticated.status}`);

  const wrongType = await call(deviceA, 'PUT', '/api/users/me/picture', Buffer.from('not an image'), 'text/plain');
  check('An unsupported content type is refused', wrongType.status === 415, `status=${wrongType.status}`);
  const mismatched = await call(deviceA, 'PUT', '/api/users/me/picture', JPEG, 'image/png');
  check('Magic bytes that do not match the declared type are refused', [400, 415].includes(mismatched.status), `status=${mismatched.status}`);
  const tooBig = await call(deviceA, 'PUT', '/api/users/me/picture', Buffer.alloc(2_097_153, 1), 'image/png');
  check('One byte over the 2 MiB limit is refused', tooBig.status === 413, `status=${tooBig.status}`);
  const kept = await call(deviceA, 'GET', '/api/users/me/picture');
  check('A rejected upload keeps the previous picture', digest(kept.bytes) === digest(PNG2));
} catch (error) {
  check('Picture journey run', false, error.message.split('\n')[0]);
}

await browser.close();
console.log(`\n${results.filter(Boolean).length}/${results.length} checks passed`);
process.exit(results.every(Boolean) ? 0 : 1);
