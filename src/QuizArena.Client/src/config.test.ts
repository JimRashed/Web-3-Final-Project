import { describe, expect, it } from 'vitest';
import { readConfig, serviceNames } from './config';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';

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
  it('documents only public VITE_ settings in the committed .env.example', () => {
    // Vite denies importing .env files (server.fs.deny), so the test reads the committed file from the client root (the Vitest root).
    const envExample = readFileSync(resolve(process.cwd(), '.env.example'), 'utf8');
    const entries = envExample.split(/\r?\n/).map((line) => line.trim()).filter((line) => line && !line.startsWith('#'));
    expect(entries.length).toBeGreaterThan(0);
    entries.forEach((line) => expect(line).toMatch(/^VITE_[A-Z0-9_]+=/));
    const keys = entries.map((line) => line.slice(0, line.indexOf('=')));
    keys.forEach((key) => expect(key).not.toMatch(/SECRET|PASSWORD|TOKEN|KEY/i));
  });
});
