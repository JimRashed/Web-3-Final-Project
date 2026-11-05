import { describe, expect, it, vi } from 'vitest';
import { Session, SessionRequiredError, userScopes } from './session';
import { adapterDouble, deferred, sessionDouble } from '../test/doubles';

describe('OIDC session', () => {
  it('initializes once with code flow, S256 and only the user scopes', async () => {
    const adapter = adapterDouble();
    const session = new Session(adapter, 'http://localhost:5173');
    await Promise.all([session.initialize(), session.initialize()]);
    expect(adapter.init).toHaveBeenCalledTimes(1);
    expect(adapter.init).toHaveBeenCalledWith(expect.objectContaining({ flow: 'standard', pkceMethod: 'S256', responseMode: 'query', checkLoginIframe: false, enableLogging: false, scope: userScopes }));
    expect(userScopes).not.toMatch(/results.write|questions.play|lobbies.verify|events.write/);
    expect(session.getSnapshot()).toEqual({ status: 'authenticated', displayName: 'CuriousPlayer', subject: 'player-a' });
  });
  it('redirects to the registered callback only after explicit sign-in', async () => {
    const { session, adapter } = await sessionDouble(false);
    expect(adapter.login).not.toHaveBeenCalled();
    await session.login();
    expect(adapter.login).toHaveBeenCalledOnce();
    expect(adapter.login).toHaveBeenCalledWith({ redirectUri: 'http://localhost:5173/auth/callback', scope: userScopes });
  });
  it('restores a shared match once after a new page completes the registered callback', async () => {
    const matchId = '//elsewhere.example/path?extra=1#fragment';
    const previousUrl = window.location.href;
    try {
      window.history.replaceState(null, '', `/matches?${new URLSearchParams({ matchId })}`);
      const { session, adapter } = await sessionDouble(false);
      await session.login();
      expect(adapter.login).toHaveBeenCalledWith({ redirectUri: 'http://localhost:5173/auth/callback', scope: userScopes });
      expect(session.completeLoginRedirect()).toBe('/');

      window.history.replaceState(null, '', '/auth/callback');
      const { session: callback } = await sessionDouble();
      const destination = new URL(callback.completeLoginRedirect(), window.location.origin);
      expect(destination.origin).toBe(window.location.origin);
      expect(destination.pathname).toBe('/matches');
      expect(destination.searchParams.get('matchId')).toBe(matchId);
      expect(callback.completeLoginRedirect()).toBe('/');
    } finally {
      window.history.replaceState(null, '', previousUrl);
      window.sessionStorage.removeItem('quizarena.login.matchId');
    }
  });
  it('clears an abandoned match destination when sign-in starts from the overview', async () => {
    const previousUrl = window.location.href;
    try {
      const { session } = await sessionDouble(false);
      window.history.replaceState(null, '', '/matches?matchId=abandoned');
      await session.login();
      window.history.replaceState(null, '', '/');
      await session.login();

      const { session: callback } = await sessionDouble();
      expect(callback.completeLoginRedirect()).toBe('/');
    } finally {
      window.history.replaceState(null, '', previousUrl);
      window.sessionStorage.removeItem('quizarena.login.matchId');
    }
  });
  it('renders a safe unavailable state on callback or provider initialization failure', async () => {
    const adapter = adapterDouble();
    vi.mocked(adapter.init).mockRejectedValue(new Error('unsafe provider detail'));
    const session = new Session(adapter, 'http://localhost:5173');
    await session.initialize();
    await session.login();
    expect(session.getSnapshot()).toEqual({ status: 'unavailable' });
    expect(adapter.clearToken).toHaveBeenCalled();
    expect(adapter.login).not.toHaveBeenCalled();
  });
  it('reads the refreshed access token and shares concurrent refresh', async () => {
    const { session, adapter } = await sessionDouble();
    const refresh = deferred<boolean>();
    vi.mocked(adapter.updateToken).mockReturnValue(refresh.promise);
    const first = session.accessToken();
    const second = session.accessToken();
    await Promise.resolve();
    adapter.token = 'access-two'; refresh.resolve(true);
    expect(await Promise.all([first, second])).toEqual(['access-two', 'access-two']);
    expect(adapter.updateToken).toHaveBeenCalledOnce();
    expect(adapter.updateToken).toHaveBeenCalledWith(30);
  });
  it('clears state and aborts requests after one failed refresh without logging in', async () => {
    const { session, adapter } = await sessionDouble();
    const signal = session.signal;
    vi.mocked(adapter.updateToken).mockRejectedValue(new Error('provider unavailable'));
    await expect(session.accessToken()).rejects.toBeInstanceOf(SessionRequiredError);
    await expect(session.accessToken()).rejects.toBeInstanceOf(SessionRequiredError);
    expect(adapter.updateToken).toHaveBeenCalledOnce();
    expect(adapter.login).not.toHaveBeenCalled();
    expect(signal.aborted).toBe(true);
    expect(session.getSnapshot()).toEqual({ status: 'expired' });
    expect(adapter.token).toBeUndefined();
  });
  it('shares one failed refresh across concurrent callers and leaves sign-in explicit', async () => {
    const { session, adapter } = await sessionDouble();
    const refresh = deferred<boolean>();
    vi.mocked(adapter.updateToken).mockReturnValue(refresh.promise);
    const signal = session.signal;
    const requests = Promise.allSettled([session.accessToken(), session.accessToken()]);
    await Promise.resolve();

    refresh.reject(new Error('provider unavailable'));

    const results = await requests;
    expect(results).toEqual([
      { status: 'rejected', reason: expect.any(SessionRequiredError) },
      { status: 'rejected', reason: expect.any(SessionRequiredError) },
    ]);
    expect(adapter.updateToken).toHaveBeenCalledOnce();
    expect(adapter.login).not.toHaveBeenCalled();
    expect(signal.aborted).toBe(true);
    expect(session.getSnapshot()).toEqual({ status: 'expired' });
  });
  it('clears local state before provider logout navigation', async () => {
    const { session, adapter, navigate } = await sessionDouble();
    const signal = session.signal;
    navigate.mockImplementation(() => {
      expect(adapter.token).toBeUndefined();
      expect(session.getSnapshot()).toEqual({ status: 'anonymous' });
    });
    session.logout();
    expect(navigate).toHaveBeenCalledWith('https://provider.example/logout');
    expect(signal.aborted).toBe(true);
  });
  it('stays locally signed out when provider navigation fails', async () => {
    const { session, adapter, navigate } = await sessionDouble();
    navigate.mockImplementation(() => { throw new Error('navigation failed'); });
    expect(() => session.logout()).not.toThrow();
    expect(adapter.token).toBeUndefined();
    expect(session.getSnapshot().status).toBe('anonymous');
  });
  it('clears state even when a provider logout URL cannot be created', async () => {
    const { session, adapter, navigate } = await sessionDouble();
    vi.mocked(adapter.createLogoutUrl).mockImplementation(() => { throw new Error('provider unavailable'); });
    expect(() => session.logout()).not.toThrow();
    expect(adapter.token).toBeUndefined();
    expect(session.getSnapshot().status).toBe('anonymous');
    expect(navigate).not.toHaveBeenCalled();
  });
  it('does not revive a session when a refresh finishes after logout', async () => {
    const { session, adapter } = await sessionDouble();
    const refresh = deferred<boolean>();
    vi.mocked(adapter.updateToken).mockReturnValue(refresh.promise);
    const token = session.accessToken();
    await Promise.resolve();
    session.logout();
    adapter.token = 'late-token'; refresh.resolve(true);
    await expect(token).rejects.toBeInstanceOf(SessionRequiredError);
    expect(adapter.token).toBeUndefined();
    expect(session.getSnapshot().status).toBe('anonymous');
  });
  it('does not revive a session when initialization finishes after logout', async () => {
    const adapter = adapterDouble();
    const init = deferred<boolean>();
    vi.mocked(adapter.init).mockReturnValue(init.promise);
    const session = new Session(adapter, 'http://localhost:5173', vi.fn());
    const initialization = session.initialize();
    session.logout(); adapter.token = 'late-token'; init.resolve(true);
    await initialization;
    expect(session.getSnapshot().status).toBe('anonymous');
    expect(adapter.token).toBeUndefined();
  });
  it('publishes token-free snapshots and never writes browser token storage', async () => {
    const local = vi.spyOn(Storage.prototype, 'setItem');
    const { session } = await sessionDouble();
    const changed = vi.fn(); const unsubscribe = session.subscribe(changed);
    session.invalidate(); unsubscribe(); session.invalidate();
    expect(changed).toHaveBeenCalledOnce();
    expect(JSON.stringify(session.getSnapshot())).not.toContain('access-one');
    expect(local).not.toHaveBeenCalled();
  });
});
