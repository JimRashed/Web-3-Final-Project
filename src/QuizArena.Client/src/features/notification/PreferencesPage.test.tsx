import { fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AuthProvider } from '../../auth/AuthProvider';
import { sessionDouble } from '../../test/doubles';
import { PreferencesPage } from './PreferencesPage';

afterEach(() => { vi.unstubAllGlobals(); });

async function mount(...responses: Array<unknown | Response>) {
  const fetcher = vi.fn<typeof fetch>();
  responses.forEach((value) => fetcher.mockResolvedValueOnce(value instanceof Response ? value : Response.json(value)));
  vi.stubGlobal('fetch', fetcher);
  const { session } = await sessionDouble(true);
  render(<AuthProvider session={session}><MemoryRouter><PreferencesPage /></MemoryRouter></AuthProvider>);
  return fetcher;
}

describe('Preferences page', () => {
  it('loads the stored flags for the signed-in subject', async () => {
    const fetcher = await mount({ emailEnabled: false, pushEnabled: true });

    const email = await screen.findByLabelText('Email updates') as HTMLInputElement;
    const push = screen.getByLabelText('Push notifications') as HTMLInputElement;
    expect(email.checked).toBe(false);
    expect(push.checked).toBe(true);
    expect(String(fetcher.mock.calls[0][0])).toContain('/users/player-a/preferences');
  });

  it('saves the changed flags and confirms', async () => {
    const fetcher = await mount(
      { emailEnabled: false, pushEnabled: false },
      { emailEnabled: true, pushEnabled: true },
    );

    fireEvent.click(await screen.findByLabelText('Email updates'));
    fireEvent.click(screen.getByRole('button', { name: 'Save preferences' }));

    expect(await screen.findByText('Preferences saved.')).toBeVisible();
    expect(fetcher.mock.calls[1][1]).toMatchObject({ method: 'PUT' });
  });

  it('reports a failed save without losing the form', async () => {
    await mount(
      { emailEnabled: false, pushEnabled: false },
      Response.json({ title: 'failed' }, { status: 500 }),
    );

    fireEvent.click(await screen.findByLabelText('Push notifications'));
    fireEvent.click(screen.getByRole('button', { name: 'Save preferences' }));

    expect(await screen.findByRole('status')).toBeVisible();
    expect(screen.getByLabelText('Push notifications')).toBeVisible();
  });
});
