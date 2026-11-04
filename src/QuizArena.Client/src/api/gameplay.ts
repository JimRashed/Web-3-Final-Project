import type { createClients } from './clients';
import { ApiError } from './httpClient';

export type MatchStatus = 'ACTIVE' | 'FINISHED';

export interface MatchView {
  matchId: string;
  status: MatchStatus;
  playerAScore: number;
  playerBScore: number;
  currentQuestionIndex: number;
  winnerUserId: string | null;
  draw: boolean;
}

export interface CurrentQuestion {
  questionId: string;
  text: string;
  options: string[];
}

export interface AnswerOutcome {
  correct: boolean;
  playerAScore: number;
  playerBScore: number;
  roundComplete: boolean;
  finished: boolean;
}

const uuidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const score = (value: unknown): value is number => typeof value === 'number' && Number.isInteger(value) && value >= 0 && value <= 5;

function readMatch(value: unknown): MatchView {
  if (!value || typeof value !== 'object') throw new ApiError('unexpected');
  const data = value as Record<string, unknown>;
  const valid = typeof data.matchId === 'string' && data.matchId.trim().length > 0
    && (data.status === 'ACTIVE' || data.status === 'FINISHED')
    && score(data.playerAScore) && score(data.playerBScore)
    && typeof data.currentQuestionIndex === 'number' && Number.isInteger(data.currentQuestionIndex)
    && (data.winnerUserId === null || typeof data.winnerUserId === 'string')
    && typeof data.draw === 'boolean';
  if (!valid) throw new ApiError('unexpected');
  return {
    matchId: data.matchId as string,
    status: data.status as MatchStatus,
    playerAScore: data.playerAScore as number,
    playerBScore: data.playerBScore as number,
    currentQuestionIndex: data.currentQuestionIndex as number,
    winnerUserId: (data.winnerUserId ?? null) as string | null,
    draw: data.draw as boolean,
  };
}

function readQuestion(value: unknown): CurrentQuestion {
  if (!value || typeof value !== 'object') throw new ApiError('unexpected');
  const data = value as Record<string, unknown>;
  const options = data.options;
  const valid = typeof data.questionId === 'string' && uuidPattern.test(data.questionId)
    && typeof data.text === 'string' && data.text.trim().length > 0
    && Array.isArray(options) && options.length === 4 && options.every((option) => typeof option === 'string' && option.trim().length > 0);
  if (!valid) throw new ApiError('unexpected');
  return { questionId: data.questionId as string, text: data.text as string, options: options as string[] };
}

function readOutcome(value: unknown): AnswerOutcome {
  if (!value || typeof value !== 'object') throw new ApiError('unexpected');
  const data = value as Record<string, unknown>;
  const valid = typeof data.correct === 'boolean' && score(data.playerAScore) && score(data.playerBScore)
    && typeof data.roundComplete === 'boolean' && typeof data.finished === 'boolean';
  if (!valid) throw new ApiError('unexpected');
  return {
    correct: data.correct as boolean,
    playerAScore: data.playerAScore as number,
    playerBScore: data.playerBScore as number,
    roundComplete: data.roundComplete as boolean,
    finished: data.finished as boolean,
  };
}

export function createGameplayApi(clients: ReturnType<typeof createClients>) {
  const service = clients.gameplay;
  return {
    async getMatch(matchId: string, signal: AbortSignal): Promise<MatchView> {
      return readMatch(await service.request<unknown>(`/matches/${encodeURIComponent(matchId)}`, { signal }));
    },

    async currentQuestion(matchId: string, signal: AbortSignal): Promise<CurrentQuestion> {
      return readQuestion(await service.request<unknown>(`/matches/${encodeURIComponent(matchId)}/next-question`, { signal }));
    },

    async answer(matchId: string, questionId: string, answer: string, signal: AbortSignal): Promise<AnswerOutcome> {
      return readOutcome(await service.request<unknown>(
        `/matches/${encodeURIComponent(matchId)}/answers`,
        { method: 'POST', body: { questionId, answer }, signal },
      ));
    },
  };
}
