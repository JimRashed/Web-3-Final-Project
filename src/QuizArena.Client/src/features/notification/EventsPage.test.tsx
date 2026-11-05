import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AuthProvider } from '../../auth/AuthProvider';
import { sessionDouble } from '../../test/doubles';
import { EventsPage } from './EventsPage';

afterEach(() => { vi.unstubAllGlobals(); });

async function mount(value: unknown | Response) {
  const fetcher = vi.fn<typeof fetch>().mockResolvedValue(value instanceof Response ? value : Response.json(value));
  vi.stubGlobal('fetch', fetcher);
  const { session } = await sessionDouble(true);
  render(<AuthProvider session={session}><MemoryRouter><EventsPage /></MemoryRouter></AuthProvider>);
  return fetcher;
}

describe('Events page', () => {
  it('lists the signed-in player events', async () => {
    await mount([
      { eventId: '26c94137-d660-4397-a731-050c2bb07004', type: 'MATCH_CREATED', userId: 'player-a', payload: { matchId: 'match-1' }, createdAt: '2026-11-04T12:00:00Z' },
      { eventId: '36c94137-d660-4397-a731-050c2bb07005', type: 'MATCH_COMPLETED', userId: 'player-a', payload: { matchId: 'match-1', winnerUserId: 'player-a', draw: false }, createdAt: '2026-11-04T12:05:00Z' },
    ]);

    expect(await screen.findByText('Match started')).toBeVisible();
    expect(screen.getByText('Match finished')).toBeVisible();
  });

  it('links each event to its match inside the app, encoding opaque ids', async () => {
    await mount([
      { eventId: '26c94137-d660-4397-a731-050c2bb07004', type: 'MATCH_CREATED', userId: 'player-a', payload: { matchId: 'a/b c' }, createdAt: '2026-11-04T12:00:00Z' },
      { eventId: '36c94137-d660-4397-a731-050c2bb07005', type: 'MATCH_COMPLETED', userId: 'player-a', payload: { matchId: 'a/b c', winnerUserId: 'player-a', draw: false }, createdAt: '2026-11-04T12:05:00Z' },
    ]);

    expect(await screen.findByRole('link', { name: 'Open match' })).toHaveAttribute('href', '/matches?matchId=a%2Fb%20c');
    expect(screen.getByRole('link', { name: 'View result' })).toHaveAttribute('href', '/matches?matchId=a%2Fb%20c');
  });

  it('shows the empty state when the player has no events', async () => {
    await mount([]);

    expect(await screen.findByRole('heading', { name: 'You’re all caught up' })).toBeVisible();
  });

  it('shows an error state instead of a partial timeline', async () => {
    await mount(Response.json({ title: 'failed' }, { status: 500 }));

    expect(await screen.findByRole('heading', { name: 'Unable to load' })).toBeVisible();
  });
});
