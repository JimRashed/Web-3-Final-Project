import type { createClients } from './clients';
import { ApiError } from './httpClient';

export interface Profile { displayName: string }

export const maxDisplayNameLength = 80;

/** Mirrors the Identity domain rule: trimmed, 1 to 80 characters. Returns an error message or undefined. */
export function displayNameError(value: string): string | undefined {
  const trimmed = value.trim();
  if (!trimmed) return 'Enter a display name.';
  if (trimmed.length > maxDisplayNameLength) return `Use at most ${maxDisplayNameLength} characters.`;
  return undefined;
}

function readProfile(value: unknown, subject: string): Profile {
  if (!value || typeof value !== 'object' || Array.isArray(value)) throw new ApiError('unexpected');
  const data = value as Record<string, unknown>;
  if (data.subject !== subject || typeof data.displayName !== 'string' || displayNameError(data.displayName)) throw new ApiError('unexpected');
  return { displayName: data.displayName };
}

/** The signed-in player's own profile; the server derives the player from the token, never from the request. */
export function createIdentityApi(clients: ReturnType<typeof createClients>) {
  const service = clients.identity;
  return {
    /** The stored profile, or null when the player has not saved one yet (404). */
    async me(subject: string, signal: AbortSignal): Promise<Profile | null> {
      try {
        return readProfile(await service.request<unknown>('/api/users/me', { signal }), subject);
      } catch (error) {
        if (error instanceof ApiError && error.kind === 'not-found') return null;
        throw error;
      }
    },
    async save(subject: string, displayName: string, signal: AbortSignal): Promise<Profile> {
      if (displayNameError(displayName)) throw new ApiError('validation', 400);
      return readProfile(await service.request<unknown>('/api/users/me', {
        method: 'PUT', body: { displayName: displayName.trim() }, signal,
      }), subject);
    },
  };
}
