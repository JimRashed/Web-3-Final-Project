import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { ApiError } from '../api/httpClient';
import { SessionRequiredError } from '../auth/session';
import { RequestState } from './RequestState';

describe('shared request states', () => {
  it('announces loading and empty states', () => {
    const { rerender } = render(<RequestState state="loading" />);
    expect(screen.getByRole('status')).toHaveTextContent('Loading');
    rerender(<RequestState state="empty" title="No results"><p>Play a round first.</p></RequestState>);
    expect(screen.getByRole('status')).toHaveTextContent('No results');
    expect(screen.getByText('Play a round first.')).toBeVisible();
  });
  it('retries a failed request only on explicit action', async () => {
    const retry = vi.fn();
    render(<RequestState state="error" error={new ApiError('unavailable', 503)} onRetry={retry} />);
    expect(retry).not.toHaveBeenCalled();
    await userEvent.click(screen.getByRole('button', { name: 'Try again' }));
    expect(retry).toHaveBeenCalledOnce();
    expect(screen.getByRole('alert')).toHaveTextContent('temporarily unavailable');
  });
  it.each([new ApiError('unauthorized', 401), new SessionRequiredError()])('offers sign-in rather than a request retry after authentication failure', async (error) => {
    const signIn = vi.fn(); const retry = vi.fn();
    render(<RequestState state="error" error={error} onSignIn={signIn} onRetry={retry} />);
    expect(screen.queryByRole('button', { name: 'Try again' })).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }));
    expect(signIn).toHaveBeenCalledOnce(); expect(retry).not.toHaveBeenCalled();
  });
  it('explains permission denial without offering a retry', () => {
    render(<RequestState state="error" error={new ApiError('forbidden', 403)} onRetry={vi.fn()} />);
    expect(screen.getByRole('alert')).toHaveTextContent('permission');
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });
  it('never renders arbitrary exception messages', () => {
    render(<RequestState state="error" error={new Error('private token details')} />);
    expect(screen.getByRole('alert')).not.toHaveTextContent('private token');
  });
});
