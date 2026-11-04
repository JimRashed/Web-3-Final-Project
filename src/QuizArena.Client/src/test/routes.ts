import { vi } from 'vitest';
import { Session } from '../auth/session';
import { adapterDouble } from './doubles';

export const subject = 'player-a';

/** A signed-in session whose token carries `sub`. */
export async function subjectSession(sub: string | undefined = subject, authenticated = true) {
  const adapter = adapterDouble(authenticated);
  adapter.tokenParsed = { preferred_username: 'CuriousPlayer', ...(sub ? { sub } : {}) };
  const navigate = vi.fn();
  const session = new Session(adapter, 'http://localhost:5173', navigate);
  await session.initialize();
  return { session, adapter, navigate };
}

export function problem(status: number, errors?: Record<string, string[]>): Response {
  return new Response(JSON.stringify({ status, title: 'Problem', ...(errors ? { errors } : {}) }), {
    status, headers: { 'content-type': 'application/problem+json' },
  });
}

type Reply = unknown | Response | (() => unknown | Response | Promise<Response>);

/** Replies consumed in order; the last one repeats. A plain array is a JSON body, not a sequence. */
export class Sequence { constructor(public readonly replies: Reply[]) {} }
export const inOrder = (...replies: Reply[]) => new Sequence(replies);

/** Stubs fetch and answers by the first route whose `METHOD path?query` prefix matches the request. */
export function routeFetch(routes: Record<string, Reply>) {
  const used = new Map<string, number>();
  const fetcher = vi.fn<typeof fetch>(async (input, init) => {
    const url = new URL(String(input));
    const key = `${init?.method ?? 'GET'} ${url.pathname}${url.search}`;
    const route = Object.keys(routes).find((prefix) => key.startsWith(prefix));
    if (!route) throw new Error(`Unexpected request ${key}`);
    const entry = routes[route];
    const list = entry instanceof Sequence ? entry.replies : [entry];
    const index = used.get(route) ?? 0;
    used.set(route, index + 1);
    const reply = list[Math.min(index, list.length - 1)];
    const value = typeof reply === 'function' ? await (reply as () => unknown)() : reply;
    return value instanceof Response ? value : Response.json(value);
  });
  vi.stubGlobal('fetch', fetcher);
  return fetcher;
}

export function requested(fetcher: ReturnType<typeof routeFetch>): string[] {
  return fetcher.mock.calls.map(([input, init]) => {
    const url = new URL(String(input));
    return `${init?.method ?? 'GET'} ${url.pathname}${url.search}`;
  });
}
