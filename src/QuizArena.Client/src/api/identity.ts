import type { createClients } from './clients';
import { ApiError } from './httpClient';

/** Accepted image types, from the approved Identity contract (`contracts/openapi/identity.yaml`). */
export const pictureTypes = ['image/png', 'image/jpeg', 'image/webp'] as const;
export type PictureType = (typeof pictureTypes)[number];

/** The contract's hard limit: 2 MiB of raw image bytes. */
export const maxPictureBytes = 2_097_152;

export interface PictureMetadata {
  contentType: PictureType;
  updatedAt: string;
  version: number;
}

/** The caller's own profile. `picture` is metadata only: never a storage key, path or URL. */
export interface Profile {
  displayName: string;
  picture: PictureMetadata | null;
}

export const maxDisplayNameLength = 80;

/** Mirrors the Identity domain rule: trimmed, 1 to 80 characters. Returns an error message or undefined. */
export function displayNameError(value: string): string | undefined {
  const trimmed = value.trim();
  if (!trimmed) return 'Enter a display name.';
  if (trimmed.length > maxDisplayNameLength) return `Use at most ${maxDisplayNameLength} characters.`;
  return undefined;
}

/**
 * Client-side check before an upload. The server repeats every one of these rules; this exists so a
 * wrong file costs nothing and the player sees the reason immediately.
 */
export function pictureFileError(file: File): string | undefined {
  if (!pictureTypes.includes(file.type as PictureType)) return 'Choose a PNG, JPEG or WebP image.';
  if (file.size === 0) return 'That file is empty.';
  if (file.size > maxPictureBytes) return 'Use an image of at most 2 MB.';
  return undefined;
}

function readPicture(value: unknown): PictureMetadata | null {
  if (value === undefined || value === null) return null;
  if (typeof value !== 'object' || Array.isArray(value)) throw new ApiError('unexpected');
  const data = value as Record<string, unknown>;
  const contentType = data.contentType;
  const updatedAt = data.updatedAt;
  const version = data.version;
  const valid = typeof contentType === 'string' && pictureTypes.includes(contentType as PictureType)
    && typeof updatedAt === 'string' && updatedAt.length > 0
    && typeof version === 'number' && Number.isInteger(version) && version >= 1;
  if (!valid) throw new ApiError('unexpected');
  return { contentType: contentType as PictureType, updatedAt: updatedAt as string, version: version as number };
}

function readProfile(value: unknown, subject: string): Profile {
  if (!value || typeof value !== 'object' || Array.isArray(value)) throw new ApiError('unexpected');
  const data = value as Record<string, unknown>;
  if (data.subject !== subject || typeof data.displayName !== 'string' || displayNameError(data.displayName)) throw new ApiError('unexpected');
  return { displayName: data.displayName, picture: readPicture(data.picture) };
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
    /**
     * The caller's picture bytes, or null when there is none (404). Private: the request carries the
     * bearer token and the response is rendered from memory, never from a public or signed URL.
     */
    async picture(signal: AbortSignal): Promise<Blob | null> {
      try {
        return await service.request<Blob>('/api/users/me/picture', {
          signal, responseType: 'blob', accept: pictureTypes.join(', '),
        });
      } catch (error) {
        if (error instanceof ApiError && error.kind === 'not-found') return null;
        throw error;
      }
    },
    /** Replaces the picture atomically; a failed request keeps the previous one. */
    async replacePicture(subject: string, file: File, signal: AbortSignal): Promise<Profile> {
      if (pictureFileError(file)) throw new ApiError('validation', 400);
      return readProfile(await service.request<unknown>('/api/users/me/picture', {
        method: 'PUT', body: file, headers: { 'Content-Type': file.type }, signal,
      }), subject);
    },
  };
}
