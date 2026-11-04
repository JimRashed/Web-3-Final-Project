import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AuthProvider } from '../../auth/AuthProvider';
import { historyPageSize } from '../../api/statistics';
import { inOrder, problem, requested, routeFetch, subject, subjectSession } from '../../test/routes';
import { LeaderboardPage } from './LeaderboardPage';
import { StatisticsPage } from './StatisticsPage';

afterEach(() => { vi.unstubAllGlobals(); });

async function mount(page: 'statistics' | 'leaderboard', sub: string | undefined = subject, authenticated = true) {
  const result = await subjectSession(sub, authenticated);
  render(<AuthProvider session={result.session}><MemoryRouter>{page === 'statistics' ? <StatisticsPage /> : <LeaderboardPage />}</MemoryRouter></AuthProvider>);
  return result;
}
const stats = { userId: subject, gamesPlayed: 4, wins: 3, totalScore: 14 };
const match = (n: number) => ({ matchId: `match-${n}`, score: n % 6, won: n % 2 === 0, playedAt: `2026-11-0${(n % 9) + 1}T12:00:00Z` });

describe('Statistics page', () => {
  it('asks for sign-in and makes no request while anonymous', async () => {
    const fetcher = routeFetch({});
    const { adapter } = await mount('statistics', subject, false);

    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));

    await waitFor(() => expect(adapter.login).toHaveBeenCalledOnce());
    expect(fetcher).not.toHaveBeenCalled();
  });

  it('shows the totals and the newest-first history for the session subject', async () => {
    const fetcher = routeFetch({ 'GET /api/players/player-a/stats': stats, 'GET /api/players/player-a/matches': [match(2), match(1)] });
    await mount('statistics');

    expect(await screen.findByText('75%')).toBeVisible();
    expect(screen.getByText('14')).toBeVisible();
    const rows = within(screen.getByRole('table')).getAllByRole('row').slice(1);
    expect(rows.map((row) => within(row).getAllByRole('cell')[3].textContent)).toEqual(['match-2', 'match-1']);
    expect(screen.getByRole('button', { name: 'Older' })).toBeDisabled();
    expect(requested(fetcher)).toEqual(expect.arrayContaining([`GET /api/players/player-a/matches?page=1&pageSize=${historyPageSize}`]));
    expect(screen.queryByRole('button', { name: /submit|record/i })).toBeNull();
  });

  it('pages through older matches', async () => {
    const full = Array.from({ length: historyPageSize }, (_, n) => match(n + 10));
    const fetcher = routeFetch({ 'GET /api/players/player-a/stats': stats, 'GET /api/players/player-a/matches?page=1': full, 'GET /api/players/player-a/matches?page=2': [match(1)] });
    await mount('statistics');

    fireEvent.click(await screen.findByRole('button', { name: 'Older' }));

    expect(await screen.findByText('match-1')).toBeVisible();
    expect(screen.getByText('Page 2')).toBeVisible();
    expect(requested(fetcher)).toContain(`GET /api/players/player-a/matches?page=2&pageSize=${historyPageSize}`);
    fireEvent.click(screen.getByRole('button', { name: 'Newer' }));
    expect(await screen.findByText('match-10')).toBeVisible();
  });

  it('shows empty states for a new player', async () => {
    routeFetch({ 'GET /api/players/player-a/stats': { ...stats, gamesPlayed: 0, wins: 0, totalScore: 0 }, 'GET /api/players/player-a/matches': [] });
    await mount('statistics');

    expect(await screen.findByRole('heading', { name: 'A fresh start' })).toBeVisible();
    expect(await screen.findByRole('heading', { name: 'No matches yet' })).toBeVisible();
  });

  it('offers a retry after an unavailable service and recovers', async () => {
    routeFetch({ 'GET /api/players/player-a/stats': inOrder(problem(503), stats), 'GET /api/players/player-a/matches': [] });
    await mount('statistics');

    expect(await screen.findByText('This service is temporarily unavailable.')).toBeVisible();
    fireEvent.click(screen.getByRole('button', { name: 'Try again' }));

    expect(await screen.findByText('75%')).toBeVisible();
  });

  it('shows a permission message without retry on 403', async () => {
    routeFetch({ 'GET /api/players/player-a/stats': problem(403), 'GET /api/players/player-a/matches': problem(403) });
    await mount('statistics');

    expect((await screen.findAllByRole('heading', { name: 'Access unavailable' })).length).toBe(2);
    expect(screen.queryByRole('button', { name: 'Try again' })).toBeNull();
  });

  it('returns to sign-in when the API rejects the session', async () => {
    routeFetch({ 'GET /api/players/player-a/stats': problem(401), 'GET /api/players/player-a/matches': [] });
    await mount('statistics');

    expect(await screen.findByRole('button', { name: 'Sign in' })).toBeVisible();
  });
});

describe('Leaderboard page', () => {
  it('ranks players, marks the signed-in player and changes the size', async () => {
    const fetcher = routeFetch({
      'GET /api/leaderboard?top=10': [{ userId: 'player-b', totalScore: 20, wins: 4 }, { userId: subject, totalScore: 14, wins: 1 }],
      'GET /api/leaderboard?top=25': [{ userId: 'player-c', totalScore: 30, wins: 6 }],
    });
    await mount('leaderboard');

    const items = await screen.findAllByRole('listitem');
    expect(items.map((item) => item.textContent)).toEqual(['1player-b20 pts4 wins', '2You14 pts1 win']);
    expect(items[1]).toHaveAttribute('aria-current', 'true');

    fireEvent.change(screen.getByRole('combobox', { name: 'Show top' }), { target: { value: '25' } });

    expect(await screen.findByText('player-c')).toBeVisible();
    expect(requested(fetcher)).toEqual(['GET /api/leaderboard?top=10', 'GET /api/leaderboard?top=25']);
  });

  it('shows the empty state', async () => {
    routeFetch({ 'GET /api/leaderboard': [] });
    await mount('leaderboard');

    expect(await screen.findByRole('heading', { name: 'The board is waiting' })).toBeVisible();
  });

  it('treats a malformed response as an error instead of rendering it', async () => {
    routeFetch({ 'GET /api/leaderboard': [{ userId: '', totalScore: 1, wins: 0 }] });
    await mount('leaderboard');

    expect(await screen.findByRole('heading', { name: 'Unable to load' })).toBeVisible();
    expect(screen.getByRole('button', { name: 'Try again' })).toBeVisible();
  });
});
