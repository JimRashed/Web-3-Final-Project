import { vi } from 'vitest';
import { Session, type OidcAdapter } from '../auth/session';

export function adapterDouble(authenticated = true): OidcAdapter {
  const adapter: OidcAdapter = {
    authenticated, token: authenticated ? 'access-one' : undefined,
    tokenParsed: { preferred_username: 'CuriousPlayer' },
    init: vi.fn().mockResolvedValue(authenticated), login: vi.fn().mockResolvedValue(undefined),
    createLogoutUrl: vi.fn().mockReturnValue('https://provider.example/logout'),
    updateToken: vi.fn().mockResolvedValue(false),
    clearToken: vi.fn(() => { adapter.token = undefined; adapter.authenticated = false; }),
  };
  return adapter;
}
export async function sessionDouble(authenticated = true) {
  const adapter = adapterDouble(authenticated);
  const navigate = vi.fn();
  const session = new Session(adapter, 'http://localhost:5173', navigate);
  await session.initialize();
  return { session, adapter, navigate };
}
export function deferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (reason?: unknown) => void;
  const promise = new Promise<T>((success, failure) => { resolve = success; reject = failure; });
  return { promise, resolve, reject };
}
