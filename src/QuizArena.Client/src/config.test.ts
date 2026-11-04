import { describe, expect, it } from 'vitest';
import { readConfig, serviceNames } from './config';

describe('public configuration', () => {
  it('uses the configured local provider and six distinct service ports', () => {
    const config = readConfig({});
    expect(config.oidc).toEqual({ url: 'http://localhost:8080', realm: 'quizarena', clientId: 'quizarena-client' });
    serviceNames.forEach((name, index) => expect(config.api[name]).toBe(`http://localhost:${5101 + index}`));
  });
  it('allows independent public service settings', () => {
    const env = Object.fromEntries(serviceNames.map((name) => [`VITE_${name.toUpperCase()}_API_URL`, `https://${name}.example/v1/`]));
    const config = readConfig(env);
    serviceNames.forEach((name) => expect(config.api[name]).toBe(`https://${name}.example/v1`));
  });
  it.each(['ftp://host', 'javascript:alert(1)', 'https://user:pass@host', 'https://host/?key=x', 'https://host/#fragment', 'not-a-url'])('rejects unsafe base %s', (url) => {
    expect(() => readConfig({ VITE_IDENTITY_API_URL: url })).toThrow();
  });
  it('rejects an empty client ID and an ambiguous realm', () => {
    expect(() => readConfig({ VITE_OIDC_CLIENT_ID: ' ' })).toThrow();
    expect(() => readConfig({ VITE_OIDC_REALM: '../realm' })).toThrow();
  });
});
