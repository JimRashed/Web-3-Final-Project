export const serviceNames = ['identity', 'statistics', 'questionbank', 'lobby', 'gameplay', 'notification'] as const;
export type ServiceName = typeof serviceNames[number];
export interface ClientConfig {
  oidc: { url: string; realm: string; clientId: string };
  api: Record<ServiceName, string>;
}

function publicUrl(value: string, name: string): string {
  let url: URL;
  try { url = new URL(value); } catch { throw new Error(`${name} must be an absolute HTTP(S) URL.`); }
  if (!['http:', 'https:'].includes(url.protocol) || url.username || url.password || url.search || url.hash) {
    throw new Error(`${name} must be an HTTP(S) URL without credentials, query or fragment.`);
  }
  return url.href.replace(/\/$/, '');
}

export function readConfig(env: Record<string, string | undefined>): ClientConfig {
  const api = Object.fromEntries(serviceNames.map((name, index) => [name,
    publicUrl(env[`VITE_${name.toUpperCase()}_API_URL`] ?? `http://localhost:${5101 + index}`, name),
  ])) as Record<ServiceName, string>;
  const realm = env.VITE_OIDC_REALM ?? 'quizarena';
  const clientId = env.VITE_OIDC_CLIENT_ID ?? 'quizarena-client';
  if (!/^[\w.-]+$/.test(realm) || !clientId.trim()) throw new Error('OIDC realm and client ID must be configured.');
  return { oidc: { url: publicUrl(env.VITE_OIDC_URL ?? 'http://localhost:8080', 'OIDC URL'), realm, clientId }, api };
}
