import type { KeycloakInitOptions, KeycloakLoginOptions, KeycloakLogoutOptions } from 'keycloak-js';

const matchReturnKey = 'quizarena.login.matchId';

export const userScopes = 'openid profile profiles.read profiles.write stats.read leaderboard.read questions.read lobbies.read lobbies.write matches.read matches.write events.read preferences.read preferences.write';
export type SessionStatus = 'loading' | 'anonymous' | 'authenticated' | 'expired' | 'unavailable';
export interface SessionSnapshot { status: SessionStatus; displayName?: string; subject?: string }
export interface OidcAdapter {
  authenticated?: boolean;
  token?: string;
  tokenParsed?: Record<string, unknown>;
  init(options: KeycloakInitOptions): Promise<boolean>;
  login(options: KeycloakLoginOptions): Promise<void>;
  createLogoutUrl(options: KeycloakLogoutOptions): string;
  updateToken(minValidity: number): Promise<boolean>;
  clearToken(): void;
  onAuthLogout?: () => void;
  onAuthRefreshError?: () => void;
  onTokenExpired?: () => void;
}

export class SessionRequiredError extends Error {
  constructor() { super('Sign in to continue.'); this.name = 'SessionRequiredError'; }
}

/** Owns adapter lifetime; snapshots intentionally contain no tokens or provider errors. */
export class Session {
  private snapshot: SessionSnapshot = { status: 'loading' };
  private listeners = new Set<() => void>();
  private initialized?: Promise<void>;
  private refreshing?: Promise<string>;
  private generation = 0;
  private clearing = false;
  private requests = new AbortController();

  constructor(
    private readonly adapter: OidcAdapter,
    private readonly origin: string,
    private readonly navigate: (url: string) => void = (url) => window.location.assign(url),
  ) {
    adapter.onAuthLogout = () => { if (!this.clearing) this.invalidate('expired'); };
    adapter.onAuthRefreshError = () => this.invalidate('expired');
    adapter.onTokenExpired = () => { void this.accessToken().catch(() => { /* state already cleared */ }); };
  }

  getSnapshot = (): SessionSnapshot => this.snapshot;
  subscribe = (listener: () => void): (() => void) => {
    this.listeners.add(listener);
    return () => { this.listeners.delete(listener); };
  };
  get signal(): AbortSignal { return this.requests.signal; }

  private publish(snapshot: SessionSnapshot): void {
    this.snapshot = snapshot;
    this.listeners.forEach((listener) => listener());
  }

  initialize(): Promise<void> {
    if (this.initialized) return this.initialized;
    const generation = this.generation;
    this.initialized = this.adapter.init({
      flow: 'standard', pkceMethod: 'S256', responseMode: 'query',
      redirectUri: `${this.origin}/auth/callback`, scope: userScopes,
      checkLoginIframe: false, enableLogging: false,
    }).then((authenticated) => {
      if (generation !== this.generation) { this.clearAdapter(); return; }
      this.publish(authenticated ? {
        status: 'authenticated',
        displayName: this.displayName(),
        subject: this.subject(),
      } : { status: 'anonymous' });
    }).catch(() => {
      if (generation === this.generation) this.invalidate('unavailable');
    });
    return this.initialized;
  }

  async login(): Promise<void> {
    await this.initialize();
    if (this.snapshot.status === 'unavailable') return;
    // Keep only a match ID across the full-page redirect, never a caller-controlled URL.
    try {
      const { pathname, search } = window.location;
      const matchId = pathname === '/matches' ? new URLSearchParams(search).get('matchId') : null;
      if (matchId) window.sessionStorage.setItem(matchReturnKey, matchId);
      else window.sessionStorage.removeItem(matchReturnKey);
    } catch { /* blocked storage must not prevent sign-in */ }
    try {
      await this.adapter.login({ redirectUri: `${this.origin}/auth/callback`, scope: userScopes });
    } catch { this.invalidate('unavailable'); }
  }

  /** Consume the match destination after the provider completes sign-in. */
  completeLoginRedirect(): string {
    if (this.snapshot.status !== 'authenticated') return '/';
    try {
      const matchId = window.sessionStorage.getItem(matchReturnKey);
      window.sessionStorage.removeItem(matchReturnKey);
      if (matchId) return `/matches?${new URLSearchParams({ matchId })}`;
    } catch { /* ordinary login can still continue to the overview */ }
    return '/';
  }

  logout(): void {
    let url: string | undefined;
    try { url = this.adapter.createLogoutUrl({ redirectUri: `${this.origin}/` }); }
    catch { /* an unavailable provider must not prevent local logout */ }
    finally { this.invalidate('anonymous'); }
    if (url) {
      try { this.navigate(url); } catch { /* local logout is already complete */ }
    }
  }

  /** The validated token subject; the preferences routes are keyed by it. */
  private subject(): string | undefined {
    const value = this.adapter.tokenParsed?.sub;
    return typeof value === 'string' && value.trim() ? value : undefined;
  }

  private displayName(): string {
    const claims = this.adapter.tokenParsed;
    const name = claims?.name ?? claims?.preferred_username;
    return typeof name === 'string' && name.trim() ? name : 'Player';
  }

  private clearAdapter(): void {
    this.clearing = true;
    try { this.adapter.clearToken(); } finally { this.clearing = false; }
  }

  invalidate(status: 'anonymous' | 'expired' | 'unavailable' = 'expired'): void {
    this.generation += 1;
    this.requests.abort();
    this.requests = new AbortController();
    this.clearAdapter();
    this.publish({ status });
  }

  async accessToken(): Promise<string> {
    await this.initialize();
    if (this.snapshot.status !== 'authenticated') throw new SessionRequiredError();
    if (this.refreshing) return this.refreshing;
    const generation = this.generation;
    const refresh = this.adapter.updateToken(30).then(() => {
      if (generation !== this.generation || !this.adapter.authenticated || !this.adapter.token) {
        this.clearAdapter();
        throw new SessionRequiredError();
      }
      return this.adapter.token;
    }).catch(() => {
      if (generation === this.generation) this.invalidate('expired');
      throw new SessionRequiredError();
    });
    this.refreshing = refresh;
    try { return await refresh; }
    finally { if (this.refreshing === refresh) this.refreshing = undefined; }
  }
}
