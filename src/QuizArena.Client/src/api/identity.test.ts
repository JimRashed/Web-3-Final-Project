import { describe, expect, it, vi } from 'vitest';
import { ApiError } from './httpClient';
import { createIdentityApi, displayNameError } from './identity';

function apiWith(request: ReturnType<typeof vi.fn>) {
  return createIdentityApi({ identity: { request } } as never);
}
const signal = new AbortController().signal;
const profile = { id: '26c94137-d660-4397-a731-050c2bb07001', issuer: 'http://localhost:8080/realms/quizarena', subject: 'player-a', displayName: 'Ada' };

describe('identity api', () => {
  it('reads the signed-in profile', async () => {
    const request = vi.fn().mockResolvedValue(profile);

    await expect(apiWith(request).me('player-a', signal)).resolves.toEqual({ displayName: 'Ada' });
    expect(request).toHaveBeenCalledWith('/api/users/me', { signal });
  });

  it('treats 404 as a player without a saved profile', async () => {
    const request = vi.fn().mockRejectedValue(new ApiError('not-found', 404));

    await expect(apiWith(request).me('player-a', signal)).resolves.toBeNull();
  });

  it('rejects a profile for a different subject', async () => {
    const request = vi.fn().mockResolvedValue({ ...profile, subject: 'player-b' });

    await expect(apiWith(request).me('player-a', signal)).rejects.toMatchObject({ kind: 'unexpected' });
  });

  it('saves the trimmed display name with PUT', async () => {
    const request = vi.fn().mockResolvedValue({ ...profile, displayName: 'Grace' });

    await expect(apiWith(request).save('player-a', '  Grace  ', signal)).resolves.toEqual({ displayName: 'Grace' });
    expect(request).toHaveBeenCalledWith('/api/users/me', { method: 'PUT', body: { displayName: 'Grace' }, signal });
  });

  it('rejects an invalid display name before any request', async () => {
    const request = vi.fn();

    await expect(apiWith(request).save('player-a', '   ', signal)).rejects.toMatchObject({ kind: 'validation' });
    expect(request).not.toHaveBeenCalled();
  });

  it.each([['', 'Enter a display name.'], ['   ', 'Enter a display name.'], ['x'.repeat(81), 'Use at most 80 characters.'], [` ${'x'.repeat(80)} `, undefined], ['Ada', undefined]])(
    'validates display name %j', (value, expected) => {
      expect(displayNameError(value)).toBe(expected);
    });
});
