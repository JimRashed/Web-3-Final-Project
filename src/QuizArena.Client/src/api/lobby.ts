import type { createClients } from './clients';
import { ApiError } from './httpClient';

export interface LobbyResponse {
  lobbyId: string; joinCode: string; status: 'Open' | 'Locked';
  memberCount: number; isMember: boolean; isHost: boolean;
}

export function isJoinCode(code: string): boolean { return /^[A-Z0-9]{6}$/.test(code); }
function validCode(code: string): string {
  if (!isJoinCode(code)) throw new ApiError('validation', 400);
  return code;
}
function readLobby(value: unknown): LobbyResponse {
  if (!value || typeof value !== 'object') throw new ApiError('unexpected');
  const data = value as Record<string, unknown>;
  if (typeof data.lobbyId !== 'string' || !/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(data.lobbyId)
    || typeof data.joinCode !== 'string' || !isJoinCode(data.joinCode)
    || (data.status !== 'Open' && data.status !== 'Locked') || (data.memberCount !== 1 && data.memberCount !== 2)
    || (data.status === 'Locked' && data.memberCount !== 2)
    || typeof data.isMember !== 'boolean' || typeof data.isHost !== 'boolean' || (data.isHost && !data.isMember)) {
    throw new ApiError('unexpected');
  }
  return { lobbyId: data.lobbyId, joinCode: data.joinCode, status: data.status, memberCount: data.memberCount, isMember: data.isMember, isHost: data.isHost };
}

export function createLobbyApi(clients: ReturnType<typeof createClients>) {
  return {
    async create(signal: AbortSignal) { return readLobby(await clients.lobby.request<unknown>('/api/lobbies', { method: 'POST', body: {}, signal })); },
    async get(code: string, signal: AbortSignal) { return readLobby(await clients.lobby.request<unknown>(`/api/lobbies/${validCode(code)}`, { signal })); },
    async join(code: string, signal: AbortSignal) { return readLobby(await clients.lobby.request<unknown>(`/api/lobbies/${validCode(code)}/join`, { method: 'POST', body: {}, signal })); },
    async lock(code: string, signal: AbortSignal) { return readLobby(await clients.lobby.request<unknown>(`/api/lobbies/${validCode(code)}/lock`, { method: 'POST', signal })); },
    async startMatch(code: string, signal: AbortSignal): Promise<{ matchId: string }> {
      const value = await clients.gameplay.request<unknown>('/matches', { method: 'POST', body: { joinCode: validCode(code) }, signal });
      if (!value || typeof value !== 'object' || !('matchId' in value) || typeof value.matchId !== 'string'
        || !value.matchId.trim() || Array.from(value.matchId).length > 100) throw new ApiError('unexpected');
      return { matchId: value.matchId };
    },
  };
}
