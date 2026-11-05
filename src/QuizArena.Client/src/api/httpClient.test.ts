import { describe, expect, it, vi } from 'vitest';
import { ApiError, createHttpClient } from './httpClient';
import { deferred, sessionDouble } from '../test/doubles';

describe('HTTP client', () => {
  it('attaches the current access token, omits cookies and refuses redirects', async () => {
    const { session, adapter } = await sessionDouble();
    adapter.token = 'current-access';
    const fetcher = vi.fn<typeof fetch>().mockResolvedValue(Response.json({ displayName: 'Player' }));
    const result = await createHttpClient('http://localhost:5101', session, fetcher).request('/api/users/me');
    expect(result).toEqual({ displayName: 'Player' });
    const [url, options] = fetcher.mock.calls[0];
    expect(String(url)).toBe('http://localhost:5101/api/users/me');
    expect(new Headers(options?.headers).get('Authorization')).toBe('Bearer current-access');
    expect(options).toMatchObject({ credentials: 'omit', redirect: 'error' });
  });
  it.each(['https://other.example/', '//other.example/', '/\\other.example/', '/internal/question-sets', '/api/../internal/check', '/%69nternal/check', '/%2fother.example'])('rejects unsafe path %s before token acquisition', async (path) => {
    const { session, adapter } = await sessionDouble();
    const fetcher = vi.fn<typeof fetch>();
    await expect(createHttpClient('http://localhost:5101', session, fetcher).request(path)).rejects.toThrow();
    expect(fetcher).not.toHaveBeenCalled();
    expect(adapter.updateToken).not.toHaveBeenCalled();
  });
  it.each(['\t', '\n', '\r', '\0', '\x7f'])('rejects control character %j before URL normalization can expose an internal route', async (control) => {
    const { session, adapter } = await sessionDouble();
    const fetcher = vi.fn<typeof fetch>().mockResolvedValue(Response.json({ ok: true }));

    await expect(createHttpClient('http://localhost:5101', session, fetcher)
      .request(`/in${control}ternal/question-sets`)).rejects.toThrow();

    expect(fetcher).not.toHaveBeenCalled();
    expect(adapter.updateToken).not.toHaveBeenCalled();
  });
  it.each(['%09', '%0a', '%0d'])('rejects encoded control %s before token acquisition', async (control) => {
    const { session, adapter } = await sessionDouble();
    const fetcher = vi.fn<typeof fetch>();

    await expect(createHttpClient('http://localhost:5101', session, fetcher)
      .request(`/in${control}ternal/question-sets`)).rejects.toThrow();

    expect(fetcher).not.toHaveBeenCalled();
    expect(adapter.updateToken).not.toHaveBeenCalled();
  });
  it('validates the final canonical decoded path including its configured prefix', async () => {
    const { session, adapter } = await sessionDouble();
    const fetcher = vi.fn<typeof fetch>();

    await expect(createHttpClient('http://localhost:5101/%69nternal', session, fetcher)
      .request('/question-sets')).rejects.toThrow();

    expect(fetcher).not.toHaveBeenCalled();
    expect(adapter.updateToken).not.toHaveBeenCalled();
  });
  it('serializes public request DTOs and supports empty success', async () => {
    const { session } = await sessionDouble();
    const fetcher = vi.fn<typeof fetch>().mockResolvedValue(new Response(null, { status: 204 }));
    await expect(createHttpClient('https://service.example/v1', session, fetcher).request('/profile', { method: 'PUT', body: { displayName: 'Player' } })).resolves.toBeUndefined();
    expect(fetcher.mock.calls[0][1]?.body).toBe('{"displayName":"Player"}');
    expect(new Headers(fetcher.mock.calls[0][1]?.headers).get('Content-Type')).toBe('application/json');
  });
  it.each([[400, 'validation'], [403, 'forbidden'], [404, 'not-found'], [409, 'conflict'], [500, 'unexpected'], [503, 'unavailable']] as const)('maps %s without automatic retry', async (status, kind) => {
    const { session } = await sessionDouble();
    const fetcher = vi.fn<typeof fetch>().mockResolvedValue(new Response('<html>unsafe server details</html>', { status }));
    await expect(createHttpClient('http://localhost:5101', session, fetcher).request('/api/users/me')).rejects.toMatchObject({ status, kind });
    expect(fetcher).toHaveBeenCalledOnce();
    expect(session.getSnapshot().status).toBe('authenticated');
  });
  it('ends a 401 session without retrying or triggering login', async () => {
    const { session, adapter } = await sessionDouble();
    const fetcher = vi.fn<typeof fetch>().mockResolvedValue(new Response(null, { status: 401 }));
    await expect(createHttpClient('http://localhost:5101', session, fetcher).request('/api/users/me')).rejects.toMatchObject({ status: 401, kind: 'unauthorized' });
    expect(session.getSnapshot().status).toBe('expired');
    expect(adapter.login).not.toHaveBeenCalled();
    expect(fetcher).toHaveBeenCalledOnce();
  });
  it('retains typed validation fields while hiding raw problem details', async () => {
    const { session } = await sessionDouble();
    const fetcher = vi.fn<typeof fetch>().mockResolvedValue(Response.json({ detail: 'private details', errors: { displayName: ['Name is required.'], invalid: 'not an array' } }, { status: 400 }));
    const error = await createHttpClient('http://localhost:5101', session, fetcher).request('/api/users/me').catch((failure: unknown) => failure);
    expect(error).toBeInstanceOf(ApiError);
    expect(error).toMatchObject({ fields: { displayName: ['Name is required.'] } });
    expect(String(error)).not.toContain('private details');
  });
  it('does not dispatch an already cancelled request', async () => {
    const { session, adapter } = await sessionDouble();
    const fetcher = vi.fn<typeof fetch>(); const controller = new AbortController(); controller.abort();
    await expect(createHttpClient('http://localhost:5101', session, fetcher).request('/api/users/me', { signal: controller.signal })).rejects.toMatchObject({ name: 'AbortError' });
    expect(fetcher).not.toHaveBeenCalled(); expect(adapter.updateToken).not.toHaveBeenCalled();
  });
  it('propagates caller cancellation to fetch and removes listeners', async () => {
    const { session } = await sessionDouble(); const controller = new AbortController();
    const removed = vi.spyOn(controller.signal, 'removeEventListener');
    const started = deferred<void>();
    const fetcher = vi.fn<typeof fetch>().mockImplementation((_url, options) => new Promise((_resolve, reject) => {
      options?.signal?.addEventListener('abort', () => reject(new DOMException('aborted', 'AbortError'))); started.resolve();
    }));
    const request = createHttpClient('http://localhost:5101', session, fetcher).request('/api/users/me', { signal: controller.signal });
    await started.promise; controller.abort();
    await expect(request).rejects.toMatchObject({ name: 'AbortError' });
    expect(removed).toHaveBeenCalledWith('abort', expect.any(Function));
  });
  it('aborts in-flight requests when the session ends', async () => {
    const { session } = await sessionDouble(); const started = deferred<void>();
    const fetcher = vi.fn<typeof fetch>().mockImplementation((_url, options) => new Promise((_resolve, reject) => {
      options?.signal?.addEventListener('abort', () => reject(new DOMException('aborted', 'AbortError'))); started.resolve();
    }));
    const request = createHttpClient('http://localhost:5101', session, fetcher).request('/api/users/me');
    await started.promise; session.logout();
    await expect(request).rejects.toMatchObject({ name: 'AbortError' });
  });
  it('does not dispatch when cancellation occurs during token refresh', async () => {
    const { session, adapter } = await sessionDouble();
    const refresh = deferred<boolean>();
    vi.mocked(adapter.updateToken).mockReturnValue(refresh.promise);
    const controller = new AbortController();
    const fetcher = vi.fn<typeof fetch>();
    const request = createHttpClient('http://localhost:5101', session, fetcher)
      .request('/api/users/me', { signal: controller.signal });
    await Promise.resolve();

    controller.abort();
    refresh.resolve(false);

    await expect(request).rejects.toMatchObject({ name: 'AbortError' });
    expect(fetcher).not.toHaveBeenCalled();
  });
  it('rejects a late response after logout even when fetch ignores cancellation', async () => {
    const { session } = await sessionDouble();
    const response = deferred<Response>();
    const started = deferred<void>();
    const fetcher = vi.fn<typeof fetch>().mockImplementation(() => {
      started.resolve();
      return response.promise;
    });
    const request = createHttpClient('http://localhost:5101', session, fetcher).request('/api/users/me');
    await started.promise;

    session.logout();
    response.resolve(Response.json({ displayName: 'Previous player' }));

    await expect(request).rejects.toMatchObject({ name: 'AbortError' });
    expect(session.getSnapshot().status).toBe('anonymous');
  });
  it('reports a safe network error without leaking fetch details', async () => {
    const { session } = await sessionDouble();
    const fetcher = vi.fn<typeof fetch>().mockRejectedValue(new Error('private URL or token'));
    await expect(createHttpClient('http://localhost:5101', session, fetcher).request('/api/users/me')).rejects.toMatchObject({ kind: 'network' });
  });
  it('rejects malformed success data safely', async () => {
    const { session } = await sessionDouble();
    const fetcher = vi.fn<typeof fetch>().mockResolvedValue(new Response('not json', { headers: { 'Content-Type': 'application/json' } }));
    await expect(createHttpClient('http://localhost:5101', session, fetcher).request('/api/users/me')).rejects.toMatchObject({ kind: 'unexpected' });
  });
});
