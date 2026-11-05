// D03 delivery-retry scenario: Statistics is down when the match finishes; the outbox must hold the results
// and deliver them after Statistics comes back. Secrets come from .env and are never printed.
import { readFileSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { execFileSync, spawn } from 'node:child_process';
import { chromium } from 'playwright-core';

const root = fileURLToPath(new URL('../..', import.meta.url));

const env = Object.fromEntries(readFileSync(`${root}/.env`, 'utf8').split(/\r?\n/)
  .filter((line) => /^[A-Z_]+=/.test(line)).map((line) => [line.slice(0, line.indexOf('=')), line.slice(line.indexOf('=') + 1)]));
const results = [];
const check = (name, ok, detail = '') => { results.push(ok); console.log(`${ok ? 'PASS' : 'FAIL'}  ${name}${detail ? ' - ' + detail : ''}`); };
const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));
const browser = await chromium.launch({ channel: 'msedge', headless: true });

async function signIn(username) {
  const page = await (await browser.newContext()).newPage();
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
  return { token, sub: JSON.parse(Buffer.from(token.split('.')[1], 'base64url')).sub };
}
async function call(who, method, url, body) {
  const r = await fetch(url, { method, headers: { Authorization: `Bearer ${who.token}`, ...(body ? { 'Content-Type': 'application/json' } : {}) }, body: body ? JSON.stringify(body) : undefined });
  const text = await r.text();
  return { status: r.status, json: text ? JSON.parse(text) : undefined };
}
function deliveries(matchId) {
  const ps = `$c = New-Object System.Data.SqlClient.SqlConnection 'Server=(localdb)\\MSSQLLocalDB;Database=QuizArenaGameplayDb;Integrated Security=true;TrustServerCertificate=true'; $c.Open(); $cmd = $c.CreateCommand();
$cmd.CommandText = "SELECT Destination, Status, AttemptCount, ISNULL(LEFT(LastError,60),'') FROM Deliveries WHERE PayloadJson LIKE '%${matchId}%' ORDER BY Destination";
$r = $cmd.ExecuteReader(); while ($r.Read()) { '{0}|{1}|{2}|{3}' -f $r[0],$r[1],$r[2],$r[3] }`;
  return execFileSync('powershell', ['-NoProfile', '-Command', ps]).toString().trim().split(/\r?\n/).filter(Boolean);
}
const pids = () => JSON.parse(readFileSync(`${root}/.run/pids.json`, 'utf8').replace(/^\uFEFF/, ''));
// The process actually serving Statistics (port 5102), so a stale pid file can't point at the wrong process.
const statisticsPid = () => execFileSync('powershell', ['-NoProfile', '-Command',
  '(Get-NetTCPConnection -LocalPort 5102 -State Listen | Select-Object -First 1).OwningProcess']).toString().trim();

try {
  const host = await signIn('player-a');
  const guest = await signIn('player-b');
  const before = (await call(guest, 'GET', `http://localhost:5102/api/players/${guest.sub}/stats`)).json.gamesPlayed;

  const code = (await call(host, 'POST', 'http://localhost:5104/api/lobbies', {})).json.joinCode;
  await call(guest, 'POST', `http://localhost:5104/api/lobbies/${code}/join`, {});
  await call(host, 'POST', `http://localhost:5104/api/lobbies/${code}/lock`);
  const matchId = (await call(host, 'POST', 'http://localhost:5105/matches', { joinCode: code })).json.matchId;
  const id = encodeURIComponent(matchId);
  for (let round = 0; round < 4; round++) {
    const q = (await call(host, 'GET', `http://localhost:5105/matches/${id}/next-question`)).json;
    await call(host, 'POST', `http://localhost:5105/matches/${id}/answers`, { questionId: q.questionId, answer: q.options[0] });
    await call(guest, 'POST', `http://localhost:5105/matches/${id}/answers`, { questionId: q.questionId, answer: q.options[0] });
  }
  check('Four rounds played', true, `match ${matchId.slice(0, 8)}`);

  // Take Statistics down before the final round.
  execFileSync('taskkill', ['/PID', statisticsPid(), '/T', '/F']);
  await sleep(1500);
  const down = await fetch('http://localhost:5102/health').then(() => 'up', () => 'down');
  check('Statistics is down', down === 'down');

  const q = (await call(host, 'GET', `http://localhost:5105/matches/${id}/next-question`)).json;
  await call(host, 'POST', `http://localhost:5105/matches/${id}/answers`, { questionId: q.questionId, answer: q.options[0] });
  const last = await call(guest, 'POST', `http://localhost:5105/matches/${id}/answers`, { questionId: q.questionId, answer: q.options[0] });
  check('Players can still finish the match while Statistics is down', last.status === 200 && last.json.finished === true, `status=${last.status}`);

  await sleep(12000);
  const pending = deliveries(matchId);
  const stats = pending.filter((row) => row.startsWith('statistics') || row.startsWith('0|') || /statistics/i.test(row));
  console.log('  outbox while down:', pending.join('  ;  '));
  check('Results stay in the outbox as Pending with an error and at least one attempt',
    pending.some((row) => /^[^|]*stat[^|]*\|(0|Pending)\|[1-9]/i.test(row)));
  check('Notification events were still delivered', pending.filter((row) => /notif/i.test(row) && /\|(1|Delivered)\|/.test(row)).length === 4);

  // Bring Statistics back exactly as the start script runs it.
  const child = spawn('dotnet', ['run', '--no-build', '--project', 'src/QuizArena.Statistics.Api', '--no-launch-profile', '--urls', 'http://localhost:5102'],
    { cwd: root, detached: true, stdio: 'ignore', env: { ...process.env, ASPNETCORE_ENVIRONMENT: 'Development' } });
  child.unref();
  // Record the new process so scripts/stop-milestone3.ps1 still stops it.
  const all = pids(); all.Statistics = child.pid;
  writeFileSync(`${root}/.run/pids.json`, JSON.stringify(all, null, 2));
  for (let i = 0; i < 30; i++) { if (await fetch('http://localhost:5102/health').then((r) => r.ok, () => false)) break; await sleep(2000); }
  check('Statistics restarted', await fetch('http://localhost:5102/health').then((r) => r.ok, () => false));

  let after = before;
  for (let i = 0; i < 60 && after === before; i++) {
    await sleep(5000);
    after = (await call(guest, 'GET', `http://localhost:5102/api/players/${guest.sub}/stats`)).json.gamesPlayed;
  }
  console.log('  outbox after restart:', deliveries(matchId).join('  ;  '));
  check('The outbox redelivered the result after Statistics came back', after === before + 1, `guest games ${before}->${after}`);
  check('Every delivery for this match ends Delivered', deliveries(matchId).every((row) => /\|(1|Delivered)\|/.test(row)));
} catch (error) {
  check('Retry run', false, error.message.split('\n')[0]);
}
await browser.close();
console.log(`\n${results.filter(Boolean).length}/${results.length} checks passed`);
process.exit(results.every(Boolean) ? 0 : 1);
