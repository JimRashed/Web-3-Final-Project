import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AuthProvider } from '../../auth/AuthProvider';
import { Session } from '../../auth/session';
import { adapterDouble, deferred, sessionDouble } from '../../test/doubles';
import { LobbyPage } from './LobbyPage';

const lobby = { lobbyId: '01234567-89ab-cdef-0123-456789abcdef', joinCode: 'ABC123', status: 'Open', memberCount: 1, isMember: true, isHost: true };
afterEach(() => { vi.unstubAllGlobals(); });
function Location() { const location = useLocation(); return <p>{location.pathname}{location.search}</p>; }
async function mount(authenticated = true) {
  const result = await sessionDouble(authenticated);
  render(<AuthProvider session={result.session}><MemoryRouter initialEntries={['/lobby']}><Routes><Route path="/lobby" element={<LobbyPage />} /><Route path="/matches" element={<Location />} /></Routes></MemoryRouter></AuthProvider>);
  return result;
}
function fetchResponses(...values: Array<unknown | Response>) {
  const fetcher = vi.fn<typeof fetch>();
  values.forEach((value) => fetcher.mockResolvedValueOnce(value instanceof Response ? value : Response.json(value)));
  vi.stubGlobal('fetch', fetcher);
  return fetcher;
}
async function create() {
  fireEvent.click(screen.getByRole('button', { name: 'Create lobby' }));
  await screen.findByText('ABC123');
}

describe('Lobby page', () => {
  it('requires explicit sign-in and makes no request while anonymous', async () => {
    const fetcher = fetchResponses(); const { adapter } = await mount(false);
    expect(adapter.login).not.toHaveBeenCalled(); expect(fetcher).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));
    await waitFor(() => expect(adapter.login).toHaveBeenCalledOnce());
  });
  it('shows loading and unavailable states without requests', async () => {
    const pending = deferred<boolean>(); const adapter = adapterDouble(); adapter.init = vi.fn(() => pending.promise);
    const session = new Session(adapter, 'http://localhost:5173'); void session.initialize();
    const result = render(<AuthProvider session={session}><MemoryRouter><LobbyPage /></MemoryRouter></AuthProvider>);
    expect(screen.getByText('Connecting to your session…')).toBeVisible();
    result.unmount();
    session.invalidate('unavailable');
    render(<AuthProvider session={session}><MemoryRouter><LobbyPage /></MemoryRouter></AuthProvider>);
    expect(screen.getByRole('heading', { name: 'Sign-in unavailable' })).toBeVisible();
  });
  it.each(['', 'abc123', ' ABC12', 'ABC12 ', 'ABC12'])('validates exact local code %j and focuses the field', async (code) => {
    const fetcher = fetchResponses(); await mount();
    const field = screen.getByRole('textbox', { name: 'Join code' });
    fireEvent.change(field, { target: { value: code } }); fireEvent.click(screen.getByRole('button', { name: 'Join lobby' }));
    expect(field).toHaveFocus(); expect(field).toHaveAttribute('aria-invalid', 'true');
    expect(screen.getByText('Enter exactly six uppercase letters or digits.')).toBeVisible(); expect(fetcher).not.toHaveBeenCalled();
  });
  it('creates a host lobby, displays counts and waits before enabling lock', async () => {
    fetchResponses(lobby); await mount(); expect(screen.getByRole('heading', { name: 'No lobby selected' })).toBeVisible();
    await create();
    expect(screen.getByText('1 of 2')).toBeVisible(); expect(screen.getByText('You are the host.')).toBeVisible();
    expect(screen.getByRole('button', { name: 'Lock lobby' })).toBeDisabled(); expect(screen.getByRole('button', { name: 'Start match' })).toBeDisabled();
  });
  it('joins as a guest and explains disabled host controls without inventing names', async () => {
    const fetcher = fetchResponses({ ...lobby, memberCount: 2, isHost: false }); await mount();
    fireEvent.change(screen.getByRole('textbox', { name: 'Join code' }), { target: { value: 'ABC123' } });
    fireEvent.click(screen.getByRole('button', { name: 'Join lobby' })); await screen.findByText('ABC123');
    expect(String(fetcher.mock.calls[0][0])).toContain('/ABC123/join');
    expect(screen.getByText('You are a member.')).toBeVisible(); expect(screen.getByText(/Only the host/)).toBeVisible();
    expect(screen.getByRole('button', { name: 'Lock lobby' })).toBeDisabled(); expect(screen.getByRole('button', { name: 'Start match' })).toBeDisabled();
  });
  it.each([200, 201])('locks before starting and navigates only to the returned encoded match ID (%s)', async (status) => {
    const fetcher = fetchResponses({ ...lobby, memberCount: 2 }, { ...lobby, status: 'Locked', memberCount: 2 }, Response.json({ matchId: 'actual/id' }, { status }));
    await mount(); await create(); expect(screen.getByRole('button', { name: 'Start match' })).toBeDisabled();
    fireEvent.click(screen.getByRole('button', { name: 'Lock lobby' })); await screen.findByText('Locked');
    fireEvent.click(screen.getByRole('button', { name: 'Start match' }));
    await screen.findByText('/matches?matchId=actual%2Fid'); expect(fetcher).toHaveBeenCalledTimes(3);
    expect(String(fetcher.mock.calls[2][0])).toBe('http://localhost:5105/matches');
  });
  it('copies the code and announces success', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined); vi.stubGlobal('navigator', { clipboard: { writeText } });
    fetchResponses(lobby); await mount(); await create(); fireEvent.click(screen.getByRole('button', { name: 'Copy code' }));
    await screen.findByText('Join code copied.'); expect(writeText).toHaveBeenCalledWith('ABC123');
  });
  it.each([false, true])('offers selectable manual copy if clipboard is absent or rejects (%s)', async (rejects) => {
    vi.stubGlobal('navigator', rejects ? { clipboard: { writeText: vi.fn().mockRejectedValue(new Error('denied')) } } : {});
    fetchResponses(lobby); await mount(); await create(); fireEvent.click(screen.getByRole('button', { name: 'Copy code' }));
    await screen.findByText('Copy is unavailable. Select the join code and copy it manually.'); expect(screen.getByText('ABC123')).toBeVisible();
  });
  it.each([400, 403, 404, 503, 500])('shows safe %s errors without automatic mutation retry', async (status) => {
    const fetcher = fetchResponses(Response.json({ detail: '<SECRET>' }, { status })); const { session, adapter } = await mount();
    fireEvent.click(screen.getByRole('button', { name: 'Create lobby' })); await screen.findByRole('alert');
    expect(screen.queryByText(/SECRET/)).not.toBeInTheDocument(); expect(fetcher).toHaveBeenCalledOnce();
    expect(session.getSnapshot().status).toBe('authenticated'); expect(adapter.login).not.toHaveBeenCalled();
    if (status === 403) expect(screen.queryByRole('button', { name: 'Try again' })).not.toBeInTheDocument();
  });
  it('clears selected data/code on401 and offers explicit sign-in without redirect', async () => {
    fetchResponses({ ...lobby, memberCount: 2 }, new Response(null, { status: 401 })); const { session, adapter } = await mount();
    await create(); fireEvent.click(screen.getByRole('button', { name: 'Lock lobby' }));
    await screen.findByRole('button', { name: 'Sign in' }); expect(screen.queryByText('ABC123')).not.toBeInTheDocument();
    expect(session.getSnapshot().status).toBe('expired'); expect(adapter.login).not.toHaveBeenCalled();
  });
  it('refreshes once after409 without repeating the mutation', async () => {
    const fetcher = fetchResponses({ ...lobby, memberCount: 2 }, new Response(null, { status: 409 }), { ...lobby, status: 'Locked', memberCount: 2 });
    await mount(); await create(); fireEvent.click(screen.getByRole('button', { name: 'Lock lobby' }));
    await screen.findByText('Locked'); expect(fetcher).toHaveBeenCalledTimes(3);
    expect(fetcher.mock.calls[2][1]?.method ?? 'GET').toBe('GET'); expect(screen.getByRole('alert')).toHaveTextContent('This item has changed.');
  });
  it('retries503 only after an explicit action', async () => {
    const fetcher = fetchResponses(new Response(null, { status: 503 }), lobby); await mount();
    fireEvent.click(screen.getByRole('button', { name: 'Create lobby' })); fireEvent.click(await screen.findByRole('button', { name: 'Try again' }));
    await screen.findByText('ABC123'); expect(fetcher).toHaveBeenCalledTimes(2);
  });
  it('offers safe network recovery', async () => {
    const fetcher = vi.fn<typeof fetch>().mockRejectedValue(new TypeError('SECRET-network')); vi.stubGlobal('fetch', fetcher); await mount();
    fireEvent.click(screen.getByRole('button', { name: 'Create lobby' })); await screen.findByText('Unable to connect. Check your connection and try again.');
    expect(screen.getByRole('button', { name: 'Try again' })).toBeVisible(); expect(fetcher).toHaveBeenCalledOnce();
  });
});
