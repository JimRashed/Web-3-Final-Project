import { describe, expect, it, vi } from 'vitest';
import { readConfig } from '../config';
import { deferred, sessionDouble } from '../test/doubles';
import { createClients } from './clients';
import { createQuestionBankApi } from './questionbank';

const question = {
  id: '01234567-89ab-cdef-0123-456789abcdef',
  prompt: 'Which planet?',
  category: 'Astronomy',
  options: ['Mars', 'Venus', 'Earth', 'Jupiter'],
};

async function setup(response: unknown = [question], status = 200) {
  const { session } = await sessionDouble();
  const fetcher = vi.fn<typeof fetch>().mockResolvedValue(Response.json(response, { status }));
  const clients = createClients(readConfig({ VITE_QUESTIONBANK_API_URL: 'https://questions.example/v1' }), session, fetcher);
  return { api: createQuestionBankApi(clients), fetcher, session };
}

describe('public QuestionBank API', () => {
  it.each([1, 50])('uses only the configured public origin, count %s, bearer and abort signal', async (count) => {
    const { api, fetcher } = await setup();
    const controller = new AbortController();
    expect(await api.generate(count, controller.signal)).toEqual([question]);
    const [url, options] = fetcher.mock.calls[0];
    expect(String(url)).toBe('https://questions.example/v1/api/questions/generate?count=' + count);
    expect(options?.method ?? 'GET').toBe('GET');
    expect(options?.body).toBeUndefined();
    expect(new Headers(options?.headers).get('Authorization')).toBe('Bearer access-one');
    expect(options?.credentials).toBe('omit');
    expect(options?.redirect).toBe('error');
    expect(options?.signal).toBeInstanceOf(AbortSignal);
    expect(fetcher).toHaveBeenCalledOnce();
  });
  it('propagates cancellation while a request is pending', async () => {
    const pending = deferred<Response>();
    const { session } = await sessionDouble();
    const fetcher = vi.fn<typeof fetch>().mockReturnValue(pending.promise);
    const api = createQuestionBankApi(createClients(readConfig({}), session, fetcher));
    const controller = new AbortController();
    const result = api.generate(10, controller.signal);
    await vi.waitFor(() => expect(fetcher).toHaveBeenCalledOnce());
    controller.abort();
    expect(fetcher.mock.calls[0][1]?.signal?.aborted).toBe(true);
    pending.resolve(Response.json([question]));
    await expect(result).rejects.toMatchObject({ name: 'AbortError' });
  });
  it.each([0, 51, -1, 1.5, NaN, Infinity, -Infinity])('rejects invalid count %s before fetching', async (count) => {
    const { api, fetcher } = await setup();
    await expect(api.generate(count, new AbortController().signal)).rejects.toMatchObject({ kind: 'validation', status: 400 });
    expect(fetcher).not.toHaveBeenCalled();
  });
  it('accepts an empty catalog and fewer questions than requested', async () => {
    const { api } = await setup([]);
    expect(await api.generate(10, new AbortController().signal)).toEqual([]);
  });
  it('projects fresh public values and drops unknown answer material', async () => {
    const upstream = { ...question, options: [...question.options], correctAnswer: 'SECRET', internal: { answer: 'SECRET' } };
    const { api } = await setup([upstream]);
    const result = await api.generate(10, new AbortController().signal);
    expect(result).toEqual([question]);
    expect(Object.keys(result[0])).toEqual(['id', 'prompt', 'category', 'options']);
    expect(result[0].options).not.toBe(upstream.options);
    upstream.options[0] = 'Changed';
    expect(result[0].options[0]).toBe('Mars');
  });
  it('counts Unicode code points and accepts contract maximum lengths', async () => {
    const valid = { ...question, prompt: '😀'.repeat(1000), category: '😀'.repeat(80), options: ['😀'.repeat(300), 'B', 'C', 'D'] };
    const { api } = await setup([valid]);
    expect(await api.generate(1, new AbortController().signal)).toEqual([valid]);
  });
  it.each([
    null, {}, 42, [null], [42],
    [{ ...question, id: 42 }],
    [{ ...question, id: 'not-a-uuid' }],
    [{ ...question, prompt: ' ' }],
    [{ ...question, prompt: '😀'.repeat(1001) }],
    [{ ...question, category: ' ' }],
    [{ ...question, category: '😀'.repeat(81) }],
    [{ ...question, options: 'A,B,C,D' }],
    [{ ...question, options: ['A', 'B', 'C'] }],
    [{ ...question, options: ['A', 'B', 42, 'D'] }],
    [{ ...question, options: ['A', 'B', 'C', ' '] }],
    [{ ...question, options: ['A', 'B', 'C', '😀'.repeat(301)] }],
    [{ ...question, options: ['A', 'A', 'C', 'D'] }],
    [question, question],
    [question, { ...question, id: question.id.toUpperCase() }],
  ])('rejects malformed success without exposing it: %j', async (response) => {
    const { api } = await setup(response);
    await expect(api.generate(10, new AbortController().signal)).rejects.toMatchObject({ kind: 'unexpected' });
  });
  it('rejects more results than requested', async () => {
    const { api } = await setup([question, { ...question, id: '11111111-1111-1111-1111-111111111111' }]);
    await expect(api.generate(1, new AbortController().signal)).rejects.toMatchObject({ kind: 'unexpected' });
  });
  it('does not fetch after cancellation and never retries a failure', async () => {
    const { api, fetcher } = await setup();
    const canceled = new AbortController();
    canceled.abort();
    await expect(api.generate(1, canceled.signal)).rejects.toMatchObject({ name: 'AbortError' });
    expect(fetcher).not.toHaveBeenCalled();
    fetcher.mockResolvedValueOnce(Response.json({ detail: 'SECRET' }, { status: 503 }));
    await expect(api.generate(1, new AbortController().signal)).rejects.toMatchObject({ kind: 'unavailable', status: 503 });
    expect(fetcher).toHaveBeenCalledOnce();
  });
});
