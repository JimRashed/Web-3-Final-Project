import { act, fireEvent, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AuthProvider } from '../auth/AuthProvider';
import { sessionDouble } from '../test/doubles';
import { App } from './App';
import { destinations } from './routes';

afterEach(() => vi.unstubAllGlobals());

async function mount(path = '/', authenticated = false) {
  const result = await sessionDouble(authenticated);
  render(<AuthProvider session={result.session}><MemoryRouter initialEntries={[path]}><App /></MemoryRouter></AuthProvider>);
  return result;
}
describe('application shell', () => {
  it('shows the overview and eight accessible destinations without fabricated statistics', async () => {
    await mount();
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Curiosity.Meet competition.');
    const navigation = screen.getByRole('navigation', { name: 'Main navigation' });
    for (const item of destinations) expect(within(navigation).getByRole('link', { name: item.label })).toHaveAttribute('href', item.path);
    expect(screen.getByRole('link', { name: 'Skip to content' })).toHaveAttribute('href', '#main');
  });
  it.each(destinations)('renders $label and marks its route active', async (item) => {
    await mount(item.path, true);
    expect(screen.getByRole('heading', { level: 1, name: item.label })).toBeVisible();
    expect(within(screen.getByRole('navigation')).getByRole('link', { name: item.label })).toHaveAttribute('aria-current', 'page');
    expect(screen.getByRole('heading', { name: item.empty })).toBeVisible();
  });
  it('navigates using the shell links', async () => {
    await mount('/', true);
    await userEvent.click(within(screen.getByRole('navigation')).getByRole('link', { name: 'Lobby' }));
    expect(screen.getByRole('heading', { level: 1, name: 'Lobby' })).toBeVisible();
  });
  it('requests sign-in only after an explicit action', async () => {
    const { adapter } = await mount('/profile');
    expect(adapter.login).not.toHaveBeenCalled();
    await userEvent.click(within(screen.getByRole('main')).getByRole('button', { name: 'Sign in' }));
    expect(adapter.login).toHaveBeenCalledOnce();
  });
  it('clears the displayed player and returns to sign-in after expiry', async () => {
    const { session, adapter } = await mount('/profile', true);
    act(() => session.invalidate());
    expect(screen.queryByText('CuriousPlayer')).not.toBeInTheDocument();
    expect(screen.getByText('Your session ended. Sign in when you’re ready to continue.')).toBeVisible();
    expect(adapter.login).not.toHaveBeenCalled();
  });
  it('handles an unknown route with a way home', async () => {
    await mount('/missing');
    expect(screen.getByRole('heading', { name: 'A little off course' })).toBeVisible();
    expect(screen.getByRole('link', { name: /Back to the arena/ })).toHaveAttribute('href', '/');
  });
  it.each([200, 201])('mounts real Lobby and routes returned match ID to an honest copyable shell on %s', async (status) => {
    const lobby = { lobbyId: '01234567-89ab-cdef-0123-456789abcdef', joinCode: 'ABC123', status: 'Open', memberCount: 2, isMember: true, isHost: true };
    const fetcher = vi.fn<typeof fetch>().mockResolvedValueOnce(Response.json(lobby)).mockResolvedValueOnce(Response.json({ ...lobby, status: 'Locked' })).mockResolvedValueOnce(Response.json({ matchId: 'actual returned id' }, { status }));
    vi.stubGlobal('fetch', fetcher); await mount('/lobby', true);
    fireEvent.click(screen.getByRole('button', { name: 'Create lobby' })); await screen.findByText('ABC123');
    fireEvent.click(screen.getByRole('button', { name: 'Lock lobby' })); await screen.findByText('Locked');
    fireEvent.click(screen.getByRole('button', { name: 'Start match' }));
    await screen.findByRole('heading', { name: 'Selected match' });
    expect(screen.getByText('Match details and participant verification are not implemented in this screen yet.')).toBeVisible();
    const link = screen.getByRole('link', { name: /http.*matches\?matchId=/ });
    const url = new URL(link.getAttribute('href')!);
    expect(url.origin).toBe(window.location.origin);
    expect(url.pathname).toBe('/matches');
    expect(url.searchParams.get('matchId')).toBe('actual returned id');
    expect(screen.getByRole('button', { name: 'Copy match link' })).toBeVisible();
    expect(fetcher).toHaveBeenCalledTimes(3);
  });
  it.each([200, 201].flatMap((status) => ['.', '..', '%2E', '%252E', 'path/part?key=value+ space&other=100%#fragment', 'part\nid', '😀'.repeat(51), '😀'.repeat(100)].map((matchId) => ({ status, matchId }))))('roundtrips opaque returned ID $matchId through navigation and copied URL on $status', async ({ status, matchId }) => {
    const lobby = { lobbyId: '01234567-89ab-cdef-0123-456789abcdef', joinCode: 'ABC123', status: 'Open', memberCount: 2, isMember: true, isHost: true };
    const fetcher = vi.fn<typeof fetch>().mockResolvedValueOnce(Response.json(lobby)).mockResolvedValueOnce(Response.json({ ...lobby, status: 'Locked' })).mockResolvedValueOnce(Response.json({ matchId }, { status }));
    const writeText = vi.fn().mockResolvedValue(undefined);
    vi.stubGlobal('fetch', fetcher); vi.stubGlobal('navigator', { clipboard: { writeText } });
    await mount('/lobby', true);
    fireEvent.click(screen.getByRole('button', { name: 'Create lobby' })); await screen.findByText('ABC123');
    fireEvent.click(screen.getByRole('button', { name: 'Lock lobby' })); await screen.findByText('Locked');
    fireEvent.click(screen.getByRole('button', { name: 'Start match' }));
    await screen.findByRole('heading', { name: 'Selected match' });
    const returned = screen.getByText((_, element) => element?.tagName === 'P' && element.textContent === `Returned match ID: ${matchId}`);
    expect(returned).toBeVisible();
    expect(returned.textContent).toBe(`Returned match ID: ${matchId}`);
    const href = screen.getByRole('link', { name: /http.*matches\?matchId=/ }).getAttribute('href')!;
    const destination = new URL(href);
    expect(destination.origin).toBe(window.location.origin);
    expect(destination.pathname).toBe('/matches');
    expect(destination.searchParams.get('matchId')).toBe(matchId);
    expect(destination.searchParams.size).toBe(1);
    expect(destination.hash).toBe('');
    fireEvent.click(screen.getByRole('button', { name: 'Copy match link' }));
    await screen.findByText('Match link copied.');
    expect(writeText).toHaveBeenCalledWith(href);
    expect(new URL(writeText.mock.calls[0][0] as string).searchParams.get('matchId')).toBe(matchId);
    expect(fetcher).toHaveBeenCalledTimes(3);
  });
  it('uses the existing Events placeholder for the documented notifications alias', async () => {
    await mount('/notifications', true);
    expect(screen.getByRole('heading', { level: 1, name: 'Events' })).toBeVisible();
  });
});
