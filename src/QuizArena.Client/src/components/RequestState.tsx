import type { ReactNode } from 'react';
import { ApiError } from '../api/httpClient';
import { SessionRequiredError } from '../auth/session';

type Props = {
  state: 'loading' | 'empty' | 'error'; title?: string; children?: ReactNode;
  error?: unknown; onRetry?: () => void; onSignIn?: () => void;
};
export function RequestState({ state, title, children, error, onRetry, onSignIn }: Props) {
  if (state === 'loading') return <div className="request-state" role="status"><span className="spinner" aria-hidden="true" /><p>{title ?? 'Loading…'}</p></div>;
  const unauthorized = error instanceof SessionRequiredError || (error instanceof ApiError && error.kind === 'unauthorized');
  const forbidden = error instanceof ApiError && error.kind === 'forbidden';
  const missing = error instanceof ApiError && error.kind === 'not-found';
  const heading = title ?? (unauthorized ? 'Sign in to continue' : forbidden ? 'Access unavailable' : missing ? 'Item not found' : state === 'empty' ? 'Nothing here yet' : 'Unable to load');
  const message = error instanceof ApiError || error instanceof SessionRequiredError ? error.message : state === 'error' ? 'Something went wrong. Please try again.' : undefined;
  return <div className="request-state" role={state === 'error' ? 'alert' : 'status'}>
    <span className="state-mark" aria-hidden="true">{state === 'empty' ? '◇' : '!'}</span>
    <h2>{heading}</h2>{children ?? (message && <p>{message}</p>)}
    {unauthorized && onSignIn ? <button className="button primary" onClick={onSignIn}>Sign in</button>
      : !forbidden && onRetry ? <button className="button secondary" onClick={onRetry}>Try again</button> : null}
  </div>;
}
