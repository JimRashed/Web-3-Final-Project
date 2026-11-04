import { describe, expect, it, vi } from 'vitest';
import { ApiError } from './httpClient';
import { createGameplayApi } from './gameplay';

function apiWith(value: unknown) {
  const request = vi.fn().mockResolvedValue(value);
  return { api: createGameplayApi({ gameplay: { request } } as never), request };
}

const matchId = 'match-1';
const questionId = '26c94137-d660-4397-a731-050c2bb07004';

describe('gameplay api', () => {
  it('reads a match', async () => {
    const { api } = apiWith({ matchId, status: 'ACTIVE', playerAScore: 1, playerBScore: 0, currentQuestionIndex: 0, winnerUserId: null, draw: false });

    await expect(api.getMatch(matchId, new AbortController().signal)).resolves.toMatchObject({ status: 'ACTIVE', playerAScore: 1 });
  });

  it('rejects a match with an impossible score', async () => {
    const { api } = apiWith({ matchId, status: 'ACTIVE', playerAScore: 9, playerBScore: 0, currentQuestionIndex: 0, winnerUserId: null, draw: false });

    await expect(api.getMatch(matchId, new AbortController().signal)).rejects.toBeInstanceOf(ApiError);
  });

  it('rejects a question without exactly four options', async () => {
    const { api } = apiWith({ questionId, text: 'What is 2 + 2?', options: ['4', '3'] });

    await expect(api.currentQuestion(matchId, new AbortController().signal)).rejects.toBeInstanceOf(ApiError);
  });

  it('submits an answer and returns the outcome', async () => {
    const { api, request } = apiWith({ correct: true, playerAScore: 1, playerBScore: 0, roundComplete: false, finished: false });

    const outcome = await api.answer(matchId, questionId, '4', new AbortController().signal);

    expect(outcome.correct).toBe(true);
    expect(request).toHaveBeenCalledWith(`/matches/${matchId}/answers`, expect.objectContaining({ method: 'POST' }));
  });
});
