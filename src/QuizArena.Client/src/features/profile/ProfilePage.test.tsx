import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AuthProvider } from '../../auth/AuthProvider';
import { deferred } from '../../test/doubles';
import { problem, requested, routeFetch, subject, subjectSession } from '../../test/routes';
import { ProfilePage } from './ProfilePage';

afterEach(() => { vi.unstubAllGlobals(); });

async function mount(authenticated = true) {
  const result = await subjectSession(subject, authenticated);
  render(<AuthProvider session={result.session}><MemoryRouter><ProfilePage /></MemoryRouter></AuthProvider>);
  return result;
}
const profile = (displayName: string) => ({ id: '26c94137-d660-4397-a731-050c2bb07001', issuer: 'http://localhost:8080/realms/quizarena', subject, displayName });
const field = () => screen.getByRole('textbox', { name: 'Display name' });
const save = () => fireEvent.click(screen.getByRole('button', { name: 'Save display name' }));

describe('Profile page', () => {
  it('offers provider sign-in and no password form while anonymous', async () => {
    const fetcher = routeFetch({});
    const { adapter } = await mount(false);

    expect(document.querySelector('input[type="password"]')).toBeNull();
    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));

    await waitFor(() => expect(adapter.login).toHaveBeenCalledOnce());
    expect(fetcher).not.toHaveBeenCalled();
  });

  it('shows the stored profile and saves a trimmed new name', async () => {
    const fetcher = routeFetch({ 'GET /api/users/me': profile('Ada'), 'PUT /api/users/me': profile('Grace') });
    await mount();

    expect(await screen.findByText('Ada')).toBeVisible();
    fireEvent.change(field(), { target: { value: '  Grace ' } });
    save();

    expect(await screen.findByText('Display name saved.')).toBeVisible();
    expect(screen.getByText('Grace')).toBeVisible();
    const put = fetcher.mock.calls.find(([, init]) => init?.method === 'PUT');
    expect(JSON.parse(String(put?.[1]?.body))).toEqual({ displayName: 'Grace' });
  });

  it('invites a first-time player to create a profile from the token name', async () => {
    routeFetch({ 'GET /api/users/me': problem(404), 'PUT /api/users/me': profile('CuriousPlayer') });
    await mount();

    expect(await screen.findByText('You have not chosen a display name yet. Save one to create your profile.')).toBeVisible();
    expect(field()).toHaveValue('CuriousPlayer');
    save();

    expect(await screen.findByText('Display name saved.')).toBeVisible();
  });

  it('validates locally, focuses the field and sends nothing', async () => {
    const fetcher = routeFetch({ 'GET /api/users/me': profile('Ada') });
    await mount();
    await screen.findByText('Ada');

    fireEvent.change(field(), { target: { value: '   ' } });
    save();

    expect(field()).toHaveFocus();
    expect(field()).toHaveAttribute('aria-invalid', 'true');
    expect(screen.getByText('Enter a display name.')).toBeVisible();
    expect(requested(fetcher)).toEqual(['GET /api/users/me']);
  });

  it('shows the saving state and blocks a second submit', async () => {
    const pending = deferred<Response>();
    const fetcher = routeFetch({ 'GET /api/users/me': profile('Ada'), 'PUT /api/users/me': () => pending.promise });
    await mount();
    await screen.findByText('Ada');
    fireEvent.change(field(), { target: { value: 'Grace' } });

    save();

    const busy = await screen.findByRole('button', { name: 'Saving…' });
    expect(busy).toBeDisabled();
    fireEvent.submit(busy.closest('form')!);
    pending.resolve(Response.json(profile('Grace')));
    expect(await screen.findByText('Display name saved.')).toBeVisible();
    expect(requested(fetcher).filter((call) => call.startsWith('PUT'))).toHaveLength(1);
  });

  it('shows the server field error from a 400', async () => {
    routeFetch({ 'GET /api/users/me': profile('Ada'), 'PUT /api/users/me': problem(400, { displayName: ['Display name must be at most 80 characters.'] }) });
    await mount();
    await screen.findByText('Ada');

    save();

    expect(await screen.findByText('Display name must be at most 80 characters.')).toBeVisible();
    expect(field()).toHaveAttribute('aria-invalid', 'true');
  });

  it('shows a permission message on 403', async () => {
    routeFetch({ 'GET /api/users/me': problem(403) });
    await mount();

    expect(await screen.findByRole('heading', { name: 'Access unavailable' })).toBeVisible();
    expect(screen.getByText('You do not have permission to do this.')).toBeVisible();
  });

  it('signs out through the provider', async () => {
    routeFetch({ 'GET /api/users/me': profile('Ada') });
    const { navigate } = await mount();
    await screen.findByText('Ada');

    fireEvent.click(screen.getByRole('button', { name: 'Sign out' }));

    expect(navigate).toHaveBeenCalledWith('https://provider.example/logout');
    expect(await screen.findByRole('button', { name: 'Sign in' })).toBeVisible();
  });
});
