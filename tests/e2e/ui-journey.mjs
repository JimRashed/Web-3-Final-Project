// D03 browser journey: two real players drive the React screens end to end. Secrets come from .env and are never printed.
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { chromium } from 'playwright-core';

const root = fileURLToPath(new URL('../..', import.meta.url));

const env = Object.fromEntries(readFileSync(`${root}/.env`, 'utf8').split(/\r?\n/)
  .filter((line) => /^[A-Z_]+=/.test(line)).map((line) => [line.slice(0, line.indexOf('=')), line.slice(line.indexOf('=') + 1)]));
const results = [];
const check = (name, ok, detail = '') => { results.push(ok); console.log(`${ok ? 'PASS' : 'FAIL'}  ${name}${detail ? ' - ' + detail : ''}`); };
const browser = await chromium.launch({ channel: 'msedge', headless: true });
const nav = (page, label) => page.getByRole('navigation', { name: 'Main navigation' }).getByRole('link', { name: label }).click();

async function player(username) {
  const context = await browser.newContext();
  const page = await context.newPage();
  const failures = [];
  page.on('response', (r) => { if (r.status() >= 500) failures.push(`${r.status()} ${r.request().method()} ${r.url()}`); });
  await page.goto('http://localhost:5173/');
  await page.getByRole('button', { name: /Sign in/ }).first().click();
  await page.waitForURL(/localhost:8080\/realms\/quizarena/);
  await page.fill('#username', username);
  await page.fill('#password', env.QUIZARENA_DEMO_PASSWORD);
  await page.click('#kc-login');
  await page.waitForURL(/localhost:5173/);
  await page.locator('.player-name').waitFor();
  return { page, failures, name: username };
}

async function finishedTitle(page) {
  const heading = page.getByRole('heading', { name: /^(You won|You lost|It’s a draw)$/ });
  return (await heading.count()) ? heading.first().innerText() : null;
}

// Plays rounds until the end screen: answer when buttons show, otherwise wait for the poll to advance.
async function play(p, optionIndex) {
  const deadline = Date.now() + 120000;
  let answered = 0;
  while (Date.now() < deadline) {
    const done = await finishedTitle(p.page);
    if (done) return { answered, done };
    const buttons = p.page.locator('.answer-options button');
    if (await buttons.count() === 4 && await buttons.nth(optionIndex).isEnabled()) {
      await buttons.nth(optionIndex).click();
      answered++;
      await p.page.waitForTimeout(300);
    } else {
      await p.page.waitForTimeout(250);
    }
  }
  return { answered, done: null };
}

let guestRef;
try {
  const host = await player('player-a');
  const guest = await player('player-b');
  guestRef = guest;
  check('Both players signed in through the Keycloak screens', true);

  await nav(host.page, 'Lobby');
  await host.page.getByRole('button', { name: 'Create lobby' }).click();
  const code = (await host.page.locator('.lobby-code').innerText({ timeout: 15000 })).trim();
  await nav(guest.page, 'Lobby');
  await guest.page.getByRole('textbox', { name: 'Join code' }).fill(code);
  await guest.page.getByRole('button', { name: 'Join lobby' }).click();
  await guest.page.getByText('2 of 2').waitFor({ timeout: 15000 });
  await host.page.getByText('2 of 2').waitFor({ timeout: 15000 });
  check('Lobby: host creates, guest joins with the code, both see 2 of 2', true, `code=${code}`);
  check('Guest sees host-only controls disabled', await guest.page.getByRole('button', { name: 'Lock lobby' }).isDisabled());

  await host.page.getByRole('button', { name: 'Lock lobby' }).click();
  await host.page.getByRole('button', { name: 'Start match' }).waitFor();
  await host.page.locator('.lobby-room dd', { hasText: 'Locked' }).waitFor({ timeout: 10000 });
  await host.page.getByRole('button', { name: 'Start match' }).click();
  await host.page.waitForURL(/\/matches\?matchId=/, { timeout: 15000 });
  const matchId = new URL(host.page.url()).searchParams.get('matchId');
  await host.page.locator('.question-panel h2').waitFor({ timeout: 15000 });
  check('Host starts the match and lands on the match page with the first question', !!matchId);

  // The guest has no URL to type: they follow the Lobby's notifications link to the delivered event.
  await guest.page.getByRole('link', { name: 'check your notifications' }).click({ timeout: 15000 });
  let opened = false;
  for (let i = 0; i < 15 && !opened; i++) {
    // Wait for the list (or the empty state) to load, then pick this match's event.
    await guest.page.locator('.event-list, .request-state h2').first().waitFor({ timeout: 10000 });
    const link = guest.page.locator('.event-item', { hasText: matchId }).getByRole('link', { name: 'Open match' });
    if (await link.count()) { await link.first().click(); opened = true; break; }
    await guest.page.waitForTimeout(2000);
    await nav(guest.page, 'Preferences');
    await nav(guest.page, 'Events');
  }
  await guest.page.locator('.question-panel h2').waitFor({ timeout: 15000 });
  check('Guest opens the match from the delivered MATCH_CREATED event (in-app, still signed in)',
    opened && new URL(guest.page.url()).searchParams.get('matchId') === matchId);

  const hostScore = await host.page.locator('.score-line').innerText();
  const guestScore = await guest.page.locator('.score-line').innerText();
  check('Each player sees the score from their own side', hostScore.startsWith('You 0') && guestScore.startsWith('You 0'), `${hostScore} | ${guestScore}`);

  const [h, g] = await Promise.all([play(host, 0), play(guest, 1)]);
  check('Five rounds answered through the buttons by both players', h.answered === 5 && g.answered === 5, `host=${h.answered} guest=${g.answered}`);
  const pair = `${h.done}/${g.done}`;
  check('End screens agree (one wins and one loses, or both draw)', ['You won/You lost', 'You lost/You won', 'It’s a draw/It’s a draw'].includes(pair), pair);

  // Results arrive through the outbox; the Statistics page shows the match for both players.
  let both = false;
  for (let i = 0; i < 15 && !both; i++) {
    await nav(host.page, 'Statistics'); await nav(guest.page, 'Statistics');
    for (const p of [host.page, guest.page]) {
      await p.locator('section[aria-labelledby="history-heading"] :is(table, .request-state h2)').first().waitFor({ timeout: 10000 });
    }
    const hostHas = await host.page.locator('td.stats-id', { hasText: matchId }).count();
    const guestHas = await guest.page.locator('td.stats-id', { hasText: matchId }).count();
    both = hostHas > 0 && guestHas > 0;
    if (!both) { await host.page.waitForTimeout(2000); await nav(host.page, 'Leaderboard'); await nav(guest.page, 'Leaderboard'); }
  }
  check('Both players see the match in their Statistics history', both);
  await nav(guest.page, 'Events');
  await guest.page.getByRole('link', { name: 'View result' }).first().waitFor({ timeout: 20000 });
  check('Guest receives the MATCH_COMPLETED event with a View result link', true);
  check('No 5xx responses during the journey', host.failures.length === 0 && guest.failures.length === 0, [...host.failures, ...guest.failures].join(' | '));
  console.log(`matchId=${matchId}`);
} catch (error) {
  check('UI journey run', false, error.message.split('\n')[0] + ' at ' + (error.stack.split('\n').find((l) => l.includes('ui-journey')) || ''));
  if (guestRef) { console.log('guest url:', guestRef.page.url()); await guestRef.page.screenshot({ path: `${root}/.run/e2e-guest-failure.png`, fullPage: true }); }
}
await browser.close();
console.log(`\n${results.filter(Boolean).length}/${results.length} checks passed`);
process.exit(results.every(Boolean) ? 0 : 1);
