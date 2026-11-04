import { describe, expect, it, vi } from 'vitest';
import { ApiError } from './httpClient';
import { createNotificationApi } from './notification';

function apiWith(value: unknown) {
  const request = vi.fn().mockResolvedValue(value);
  const api = createNotificationApi({ notification: { request } } as never);
  return { api, request };
}

const eventId = '26c94137-d660-4397-a731-050c2bb07004';

describe('notification api', () => {
  it('maps a valid event list', async () => {
    const { api } = apiWith([
      { eventId, type: 'MATCH_CREATED', userId: 'player-a', payload: { matchId: 'match-1' }, createdAt: '2026-11-04T12:00:00Z' },
    ]);

    const events = await api.list(new AbortController().signal);

    expect(events).toEqual([{ eventId, type: 'MATCH_CREATED', matchId: 'match-1', createdAt: '2026-11-04T12:00:00Z' }]);
  });

  it('rejects a malformed event instead of trusting the payload', async () => {
    const { api } = apiWith([
      { eventId: 'not-a-uuid', type: 'MATCH_CREATED', payload: { matchId: 'm1' }, createdAt: '2026-11-04T12:00:00Z' },
    ]);

    await expect(api.list(new AbortController().signal)).rejects.toBeInstanceOf(ApiError);
  });

  it('rejects a non-array response', async () => {
    const { api } = apiWith({ events: [] });

    await expect(api.list(new AbortController().signal)).rejects.toBeInstanceOf(ApiError);
  });

  it('reads preferences and rejects missing flags', async () => {
    const good = apiWith({ emailEnabled: true, pushEnabled: false });
    await expect(good.api.getPreferences('player-a', new AbortController().signal)).resolves.toEqual({ emailEnabled: true, pushEnabled: false });

    const bad = apiWith({ emailEnabled: true });
    await expect(bad.api.getPreferences('player-a', new AbortController().signal)).rejects.toBeInstanceOf(ApiError);
  });

  it('saves preferences with PUT and returns the stored values', async () => {
    const { api, request } = apiWith({ emailEnabled: false, pushEnabled: true });

    const stored = await api.savePreferences('player-a', { emailEnabled: false, pushEnabled: true }, new AbortController().signal);

    expect(request).toHaveBeenCalledWith('/users/player-a/preferences', expect.objectContaining({ method: 'PUT' }));
    expect(stored).toEqual({ emailEnabled: false, pushEnabled: true });
  });
});
