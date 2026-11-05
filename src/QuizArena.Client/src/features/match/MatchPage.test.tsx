import { fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AuthProvider } from '../../auth/AuthProvider';
import { inOrder, problem, requested, routeFetch, subjectSession } from '../../test/routes';
import { MatchPage, waitingPollMs } from './MatchPage';

afterEach(() => { vi.unstubAllGlobals(); });

const q1 = { questionId: '26c94137-d660-4397-a731-050c2bb07001', text: 'What is 2 + 2?', options: ['4', '3', '5', '6'] };
const q2 = { questionId: '26c94137-d660-4397-a731-050c2bb07002', text: 'Capital of France?', options: ['Paris', 'Rome', 'Madrid', 'Berlin'] };
const match = (changes: Record<string, unknown> = {}) => ({
  matchId: 'm1', lobbyId: 'l1', playerAId: 'host', playerBId: 'guest', status: 'ACTIVE',
  playerAScore: 3, playerBScore: 1, currentQuestionIndex: 0, winnerUserId: null, draw: false, completedAt: null, ...changes,
});

async function mount(sub: string, path = '/matches/m1') {
  const result = await subjectSession(sub);
  render(<AuthProvider session={result.session}><MemoryRouter initialEntries={[path]}><Routes>
    <Route path="/matches" element={<MatchPage />} /><Route path="/matches/:matchId" element={<MatchPage />} />
  </Routes></MemoryRouter></AuthProvider>);
  return result;
}

describe('Match page', () => {
  it.each([['host', 'You 3 — 1 Opponent'], ['guest', 'You 1 — 3 Opponent']])('shows the score from the %s perspective', async (sub, line) => {
    routeFetch({ 'GET /matches/m1/next-question': q1, 'GET /matches/m1': match() });
    await mount(sub);

    expect(await screen.findByText(line)).toBeVisible();
  });

  it('opens from the /matches?matchId= link that Lobby navigates to', async () => {
    const fetcher = routeFetch({ 'GET /matches/a%2Fb/next-question': q1, 'GET /matches/a%2Fb': match({ matchId: 'a/b' }) });
    await mount('host', '/matches?matchId=a%2Fb');

    expect(await screen.findByRole('heading', { name: 'What is 2 + 2?' })).toBeVisible();
    expect(screen.getByText('Match ID: a/b')).toBeVisible();
    expect(requested(fetcher)[0]).toBe('GET /matches/a%2Fb');
  });

  it('waits for the opponent after answering, then moves to the next round', async () => {
    const fetcher = routeFetch({
      'POST /matches/m1/answers': { correct: true, playerAScore: 4, playerBScore: 1, roundComplete: false, finished: false },
      'GET /matches/m1/next-question': inOrder(q1, q1, q2),
      'GET /matches/m1': inOrder(match(), match({ playerAScore: 4 }), match({ playerAScore: 4, playerBScore: 2, currentQuestionIndex: 1 })),
    });
    await mount('host');

    fireEvent.click(await screen.findByRole('button', { name: '4' }));

    expect(await screen.findByText('Correct — one point. Waiting for your opponent…')).toBeVisible();
    expect(screen.queryByRole('button', { name: '4' })).toBeNull();
    expect(await screen.findByRole('button', { name: 'Paris' }, { timeout: waitingPollMs + 1500 })).toBeEnabled();
    expect(screen.getByText('Round 2 of 5')).toBeVisible();
    expect(requested(fetcher).filter((call) => call.startsWith('POST'))).toHaveLength(1);
  });

  it('treats a 409 on answer as "already answered" and keeps the match view', async () => {
    routeFetch({
      'POST /matches/m1/answers': problem(409),
      'GET /matches/m1/next-question': q1,
      'GET /matches/m1': match(),
    });
    await mount('guest');

    fireEvent.click(await screen.findByRole('button', { name: '3' }));

    expect(await screen.findByText('Waiting for your opponent…')).toBeVisible();
    expect(screen.getByText('You 1 — 3 Opponent')).toBeVisible();
    expect(screen.queryByRole('alert')).toBeNull();
  });

  it('shows a failed answer inline and keeps the question', async () => {
    routeFetch({ 'POST /matches/m1/answers': problem(503), 'GET /matches/m1/next-question': q1, 'GET /matches/m1': match() });
    await mount('host');

    fireEvent.click(await screen.findByRole('button', { name: '4' }));

    expect(await screen.findByRole('heading', { name: 'Your answer was not sent' })).toBeVisible();
    expect(screen.getByRole('button', { name: '4' })).toBeEnabled();
    fireEvent.click(screen.getByRole('button', { name: 'Try again' }));
    expect(screen.queryByRole('heading', { name: 'Your answer was not sent' })).toBeNull();
  });

  it.each([
    ['host', { winnerUserId: 'host' }, 'You won'],
    ['guest', { winnerUserId: 'host' }, 'You lost'],
    ['guest', { draw: true, playerBScore: 3 }, 'It’s a draw'],
  ])('shows the result for %s when finished', async (sub, changes, title) => {
    routeFetch({ 'GET /matches/m1': match({ status: 'FINISHED', currentQuestionIndex: 5, ...changes }) });
    await mount(sub);

    expect(await screen.findByRole('heading', { name: title })).toBeVisible();
  });

  it('offers a retry when the match cannot be loaded', async () => {
    routeFetch({ 'GET /matches/m1/next-question': q1, 'GET /matches/m1': inOrder(problem(503), match()) });
    await mount('host');

    fireEvent.click(await screen.findByRole('button', { name: 'Try again' }));

    expect(await screen.findByRole('heading', { name: 'What is 2 + 2?' })).toBeVisible();
  });

  it('shows a permission message without retry for a non-participant', async () => {
    routeFetch({ 'GET /matches/m1': problem(403) });
    await mount('stranger');

    expect(await screen.findByRole('heading', { name: 'Access unavailable' })).toBeVisible();
    expect(screen.queryByRole('button', { name: 'Try again' })).toBeNull();
  });
});
