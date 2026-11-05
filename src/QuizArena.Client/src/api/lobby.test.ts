import { describe, expect, it, vi } from 'vitest';
import { readConfig } from '../config';
import { sessionDouble } from '../test/doubles';
import { createClients } from './clients';
import { createLobbyApi } from './lobby';

const lobby = { lobbyId: '01234567-89ab-cdef-0123-456789abcdef', joinCode: 'ABC123', status: 'Open', memberCount: 1, isMember: true, isHost: true };
async function setup(response: unknown = lobby, status = 200) {
  const { session } = await sessionDouble();
  const fetcher = vi.fn<typeof fetch>().mockResolvedValue(Response.json(response, { status }));
  const clients = createClients(readConfig({ VITE_LOBBY_API_URL: 'https://lobby.example/v1', VITE_GAMEPLAY_API_URL: 'https://play.example/v2' }), session, fetcher);
  return { api: createLobbyApi(clients), fetcher, session };
}

describe('public Lobby and Gameplay requests', () => {
  it.each(['create', 'get', 'join', 'lock'] as const)('sends %s to only the configured Lobby origin with bearer and signal', async (operation) => {
    const { api, fetcher } = await setup();
    const controller = new AbortController();
    const result = operation === 'create' ? await api.create(controller.signal) : await api[operation]('ABC123', controller.signal);
    expect(result).toEqual(lobby);
    const [url, options] = fetcher.mock.calls[0];
    expect(String(url)).toBe(`https://lobby.example/v1/api/lobbies${operation === 'create' ? '' : `/ABC123${operation === 'get' ? '' : `/${operation}`}`}`);
    expect(options?.method ?? 'GET').toBe(operation === 'get' ? 'GET' : 'POST');
    expect(options?.body).toBe(operation === 'create' || operation === 'join' ? '{}' : undefined);
    expect(new Headers(options?.headers).get('Authorization')).toBe('Bearer access-one');
    expect(options?.signal).toBeInstanceOf(AbortSignal);
    controller.abort();
    expect(options?.credentials).toBe('omit');
    expect(options?.redirect).toBe('error');
    expect(fetcher).toHaveBeenCalledOnce();
  });
  it.each([200, 201])('starts through Gameplay with only joinCode and accepts returned match ID on %s', async (status) => {
    const { api, fetcher } = await setup({ matchId: 'returned/id with space', ignored: true }, status);
    expect(await api.startMatch('ABC123', new AbortController().signal)).toEqual({ matchId: 'returned/id with space' });
    expect(String(fetcher.mock.calls[0][0])).toBe('https://play.example/v2/matches');
    expect(fetcher.mock.calls[0][1]?.body).toBe('{"joinCode":"ABC123"}');
  });
  it.each([200, 201].flatMap((status) => ['part\nid', 'part\t\u0000id', '😀'.repeat(51), '😀'.repeat(100), 'x'.repeat(100)].map((matchId) => ({ status, matchId }))))('accepts exact contract-valid Unicode/control MatchId $matchId on $status', async ({ status, matchId }) => {
    const { api } = await setup({ matchId }, status);
    expect(await api.startMatch('ABC123', new AbortController().signal)).toEqual({ matchId });
  });
  it.each(['abc123', ' ABC12', 'ABC12 ', 'ABC12', 'ABC1234', 'ABC-12'])('rejects invalid code %j before fetching', async (code) => {
    const { api, fetcher } = await setup();
    await expect(api.join(code, new AbortController().signal)).rejects.toMatchObject({ kind: 'validation' });
    expect(fetcher).not.toHaveBeenCalled();
  });
  it.each([null, {}, { ...lobby, memberCount: 3 }, { ...lobby, status: 'Locked' }, { ...lobby, isMember: false }])('rejects malformed Lobby success safely: %j', async (value) => {
    const { api } = await setup(value);
    await expect(api.create(new AbortController().signal)).rejects.toMatchObject({ kind: 'unexpected' });
  });
  it.each([{}, { matchId: '' }, { matchId: ' ' }, { matchId: '\t\r\n\u00a0' }, { matchId: 'x'.repeat(101) }, { matchId: '😀'.repeat(101) }, { matchId: 12 }, { matchId: false }, { matchId: null }, { matchId: [] }, { matchId: {} }])('rejects malformed match success: %j', async (value) => {
    const { api } = await setup(value);
    await expect(api.startMatch('ABC123', new AbortController().signal)).rejects.toMatchObject({ kind: 'unexpected' });
  });
  it('copies only six public fields even when an upstream object has extra data', async () => {
    const { api } = await setup({ ...lobby, hostUserId: 'must-not-store' });
    expect(Object.keys(await api.create(new AbortController().signal))).toEqual(Object.keys(lobby));
  });
  it('does not fetch after an already canceled signal', async () => {
    const { api, fetcher } = await setup();
    const controller = new AbortController(); controller.abort();
    await expect(api.get('ABC123', controller.signal)).rejects.toMatchObject({ name: 'AbortError' });
    expect(fetcher).not.toHaveBeenCalled();
  });
  it('passes safe failures through without retry', async () => {
    const { api, fetcher } = await setup({ detail: 'SECRET' }, 503);
    await expect(api.lock('ABC123', new AbortController().signal)).rejects.toMatchObject({ kind: 'unavailable', status: 503 });
    expect(fetcher).toHaveBeenCalledOnce();
  });
});
