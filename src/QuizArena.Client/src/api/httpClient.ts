import { Session, SessionRequiredError } from '../auth/session';

export type ApiErrorKind = 'validation' | 'unauthorized' | 'forbidden' | 'not-found' | 'conflict' | 'unavailable' | 'unexpected' | 'network';
const messages: Record<ApiErrorKind, string> = {
  validation: 'Check the highlighted fields and try again.', unauthorized: 'Your session has ended. Sign in to continue.',
  forbidden: 'You do not have permission to do this.', 'not-found': 'We could not find that item.',
  conflict: 'This item has changed. Refresh it before trying again.', unavailable: 'This service is temporarily unavailable.',
  unexpected: 'Something went wrong. Please try again.', network: 'Unable to connect. Check your connection and try again.',
};
export class ApiError extends Error {
  constructor(public readonly kind: ApiErrorKind, public readonly status = 0, public readonly fields: Record<string, string[]> = {}) {
    super(messages[kind]); this.name = 'ApiError';
  }
}
export type RequestOptions = Omit<RequestInit, 'body' | 'headers' | 'credentials' | 'redirect'> & {
  body?: unknown; headers?: Record<string, string>;
};

function hasControlCharacters(value: string): boolean {
  return Array.from(value).some((character) => {
    const code = character.charCodeAt(0);
    return code < 0x20 || code === 0x7f;
  });
}

function validatePublicPath(pathname: string): void {
  let decoded: string;
  try { decoded = decodeURIComponent(pathname); } catch { throw new Error('Invalid request path.'); }
  if (hasControlCharacters(decoded) || decoded.includes('\\') || decoded.includes('//')
    || decoded.split('/').some((part) => ['.', '..', 'internal'].includes(part.toLowerCase()))) {
    throw new Error('Use a public service-relative path.');
  }
}

function requestUrl(base: string, path: string): URL {
  // URL parsing removes tabs and line breaks. Reject them before normalization.
  if (hasControlCharacters(path) || !path.startsWith('/') || path.startsWith('//') || path.includes('\\')) {
    throw new Error('Use a public service-relative path.');
  }
  validatePublicPath(path.split(/[?#]/)[0]);
  const root = new URL(`${base.replace(/\/$/, '')}/`);
  const url = new URL(`${base.replace(/\/$/, '')}${path}`);
  if (url.origin !== root.origin || !url.pathname.startsWith(root.pathname)) throw new Error('Request escaped its service base.');
  validatePublicPath(url.pathname);
  return url;
}

function kindFor(status: number): ApiErrorKind {
  return ({ 400: 'validation', 401: 'unauthorized', 403: 'forbidden', 404: 'not-found', 409: 'conflict', 503: 'unavailable' } as Record<number, ApiErrorKind>)[status] ?? 'unexpected';
}

async function fieldErrors(response: Response): Promise<Record<string, string[]>> {
  if (!response.headers.get('content-type')?.includes('json') || response.status !== 400) return {};
  try {
    const value: unknown = await response.json();
    if (!value || typeof value !== 'object' || !('errors' in value) || !value.errors || typeof value.errors !== 'object') return {};
    return Object.fromEntries(Object.entries(value.errors).filter((entry): entry is [string, string[]] =>
      Array.isArray(entry[1]) && entry[1].every((message) => typeof message === 'string')).map(([name, errors]) => [name, errors.slice(0, 5)]));
  } catch { return {}; }
}

export function createHttpClient(base: string, session: Session, fetcher: typeof fetch = fetch) {
  return {
    async request<T>(path: string, options: RequestOptions = {}): Promise<T> {
      const url = requestUrl(base, path);
      const lifetime = session.signal;
      const controller = new AbortController();
      const abort = () => controller.abort();
      const signals = [lifetime, options.signal].filter((signal): signal is AbortSignal => !!signal);
      signals.forEach((signal) => signal.addEventListener('abort', abort, { once: true }));
      if (signals.some((signal) => signal.aborted)) abort();
      try {
        controller.signal.throwIfAborted();
        const token = await session.accessToken();
        controller.signal.throwIfAborted();
        const headers = new Headers(options.headers);
        headers.set('Accept', 'application/json');
        headers.set('Authorization', `Bearer ${token}`);
        if (options.body !== undefined) headers.set('Content-Type', 'application/json');
        const response = await fetcher(url, {
          ...options, headers, body: options.body === undefined ? undefined : JSON.stringify(options.body),
          signal: controller.signal, credentials: 'omit', redirect: 'error',
        });
        controller.signal.throwIfAborted();
        if (!response.ok) {
          if (response.status === 401) session.invalidate('expired');
          throw new ApiError(kindFor(response.status), response.status, await fieldErrors(response));
        }
        if (response.status === 204 || response.status === 205) return undefined as T;
        const body = await response.text();
        controller.signal.throwIfAborted();
        if (!body) return undefined as T;
        if (!response.headers.get('content-type')?.includes('json')) throw new ApiError('unexpected', response.status);
        try { return JSON.parse(body) as T; } catch { throw new ApiError('unexpected', response.status); }
      } catch (error) {
        if (error instanceof ApiError || error instanceof SessionRequiredError) throw error;
        if (controller.signal.aborted) throw new DOMException('Request cancelled.', 'AbortError');
        throw new ApiError('network');
      } finally { signals.forEach((signal) => signal.removeEventListener('abort', abort)); }
    },
  };
}
