import { expect, it, vi } from 'vitest';
import { readConfig, serviceNames } from '../config';
import { sessionDouble } from '../test/doubles';
import { createClients } from './clients';

it('routes all six clients to their independently configured service', async () => {
  const { session } = await sessionDouble();
  const config = readConfig(Object.fromEntries(serviceNames.map((name) => [`VITE_${name.toUpperCase()}_API_URL`, `https://${name}.example`])));
  const fetcher = vi.fn<typeof fetch>().mockImplementation(async () => Response.json({ ok: true }));
  const clients = createClients(config, session, fetcher);
  for (const name of serviceNames) await clients[name].request('/health');
  expect(fetcher.mock.calls.map(([url]) => String(url))).toEqual(serviceNames.map((name) => `https://${name}.example/health`));
});
