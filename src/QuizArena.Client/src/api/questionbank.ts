import type { createClients } from './clients';
import { ApiError } from './httpClient';

export interface QuestionPreview {
  id: string;
  prompt: string;
  category: string;
  options: string[];
}

const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

function validText(value: unknown, maximum: number): value is string {
  return typeof value === 'string' && value.trim().length > 0 && Array.from(value).length <= maximum;
}

function readQuestion(value: unknown): QuestionPreview {
  if (!value || typeof value !== 'object' || Array.isArray(value)) throw new ApiError('unexpected');
  const data = value as Record<string, unknown>;
  if (typeof data.id !== 'string' || !uuid.test(data.id)
    || !validText(data.prompt, 1000) || !validText(data.category, 80)
    || !Array.isArray(data.options) || data.options.length !== 4
    || !data.options.every((option: unknown) => validText(option, 300))
    || new Set(data.options).size !== 4) throw new ApiError('unexpected');
  return { id: data.id, prompt: data.prompt, category: data.category, options: [...data.options] };
}

function readQuestions(value: unknown, count: number): QuestionPreview[] {
  if (!Array.isArray(value) || value.length > count) throw new ApiError('unexpected');
  const questions = value.map(readQuestion);
  if (new Set(questions.map((question) => question.id.toLowerCase())).size !== questions.length) throw new ApiError('unexpected');
  return questions;
}

export function validQuestionCount(count: number): boolean {
  return Number.isInteger(count) && count >= 1 && count <= 50;
}

export function createQuestionBankApi(clients: ReturnType<typeof createClients>) {
  return {
    async generate(count: number, signal: AbortSignal): Promise<QuestionPreview[]> {
      if (!validQuestionCount(count)) throw new ApiError('validation', 400);
      const value = await clients.questionbank.request<unknown>('/api/questions/generate?count=' + count, { signal });
      return readQuestions(value, count);
    },
  };
}
