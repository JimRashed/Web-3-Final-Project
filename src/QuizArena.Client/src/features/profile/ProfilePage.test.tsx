import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AuthProvider } from '../../auth/AuthProvider';
import { deferred } from '../../test/doubles';
import { inOrder, problem, requested, routeFetch, subject, subjectSession } from '../../test/routes';
import { ProfilePage } from './ProfilePage';

afterEach(() => { vi.unstubAllGlobals(); });

/** jsdom has no object-URL implementation; the component must still create and revoke one. */
function objectUrls() {
  const create = vi.fn((blob: Blob) => `blob:preview-${blob.size}-${create.mock.calls.length}`);
  const revoke = vi.fn();
  Object.assign(URL, { createObjectURL: create, revokeObjectURL: revoke });
  return { create, revoke };
}

async function mount(authenticated = true) {
  const result = await subjectSession(subject, authenticated);
  const view = render(<AuthProvider session={result.session}><MemoryRouter><ProfilePage /></MemoryRouter></AuthProvider>);
  return { ...result, unmount: view.unmount };
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
    const fetcher = routeFetch({ 'GET /api/users/me/picture': problem(404), 'GET /api/users/me': profile('Ada'), 'PUT /api/users/me': profile('Grace') });
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
    routeFetch({ 'GET /api/users/me/picture': problem(404), 'GET /api/users/me': problem(404), 'PUT /api/users/me': profile('CuriousPlayer') });
    await mount();

    expect(await screen.findByText('You have not chosen a display name yet. Save one to create your profile.')).toBeVisible();
    expect(field()).toHaveValue('CuriousPlayer');
    save();

    expect(await screen.findByText('Display name saved.')).toBeVisible();
  });

  it('validates locally, focuses the field and sends nothing', async () => {
    const fetcher = routeFetch({ 'GET /api/users/me/picture': problem(404), 'GET /api/users/me': profile('Ada') });
    await mount();
    await screen.findByText('Ada');

    fireEvent.change(field(), { target: { value: '   ' } });
    save();

    expect(field()).toHaveFocus();
    expect(field()).toHaveAttribute('aria-invalid', 'true');
    expect(screen.getByText('Enter a display name.')).toBeVisible();
    expect(requested(fetcher).filter((call) => !call.includes('/picture'))).toEqual(['GET /api/users/me']);
  });

  it('shows the saving state and blocks a second submit', async () => {
    const pending = deferred<Response>();
    const fetcher = routeFetch({ 'GET /api/users/me/picture': problem(404), 'GET /api/users/me': profile('Ada'), 'PUT /api/users/me': () => pending.promise });
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
    routeFetch({ 'GET /api/users/me/picture': problem(404), 'GET /api/users/me': profile('Ada'), 'PUT /api/users/me': problem(400, { displayName: ['Display name must be at most 80 characters.'] }) });
    await mount();
    await screen.findByText('Ada');

    save();

    expect(await screen.findByText('Display name must be at most 80 characters.')).toBeVisible();
    expect(field()).toHaveAttribute('aria-invalid', 'true');
  });

  it('shows a permission message on 403', async () => {
    routeFetch({ 'GET /api/users/me/picture': problem(404), 'GET /api/users/me': problem(403) });
    await mount();

    expect(await screen.findByRole('heading', { name: 'Access unavailable' })).toBeVisible();
    expect(screen.getByText('You do not have permission to do this.')).toBeVisible();
  });

  it('signs out through the provider', async () => {
    routeFetch({ 'GET /api/users/me/picture': problem(404), 'GET /api/users/me': profile('Ada') });
    const { navigate } = await mount();
    await screen.findByText('Ada');

    fireEvent.click(screen.getByRole('button', { name: 'Sign out' }));

    expect(navigate).toHaveBeenCalledWith('https://provider.example/logout');
    expect(await screen.findByRole('button', { name: 'Sign in' })).toBeVisible();
  });

  describe('profile picture', () => {
    const png = () => new Response(new Uint8Array([137, 80, 78, 71, 13, 10, 26, 10]), {
      status: 200, headers: { 'content-type': 'image/png' },
    });
    const withPicture = { ...profile('Ada'), picture: { contentType: 'image/png', updatedAt: '2026-11-21T12:00:00Z', version: 1 } };
    const chooser = () => screen.getByLabelText('Profile picture', { selector: 'input' }) as HTMLInputElement;
    const file = (type: string, bytes = 8) => new File([new Uint8Array(bytes)], 'me.png', { type });

    it('renders the stored picture from bytes fetched with the token', async () => {
      const urls = objectUrls();
      const fetcher = routeFetch({
        'GET /api/users/me/picture': png(),
        'GET /api/users/me': profile('Ada'),
      });
      await mount();

      const image = await screen.findByRole('img', { name: 'Your profile picture' });
      expect(image.getAttribute('src')).toMatch(/^blob:preview-/);
      expect(urls.create).toHaveBeenCalledOnce();
      expect(requested(fetcher)).toContain('GET /api/users/me/picture');
    });

    it('invites the player to choose one when there is no picture', async () => {
      routeFetch({ 'GET /api/users/me/picture': problem(404), 'GET /api/users/me': profile('Ada') });
      await mount();

      expect(await screen.findByText('No picture yet. Choose a PNG, JPEG or WebP image up to 2 MB.')).toBeVisible();
    });

    it('rejects a wrong type before sending anything', async () => {
      const fetcher = routeFetch({ 'GET /api/users/me/picture': problem(404), 'GET /api/users/me': profile('Ada') });
      await mount();
      await screen.findByText('Ada');

      fireEvent.change(chooser(), { target: { files: [file('image/gif')] } });

      expect(await screen.findByText('Choose a PNG, JPEG or WebP image.')).toBeVisible();
      expect(requested(fetcher).filter((call) => call.startsWith('PUT'))).toEqual([]);
    });

    it('rejects a file over the 2 MB contract limit before sending anything', async () => {
      const fetcher = routeFetch({ 'GET /api/users/me/picture': problem(404), 'GET /api/users/me': profile('Ada') });
      await mount();
      await screen.findByText('Ada');

      const huge = new File([new Uint8Array(2_097_153)], 'big.png', { type: 'image/png' });
      fireEvent.change(chooser(), { target: { files: [huge] } });

      expect(await screen.findByText('Use an image of at most 2 MB.')).toBeVisible();
      expect(requested(fetcher).filter((call) => call.startsWith('PUT'))).toEqual([]);
    });

    it('uploads the raw file and then shows the new picture', async () => {
      objectUrls();
      const fetcher = routeFetch({
        'GET /api/users/me/picture': inOrder(problem(404), png()),
        'PUT /api/users/me/picture': withPicture,
        'GET /api/users/me': profile('Ada'),
      });
      await mount();
      await screen.findByText('Ada');

      const chosen = file('image/png');
      fireEvent.change(chooser(), { target: { files: [chosen] } });

      expect(await screen.findByText('Picture saved.')).toBeVisible();
      expect(await screen.findByRole('img', { name: 'Your profile picture' })).toBeVisible();
      const put = fetcher.mock.calls.find(([input, init]) => init?.method === 'PUT' && String(input).endsWith('/api/users/me/picture'));
      expect(put?.[1]?.body).toBe(chosen);
      expect(new Headers(put?.[1]?.headers).get('Content-Type')).toBe('image/png');
    });

    it('shows the permission message when the upload is refused', async () => {
      routeFetch({
        'GET /api/users/me/picture': problem(404),
        'PUT /api/users/me/picture': problem(403),
        'GET /api/users/me': profile('Ada'),
      });
      await mount();
      await screen.findByText('Ada');

      fireEvent.change(chooser(), { target: { files: [file('image/png')] } });

      expect(await screen.findByText('You do not have permission to do this.')).toBeVisible();
    });

    it('drops the picture and its bytes when the player signs out', async () => {
      const urls = objectUrls();
      routeFetch({ 'GET /api/users/me/picture': png(), 'GET /api/users/me': profile('Ada') });
      const { navigate } = await mount();
      await screen.findByRole('img', { name: 'Your profile picture' });

      fireEvent.click(screen.getByRole('button', { name: 'Sign out' }));

      expect(navigate).toHaveBeenCalledWith('https://provider.example/logout');
      expect(urls.revoke).toHaveBeenCalledWith(urls.create.mock.results[0].value);
      await waitFor(() => expect(screen.queryByRole('img', { name: 'Your profile picture' })).toBeNull());
    });

    it('revokes the object URL when the page goes away, so sign-out leaves no image handle', async () => {
      const urls = objectUrls();
      routeFetch({ 'GET /api/users/me/picture': png(), 'GET /api/users/me': profile('Ada') });
      const { unmount } = await mount();
      await screen.findByRole('img', { name: 'Your profile picture' });

      unmount();

      expect(urls.revoke).toHaveBeenCalledWith(urls.create.mock.results[0].value);
    });
  });
});