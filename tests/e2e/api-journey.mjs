import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { chromium } from 'playwright-core';

const root = fileURLToPath(new URL('../..', import.meta.url));

const env = Object.fromEntries(readFileSync(`${root}/.env`, 'utf8').split(/\r?\n/)
  .filter((line) => /^[A-Z_]+=/.test(line)).map((line) => [line.slice(0, line.indexOf('=')), line.slice(line.indexOf('=') + 1)]));
const results = [];
const check = (name, ok, detail = '') => { results.push(ok); console.log(`${ok ? 'PASS' : 'FAIL'}  ${name}${detail ? ' - ' + detail : ''}`); };
const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));
const browser = await chromium.launch({ channel: 'msedge', headless: true });

async function signIn(username) {
  const context = await browser.newContext();
  const page = await context.newPage();
  let token;
  page.on('response', async (r) => {
    if (r.url().includes('/protocol/openid-connect/token') && r.request().method() === 'POST') token = (await r.json().catch(() => null))?.access_token;
  });
  await page.goto('http://localhost:5173/');
  await page.getByRole('button', { name: /Sign in/ }).first().click();
  await page.waitForURL(/localhost:8080\/realms\/quizarena/);
  await page.fill('#username', username);
  await page.fill('#password', env.QUIZARENA_DEMO_PASSWORD);
  await page.click('#kc-login');
  await page.waitForURL(/localhost:5173/);
  await page.locator('.player-name').waitFor();
  const sub = JSON.parse(Buffer.from(token.split('.')[1], 'base64url')).sub;
  return { page, token, sub };
}

async function call(who, method, url, body) {
  const response = await fetch(url, {
    method, headers: { Authorization: `Bearer ${who.token}`, ...(body ? { 'Content-Type': 'application/json' } : {}) },
    body: body ? JSON.stringify(body) : undefined,
  });
  const text = await response.text();
  return { status: response.status, json: text ? JSON.parse(text) : undefined };
}

try {
  const host = await signIn('player-a');
  const guest = await signIn('player-b');
  check('Both players signed in through Keycloak (distinct subjects)', host.sub !== guest.sub);

  const before = {
    host: (await call(host, 'GET', `http://localhost:5102/api/players/${host.sub}/stats`)).json,
    guest: (await call(guest, 'GET', `http://localhost:5102/api/players/${guest.sub}/stats`)).json,
  };

  const created = await call(host, 'POST', 'http://localhost:5104/api/lobbies', {});
  const code = created.json?.joinCode;
  const joined = await call(guest, 'POST', `http://localhost:5104/api/lobbies/${code}/join`, {});
  const locked = await call(host, 'POST', `http://localhost:5104/api/lobbies/${code}/lock`);
  check('Lobby: host creates, guest joins, host locks', created.status === 201 || created.status === 200 ? joined.status === 200 && locked.status === 200 && locked.json?.status === 'Locked' : false,
    `create=${created.status} join=${joined.status} lock=${locked.status}`);

  const guestStart = await call(guest, 'POST', 'http://localhost:5105/matches', { joinCode: code });
  const start = await call(host, 'POST', 'http://localhost:5105/matches', { joinCode: code });
  const repeat = await call(host, 'POST', 'http://localhost:5105/matches', { joinCode: code });
  const matchId = start.json?.matchId;
  check('Gameplay: host starts (201, verified against Lobby, five QuestionBank snapshots with machine token)', start.status === 201 && !!matchId, `status=${start.status}`);
  check('Gameplay: guest cannot start (403) and a repeated start returns the same match (200)', guestStart.status === 403 && repeat.status === 200 && repeat.json?.matchId === matchId,
    `guest=${guestStart.status} repeat=${repeat.status}`);

  let rounds = 0;
  for (let round = 0; round < 5; round++) {
    const q = await call(host, 'GET', `http://localhost:5105/matches/${encodeURIComponent(matchId)}/next-question`);
    if (q.status !== 200) { check(`Round ${round + 1} question`, false, `status=${q.status}`); break; }
    if ('correctAnswer' in q.json) { check('Question never carries the answer', false); break; }
    // Both answer at the same moment: exercises the concurrency handling from #107.
    const [a, b] = await Promise.all([
      call(host, 'POST', `http://localhost:5105/matches/${encodeURIComponent(matchId)}/answers`, { questionId: q.json.questionId, answer: q.json.options[0] }),
      call(guest, 'POST', `http://localhost:5105/matches/${encodeURIComponent(matchId)}/answers`, { questionId: q.json.questionId, answer: q.json.options[1] }),
    ]);
    if (a.status !== 200 || b.status !== 200) { check(`Round ${round + 1} simultaneous answers`, false, `host=${a.status} guest=${b.status}`); break; }
    rounds++;
  }
  check('Five rounds played with both players answering simultaneously (no 409/500)', rounds === 5, `rounds=${rounds}`);

  const final = await call(guest, 'GET', `http://localhost:5105/matches/${encodeURIComponent(matchId)}`);
  check('Match finished', final.json?.status === 'FINISHED', `score ${final.json?.playerAScore}-${final.json?.playerBScore} winner=${final.json?.winnerUserId ? (final.json.winnerUserId === host.sub ? 'host' : 'guest') : 'draw'}`);

  // The outbox delivers results to Statistics and events to Notification in the background.
  let statsOk = false; let eventsOk = false; let after; let hostEvents; let guestEvents;
  for (let i = 0; i < 30 && !(statsOk && eventsOk); i++) {
    await sleep(2000);
    after = {
      host: (await call(host, 'GET', `http://localhost:5102/api/players/${host.sub}/stats`)).json,
      guest: (await call(guest, 'GET', `http://localhost:5102/api/players/${guest.sub}/stats`)).json,
    };
    statsOk = after.host.gamesPlayed === before.host.gamesPlayed + 1 && after.guest.gamesPlayed === before.guest.gamesPlayed + 1;
    hostEvents = (await call(host, 'GET', 'http://localhost:5106/events')).json ?? [];
    guestEvents = (await call(guest, 'GET', 'http://localhost:5106/events')).json ?? [];
    const types = (list) => list.filter((e) => e.payload?.matchId === matchId).map((e) => e.type).sort().join(',');
    eventsOk = types(hostEvents) === 'MATCH_COMPLETED,MATCH_CREATED' && types(guestEvents) === 'MATCH_COMPLETED,MATCH_CREATED';
  }
  check('Outbox delivered one result per player to Statistics', statsOk,
    `host games ${before.host.gamesPlayed}->${after.host.gamesPlayed}, guest ${before.guest.gamesPlayed}->${after.guest.gamesPlayed}`);
  check('Outbox delivered MATCH_CREATED and MATCH_COMPLETED to each player in Notification', eventsOk);

  const history = (await call(host, 'GET', `http://localhost:5102/api/players/${host.sub}/matches?page=1&pageSize=1`)).json;
  check('Newest history entry is this match', history?.[0]?.matchId === matchId);
  const snoop = await call(guest, 'GET', `http://localhost:5102/api/players/${host.sub}/stats`);
  check('Guest cannot read the host statistics (403)', snoop.status === 403, `status=${snoop.status}`);
  console.log(`matchId=${matchId}`);
} catch (error) {
  check('Journey run', false, error.message.split('\n')[0]);
}
await browser.close();
console.log(`\n${results.filter(Boolean).length}/${results.length} checks passed`);
process.exit(results.every(Boolean) ? 0 : 1);
