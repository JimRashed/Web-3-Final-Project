import { act, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it } from 'vitest';
import { AuthProvider } from '../auth/AuthProvider';
import { sessionDouble } from '../test/doubles';
import { App } from './App';
import { destinations } from './routes';

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
});
