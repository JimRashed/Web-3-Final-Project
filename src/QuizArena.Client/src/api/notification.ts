import type { createClients } from './clients';
import { ApiError } from './httpClient';

export interface NotificationEvent {
  eventId: string;
  type: 'MATCH_CREATED' | 'MATCH_COMPLETED';
  matchId: string;
  createdAt: string;
}

export interface NotificationPreferences {
  emailEnabled: boolean;
  pushEnabled: boolean;
}

const uuidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

function readEvent(value: unknown): NotificationEvent {
  if (!value || typeof value !== 'object') throw new ApiError('unexpected');
  const data = value as Record<string, unknown>;
  const payload = data.payload as Record<string, unknown> | undefined;
  const valid = typeof data.eventId === 'string' && uuidPattern.test(data.eventId)
    && (data.type === 'MATCH_CREATED' || data.type === 'MATCH_COMPLETED')
    && payload !== null && typeof payload === 'object'
    && typeof payload!.matchId === 'string' && payload!.matchId.trim().length > 0
    && typeof data.createdAt === 'string' && !Number.isNaN(Date.parse(data.createdAt));
  if (!valid) throw new ApiError('unexpected');
  return {
    eventId: data.eventId as string,
    type: data.type as NotificationEvent['type'],
    matchId: payload!.matchId as string,
    createdAt: data.createdAt as string,
  };
}

function readPreferences(value: unknown): NotificationPreferences {
  if (!value || typeof value !== 'object') throw new ApiError('unexpected');
  const data = value as Record<string, unknown>;
  if (typeof data.emailEnabled !== 'boolean' || typeof data.pushEnabled !== 'boolean') throw new ApiError('unexpected');
  return { emailEnabled: data.emailEnabled, pushEnabled: data.pushEnabled };
}

export function createNotificationApi(clients: ReturnType<typeof createClients>) {
  const service = clients.notification;
  return {
    /** The signed-in player's own events, newest first. */
    async list(signal: AbortSignal): Promise<NotificationEvent[]> {
      const value = await service.request<unknown>('/events', { signal });
      if (!Array.isArray(value)) throw new ApiError('unexpected');
      return value.map(readEvent);
    },

    async getPreferences(userId: string, signal: AbortSignal): Promise<NotificationPreferences> {
      return readPreferences(await service.request<unknown>(`/users/${encodeURIComponent(userId)}/preferences`, { signal }));
    },

    async savePreferences(userId: string, preferences: NotificationPreferences, signal: AbortSignal): Promise<NotificationPreferences> {
      return readPreferences(await service.request<unknown>(
        `/users/${encodeURIComponent(userId)}/preferences`,
        { method: 'PUT', body: preferences, signal },
      ));
    },
  };
}
