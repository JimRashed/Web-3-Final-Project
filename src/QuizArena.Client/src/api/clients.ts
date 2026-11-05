import type { ClientConfig, ServiceName } from '../config';
import type { Session } from '../auth/session';
import { createHttpClient } from './httpClient';

export function createClients(config: ClientConfig, session: Session, fetcher: typeof fetch = fetch) {
  return Object.fromEntries(Object.entries(config.api).map(([name, base]) => [name, createHttpClient(base, session, fetcher)])) as Record<ServiceName, ReturnType<typeof createHttpClient>>;
}
