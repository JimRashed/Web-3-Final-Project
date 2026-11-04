import { StrictMode } from 'react';
import { act, fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AuthProvider } from '../../auth/AuthProvider';
import type { Session } from '../../auth/session';
import { deferred, sessionDouble } from '../../test/doubles';
import { LobbyPage } from './LobbyPage';

const lobby = { lobbyId: '01234567-89ab-cdef-0123-456789abcdef', joinCode: 'ABC123', status: 'Open', memberCount: 2, isMember: true, isHost: true };
afterEach(() => { vi.useRealTimers(); vi.unstubAllGlobals(); });
function view(session: Session, strict = false) {
  const page = <AuthProvider session={session}><MemoryRouter><Routes><Route path="/" element={<LobbyPage />} /><Route path="/matches" element={<h1>Navigated to match</h1>} /></Routes></MemoryRouter></AuthProvider>;
  return strict ? <StrictMode>{page}</StrictMode> : page;
}
async function mount(fetcher: ReturnType<typeof vi.fn<typeof fetch>>, strict = false) {
  vi.stubGlobal('fetch', fetcher); vi.useFakeTimers();
  const result = await sessionDouble();
  const rendered = render(view(result.session, strict));
  await act(async () => { fireEvent.click(screen.getByRole('button', { name: 'Create lobby' })); });
  expect(screen.getByText('ABC123')).toBeVisible();
  return { ...result, ...rendered };
}
async function advance(milliseconds: number) { await act(async () => { await vi.advanceTimersByTimeAsync(milliseconds); }); }

describe('Lobby request lifecycle', () => {
  it('polls sequentially two seconds after settlement without overlapping a pending read', async () => {
    const pending = deferred<Response>();
    const fetcher = vi.fn<typeof fetch>().mockImplementation(async () => Response.json(lobby)).mockResolvedValueOnce(Response.json(lobby)).mockReturnValueOnce(pending.promise);
    await mount(fetcher); await advance(2000); expect(fetcher).toHaveBeenCalledTimes(2);
    await advance(10000); expect(fetcher).toHaveBeenCalledTimes(2);
    await act(async () => { pending.resolve(Response.json(lobby)); });
    await advance(1999); expect(fetcher).toHaveBeenCalledTimes(2);
    await advance(1); expect(fetcher).toHaveBeenCalledTimes(3);
  });
  it('does not let an old Open poll overwrite a successful Locked mutation', async () => {
    const pending = deferred<Response>();
    const fetcher = vi.fn<typeof fetch>().mockResolvedValueOnce(Response.json(lobby)).mockReturnValueOnce(pending.promise).mockResolvedValueOnce(Response.json({ ...lobby, status: 'Locked' }));
    await mount(fetcher); await advance(2000);
    const signal = fetcher.mock.calls[1][1]?.signal;
    await act(async () => { fireEvent.click(screen.getByRole('button', { name: 'Lock lobby' })); });
    expect(signal?.aborted).toBe(true); expect(screen.getByText('Locked')).toBeVisible();
    await act(async () => { pending.resolve(Response.json(lobby)); });
    expect(screen.getByText('Locked')).toBeVisible(); expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });
  it('invalidates reads from the previous lobby when selecting a newly created lobby', async () => {
    const pending = deferred<Response>();
    const fetcher = vi.fn<typeof fetch>().mockResolvedValueOnce(Response.json(lobby)).mockReturnValueOnce(pending.promise).mockResolvedValueOnce(Response.json({ ...lobby, joinCode: 'XYZ789' }));
    await mount(fetcher); await advance(2000);
    await act(async () => { fireEvent.click(screen.getByRole('button', { name: 'Create lobby' })); });
    await act(async () => { pending.resolve(Response.json(lobby)); });
    expect(screen.getByText('XYZ789')).toBeVisible(); expect(screen.queryByText('ABC123')).not.toBeInTheDocument();
  });
  it('guards repeated form submissions immediately, beyond rendered disabled buttons', async () => {
    vi.useFakeTimers(); const pending = deferred<Response>();
    const fetcher = vi.fn<typeof fetch>().mockReturnValue(pending.promise); vi.stubGlobal('fetch', fetcher);
    const { session } = await sessionDouble(); render(view(session));
    const field = screen.getByRole('textbox', { name: 'Join code' }); fireEvent.change(field, { target: { value: 'ABC123' } });
    await act(async () => { fireEvent.submit(field.closest('form')!); fireEvent.submit(field.closest('form')!); });
    expect(fetcher).toHaveBeenCalledOnce();
    await act(async () => { pending.resolve(Response.json(lobby)); });
  });
  it('aborts pending reads and removes timers on unmount, ignoring late rejection', async () => {
    const pending = deferred<Response>();
    const fetcher = vi.fn<typeof fetch>().mockResolvedValueOnce(Response.json(lobby)).mockReturnValueOnce(pending.promise);
    const { unmount } = await mount(fetcher); await advance(2000);
    const signal = fetcher.mock.calls[1][1]?.signal;
    unmount(); expect(signal?.aborted).toBe(true);
    await act(async () => { pending.reject(new Error('late SECRET')); });
    await advance(10000); expect(fetcher).toHaveBeenCalledTimes(2);
  });
  it('clears a lobby and aborts a pending start on expiry without late navigation or login', async () => {
    const pending = deferred<Response>();
    const fetcher = vi.fn<typeof fetch>().mockResolvedValueOnce(Response.json(lobby)).mockResolvedValueOnce(Response.json({ ...lobby, status: 'Locked' })).mockReturnValueOnce(pending.promise);
    const { session, adapter } = await mount(fetcher);
    await act(async () => { fireEvent.click(screen.getByRole('button', { name: 'Lock lobby' })); });
    await act(async () => { fireEvent.click(screen.getByRole('button', { name: 'Start match' })); });
    const signal = fetcher.mock.calls[2][1]?.signal;
    act(() => session.invalidate()); expect(signal?.aborted).toBe(true);
    await act(async () => { pending.resolve(Response.json({ matchId: 'too-late' })); });
    expect(screen.getByRole('button', { name: 'Sign in' })).toBeVisible(); expect(screen.queryByText('ABC123')).not.toBeInTheDocument();
    expect(screen.queryByText('Navigated to match')).not.toBeInTheDocument(); expect(adapter.login).not.toHaveBeenCalled();
  });
  it('clears data and cancels old-session reads when an authenticated session is replaced', async () => {
    const pending = deferred<Response>();
    const fetcher = vi.fn<typeof fetch>().mockResolvedValueOnce(Response.json(lobby)).mockReturnValueOnce(pending.promise);
    const { rerender } = await mount(fetcher); await advance(2000);
    const replacement = await sessionDouble(); rerender(view(replacement.session));
    expect(fetcher.mock.calls[1][1]?.signal?.aborted).toBe(true);
    await act(async () => { pending.resolve(Response.json(lobby)); });
    expect(screen.getByRole('heading', { name: 'No lobby selected' })).toBeVisible(); expect(screen.queryByText('ABC123')).not.toBeInTheDocument();
  });
  it('stops a failing poll loop until explicit recovery', async () => {
    const fetcher = vi.fn<typeof fetch>().mockResolvedValueOnce(Response.json(lobby)).mockResolvedValueOnce(new Response(null, { status: 503 })).mockResolvedValueOnce(Response.json(lobby));
    await mount(fetcher); await advance(2000); await advance(10000); expect(fetcher).toHaveBeenCalledTimes(2);
    await act(async () => { fireEvent.click(screen.getByRole('button', { name: 'Try again' })); });
    expect(fetcher).toHaveBeenCalledTimes(3); expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });
  it('keeps one poll lifecycle under StrictMode setup and cleanup', async () => {
    const fetcher = vi.fn<typeof fetch>().mockImplementation(async () => Response.json(lobby));
    await mount(fetcher, true); await advance(2000); expect(fetcher).toHaveBeenCalledTimes(2);
    await advance(2000); expect(fetcher).toHaveBeenCalledTimes(3);
  });
});
