import type { ReactNode } from 'react';
import { useSession } from '../auth/AuthProvider';
import { Session, SessionRequiredError } from '../auth/session';
import { RequestState } from './RequestState';

/** Renders its children only for a signed-in session with a token subject; otherwise the matching session state. */
export function SignedInOnly({ children }: { children: (session: Session, subject: string) => ReactNode }) {
  const { session, status, subject } = useSession();
  if (status === 'authenticated' && subject) return <>{children(session, subject)}</>;
  if (status === 'loading') return <RequestState state="loading" title="Connecting to your session…" />;
  if (status === 'unavailable') return <RequestState state="error" title="Sign-in unavailable"><p>Reload when sign-in is available again.</p></RequestState>;
  return <RequestState state="error" error={new SessionRequiredError()} onSignIn={() => { void session.login(); }} />;
}
