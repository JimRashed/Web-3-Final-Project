import { describe, expect, it, vi } from 'vitest';
import { ApiError } from './httpClient';
import { createStatisticsApi, historyPageSize } from './statistics';

function apiWith(value: unknown) {
  const request = vi.fn().mockResolvedValue(value);
  const api = createStatisticsApi({ statistics: { request } } as never);
  return { api, request };
}
const signal = new AbortController().signal;
const item = { matchId: 'match-1', score: 3, won: true, playedAt: '2026-11-04T12:00:00Z' };

describe('statistics api', () => {
  it('reads the player totals from an encoded self route', async () => {
    const { api, request } = apiWith({ userId: 'player a/b', gamesPlayed: 3, wins: 1, totalScore: 9 });

    await expect(api.stats('player a/b', signal)).resolves.toEqual({ gamesPlayed: 3, wins: 1, totalScore: 9 });
    expect(request).toHaveBeenCalledWith('/api/players/player%20a%2Fb/stats', { signal });
  });

  it.each([
    [{ userId: 'someone-else', gamesPlayed: 1, wins: 0, totalScore: 0 }],
    [{ userId: 'player-a', gamesPlayed: 1, wins: 2, totalScore: 0 }],
    [{ userId: 'player-a', gamesPlayed: -1, wins: 0, totalScore: 0 }],
    [{ userId: 'player-a', gamesPlayed: 1.5, wins: 0, totalScore: 0 }],
    [[]],
  ])('rejects totals that do not match the contract: %j', async (value) => {
    await expect(apiWith(value).api.stats('player-a', signal)).rejects.toBeInstanceOf(ApiError);
  });

  it('requests one history page and maps it', async () => {
    const { api, request } = apiWith([item]);

    await expect(api.history('player-a', 2, signal)).resolves.toEqual([item]);
    expect(request).toHaveBeenCalledWith(`/api/players/player-a/matches?page=2&pageSize=${historyPageSize}`, { signal });
  });

  it('rejects malformed or oversized history pages', async () => {
    await expect(apiWith([{ ...item, playedAt: 'yesterday' }]).api.history('player-a', 1, signal)).rejects.toBeInstanceOf(ApiError);
    await expect(apiWith(Array(historyPageSize + 1).fill(item)).api.history('player-a', 1, signal)).rejects.toBeInstanceOf(ApiError);
  });

  it('rejects an invalid page before any request', async () => {
    const { api, request } = apiWith([]);

    await expect(api.history('player-a', 0, signal)).rejects.toMatchObject({ kind: 'validation' });
    expect(request).not.toHaveBeenCalled();
  });

  it('reads the leaderboard for the selected size', async () => {
    const { api, request } = apiWith([{ userId: 'player-b', totalScore: 12, wins: 3 }]);

    await expect(api.leaderboard(25, signal)).resolves.toEqual([{ userId: 'player-b', totalScore: 12, wins: 3 }]);
    expect(request).toHaveBeenCalledWith('/api/leaderboard?top=25', { signal });
  });

  it.each([0, 101, 2.5])('rejects leaderboard size %d before any request', async (top) => {
    const { api, request } = apiWith([]);

    await expect(api.leaderboard(top, signal)).rejects.toMatchObject({ kind: 'validation' });
    expect(request).not.toHaveBeenCalled();
  });
});
