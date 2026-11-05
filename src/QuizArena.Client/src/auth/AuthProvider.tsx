import { createContext, useContext, useSyncExternalStore, type ReactNode } from 'react';
import { Session } from './session';

const SessionContext = createContext<Session | null>(null);
export function AuthProvider({ session, children }: { session: Session; children: ReactNode }) {
  return <SessionContext.Provider value={session}>{children}</SessionContext.Provider>;
}
export function useSession() {
  const session = useContext(SessionContext);
  if (!session) throw new Error('AuthProvider is required.');
  const snapshot = useSyncExternalStore(session.subscribe, session.getSnapshot);
  return { session, ...snapshot };
}
