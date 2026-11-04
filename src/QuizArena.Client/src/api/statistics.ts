import type { createClients } from './clients';
import { ApiError } from './httpClient';

export interface PlayerStats { gamesPlayed: number; wins: number; totalScore: number }
export interface MatchHistoryItem { matchId: string; score: number; won: boolean; playedAt: string }
export interface Leader { userId: string; totalScore: number; wins: number }

export const historyPageSize = 10;
export const leaderboardSizes = [10, 25, 50, 100] as const;

function isCount(value: unknown): value is number { return typeof value === 'number' && Number.isInteger(value) && value >= 0; }
function isText(value: unknown): value is string { return typeof value === 'string' && value.trim().length > 0 && Array.from(value).length <= 200; }
function record(value: unknown): Record<string, unknown> {
  if (!value || typeof value !== 'object' || Array.isArray(value)) throw new ApiError('unexpected');
  return value as Record<string, unknown>;
}
function list(value: unknown, limit: number): unknown[] {
  if (!Array.isArray(value) || value.length > limit) throw new ApiError('unexpected');
  return value;
}

function readStats(value: unknown, subject: string): PlayerStats {
  const data = record(value);
  if (data.userId !== subject || !isCount(data.gamesPlayed) || !isCount(data.wins) || !isCount(data.totalScore)
    || data.wins > data.gamesPlayed) throw new ApiError('unexpected');
  return { gamesPlayed: data.gamesPlayed, wins: data.wins, totalScore: data.totalScore };
}
function readHistoryItem(value: unknown): MatchHistoryItem {
  const data = record(value);
  if (!isText(data.matchId) || !isCount(data.score) || typeof data.won !== 'boolean'
    || typeof data.playedAt !== 'string' || Number.isNaN(Date.parse(data.playedAt))) throw new ApiError('unexpected');
  return { matchId: data.matchId, score: data.score, won: data.won, playedAt: data.playedAt };
}
function readLeader(value: unknown): Leader {
  const data = record(value);
  if (!isText(data.userId) || !isCount(data.totalScore) || !isCount(data.wins)) throw new ApiError('unexpected');
  return { userId: data.userId, totalScore: data.totalScore, wins: data.wins };
}

/** Statistics reads for the signed-in player. Results are only ever written by Gameplay, never by the client. */
export function createStatisticsApi(clients: ReturnType<typeof createClients>) {
  const service = clients.statistics;
  const player = (subject: string) => `/api/players/${encodeURIComponent(subject)}`;
  return {
    async stats(subject: string, signal: AbortSignal): Promise<PlayerStats> {
      return readStats(await service.request<unknown>(`${player(subject)}/stats`, { signal }), subject);
    },
    /** One page of the player's history, newest first. */
    async history(subject: string, page: number, signal: AbortSignal): Promise<MatchHistoryItem[]> {
      if (!Number.isInteger(page) || page < 1) throw new ApiError('validation', 400);
      const value = await service.request<unknown>(`${player(subject)}/matches?page=${page}&pageSize=${historyPageSize}`, { signal });
      return list(value, historyPageSize).map(readHistoryItem);
    },
    async leaderboard(top: number, signal: AbortSignal): Promise<Leader[]> {
      if (!Number.isInteger(top) || top < 1 || top > 100) throw new ApiError('validation', 400);
      return list(await service.request<unknown>(`/api/leaderboard?top=${top}`, { signal }), top).map(readLeader);
    },
  };
}
