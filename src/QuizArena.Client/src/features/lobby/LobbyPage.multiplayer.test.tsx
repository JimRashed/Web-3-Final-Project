import { act, fireEvent, render, within } from '@testing-library/react';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AuthProvider } from '../../auth/AuthProvider';
import type { Session } from '../../auth/session';
import { sessionDouble } from '../../test/doubles';
import { LobbyPage } from './LobbyPage';

afterEach(() => { vi.useRealTimers(); vi.unstubAllGlobals(); });

function MatchLocation() {
  const location = useLocation();
  return <p>Selected match route: {location.pathname}{location.search}</p>;
}

function view(session: Session) {
  return <AuthProvider session={session}><MemoryRouter initialEntries={['/lobby']}><Routes>
    <Route path="/lobby" element={<LobbyPage />} />
    <Route path="/matches" element={<MatchLocation />} />
  </Routes></MemoryRouter></AuthProvider>;
}

describe('two independent Lobby browser sessions', () => {
  it('lets a host create, a guest join, the host refresh and lock, then only the host start the returned match', async () => {
    // Arrange: the double owns only public protocol responses, with a separate Gameplay origin.
    vi.useFakeTimers();
    const host = await sessionDouble();
    const guest = await sessionDouble();
    host.adapter.token = 'access-host';
    guest.adapter.token = 'access-guest';
    const lobbyId = '01234567-89ab-cdef-0123-456789abcdef';
    const joinCode = 'ABC123';
    const members = ['host'];
    let status: 'Open' | 'Locked' = 'Open';
    const publicView = (subject: string) => ({
      lobbyId, joinCode, status, memberCount: members.length,
      isMember: members.includes(subject), isHost: subject === 'host',
    });
    const fetcher = vi.fn<typeof fetch>(async (input, options) => {
      const url = new URL(String(input));
      const authorization = new Headers(options?.headers).get('Authorization');
      const subject = authorization === 'Bearer access-host' ? 'host' : authorization === 'Bearer access-guest' ? 'guest' : '';
      if (!subject) throw new Error('Unexpected test token');
      if (url.origin === 'http://localhost:5104' && url.pathname === '/api/lobbies' && options?.method === 'POST') {
        return Response.json(publicView(subject), { status: 201 });
      }
      if (url.origin === 'http://localhost:5104' && url.pathname === '/api/lobbies/' + joinCode + '/join' && options?.method === 'POST') {
        members.push(subject);
        return Response.json(publicView(subject));
      }
      if (url.origin === 'http://localhost:5104' && url.pathname === '/api/lobbies/' + joinCode && (options?.method ?? 'GET') === 'GET') {
        return Response.json(publicView(subject));
      }
      if (url.origin === 'http://localhost:5104' && url.pathname === '/api/lobbies/' + joinCode + '/lock' && options?.method === 'POST') {
        status = 'Locked';
        return Response.json(publicView(subject));
      }
      if (url.origin === 'http://localhost:5105' && url.pathname === '/matches' && options?.method === 'POST') {
        return Response.json({ matchId: 'game/42' }, { status: 201 });
      }
      throw new Error('Unexpected test request');
    });
    vi.stubGlobal('fetch', fetcher);
    const hostRender = render(view(host.session));
    const guestRender = render(view(guest.session));
    const hostPage = within(hostRender.container);
    const guestPage = within(guestRender.container);

    // Act: host creates and guest joins the returned public code.
    await act(async () => { fireEvent.click(hostPage.getByRole('button', { name: 'Create lobby' })); });
    expect(hostPage.getByText('1 of 2')).toBeVisible();
    expect(hostPage.getByRole('button', { name: 'Lock lobby' })).toBeDisabled();
    expect(hostPage.getByRole('button', { name: 'Start match' })).toBeDisabled();
    // Each render is scoped; duplicate field IDs share this test document, unlike separate browser tabs.
    fireEvent.change(guestPage.getByRole('textbox'), { target: { value: hostPage.getByText(joinCode).textContent } });
    await act(async () => { fireEvent.click(guestPage.getByRole('button', { name: 'Join lobby' })); });
    expect(guestPage.getByText('2 of 2')).toBeVisible();
    expect(guestPage.getByText('You are a member.')).toBeVisible();
    expect(guestPage.getByRole('button', { name: 'Lock lobby' })).toBeDisabled();
    expect(guestPage.getByRole('button', { name: 'Start match' })).toBeDisabled();
    expect(fetcher.mock.calls.some(([input]) => new URL(String(input)).pathname === '/matches')).toBe(false);

    // Act: normal polling reveals the second member to the host, who locks.
    await act(async () => { await vi.advanceTimersByTimeAsync(2000); });
    expect(hostPage.getByText('2 of 2')).toBeVisible();
    expect(hostPage.getByRole('button', { name: 'Lock lobby' })).toBeEnabled();
    await act(async () => { fireEvent.click(hostPage.getByRole('button', { name: 'Lock lobby' })); });
    expect(hostPage.getByText('Locked')).toBeVisible();
    await act(async () => { await vi.advanceTimersByTimeAsync(2000); });
    expect(guestPage.getByText('Locked')).toBeVisible();
    expect(guestPage.getByRole('button', { name: 'Lock lobby' })).toBeDisabled();
    expect(guestPage.getByRole('button', { name: 'Start match' })).toBeDisabled();
    fireEvent.click(guestPage.getByRole('button', { name: 'Lock lobby' }));
    fireEvent.click(guestPage.getByRole('button', { name: 'Start match' }));
    expect(fetcher.mock.calls.some(([input]) => new URL(String(input)).pathname === '/matches')).toBe(false);

    // Act: only the host starts Gameplay and navigates using its returned opaque ID.
    await act(async () => { fireEvent.click(hostPage.getByRole('button', { name: 'Start match' })); });

    // Assert: exact public calls and bodies, no internal route or caller identity override.
    expect(hostPage.getByText('Selected match route: /matches?matchId=game%2F42')).toBeVisible();
    expect(guestPage.getByRole('heading', { name: 'Lobby' })).toBeVisible();
    const calls = fetcher.mock.calls.map(([input, options]) => ({ url: new URL(String(input)), options }));
    expect(calls.filter(({ url }) => url.pathname === '/matches')).toHaveLength(1);
    expect(calls.filter(({ url }) => url.pathname === '/matches')[0].options?.body).toBe('{"joinCode":"ABC123"}');
    expect(new Headers(calls.filter(({ url }) => url.pathname === '/matches')[0].options?.headers).get('Authorization')).toBe('Bearer access-host');
    expect(calls.some(({ url }) => url.pathname.startsWith('/internal/'))).toBe(false);
    expect(calls.every(({ url }) => url.search === '')).toBe(true);
    expect(calls.filter(({ url }) => url.pathname.endsWith('/lock'))).toHaveLength(1);
    expect(calls.filter(({ url }) => url.pathname.endsWith('/lock'))[0].options?.body).toBeUndefined();
    expect(new Headers(calls.filter(({ url }) => url.pathname.endsWith('/lock'))[0].options?.headers).get('Authorization')).toBe('Bearer access-host');
    expect(calls.filter(({ url }) => url.pathname.endsWith('/join'))[0].options?.body).toBe('{}');
    expect(new Headers(calls.filter(({ url }) => url.pathname.endsWith('/join'))[0].options?.headers).get('Authorization')).toBe('Bearer access-guest');
    expect(calls.filter(({ url }) => url.pathname === '/api/lobbies')[0].options?.body).toBe('{}');
    hostRender.unmount();
    guestRender.unmount();
  });
});
