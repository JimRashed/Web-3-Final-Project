import { act, render, screen } from '@testing-library/react';
import { expect, it } from 'vitest';
import { sessionDouble } from '../test/doubles';
import { AuthProvider, useSession } from './AuthProvider';

function Consumer() { const { status, displayName } = useSession(); return <p>{status} {displayName}</p>; }
it('updates consumers with user-free state when the session is cleared', async () => {
  const { session } = await sessionDouble();
  render(<AuthProvider session={session}><Consumer /></AuthProvider>);
  expect(screen.getByText('authenticated CuriousPlayer')).toBeVisible();
  act(() => session.invalidate());
  expect(screen.getByText('expired')).toBeVisible();
  expect(screen.queryByText(/CuriousPlayer/)).not.toBeInTheDocument();
});
