import { useEffect, useMemo, useState } from 'react';
import { createClients } from '../../api/clients';
import { createNotificationApi, type NotificationPreferences } from '../../api/notification';
import { useSession } from '../../auth/AuthProvider';
import { Session, SessionRequiredError } from '../../auth/session';
import { RequestState } from '../../components/RequestState';
import { readConfig } from '../../config';

export function PreferencesPage() {
  const { session, status, subject } = useSession();
  return <section className="destination">
    <p className="eyebrow muted">Your corner</p>
    <h1>Preferences</h1>
    <p className="page-description">Choose how the arena reaches you.</p>
    {status === 'authenticated' && subject ? <SessionPreferences session={session} subject={subject} />
      : status === 'loading' ? <RequestState state="loading" title="Connecting to your session…" />
        : <RequestState state="error" error={new SessionRequiredError()} onSignIn={status === 'unavailable' ? undefined : () => { void session.login(); }} />}
  </section>;
}

function SessionPreferences({ session, subject }: { session: Session; subject: string }) {
  const api = useMemo(() => createNotificationApi(createClients(readConfig(import.meta.env), session)), [session]);
  const [preferences, setPreferences] = useState<NotificationPreferences>();
  const [error, setError] = useState<unknown>();
  const [note, setNote] = useState('');
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    const controller = new AbortController();
    api.getPreferences(subject, controller.signal)
      .then((loaded) => { if (!controller.signal.aborted) setPreferences(loaded); })
      .catch((failure: unknown) => {
        if (!controller.signal.aborted && !(failure instanceof DOMException && failure.name === 'AbortError')) setError(failure);
      });
    return () => controller.abort();
  }, [api, subject]);

  if (error) return <RequestState state="error" error={error} />;
  if (!preferences) return <RequestState state="loading" />;

  async function save(next: NotificationPreferences) {
    setBusy(true);
    setNote('');
    try {
      setPreferences(await api.savePreferences(subject, next, session.signal));
      setNote('Preferences saved.');
    } catch (failure) {
      setNote(failure instanceof Error ? failure.message : 'Preferences could not be saved.');
    } finally {
      setBusy(false);
    }
  }

  return <form className="preferences" onSubmit={(event) => { event.preventDefault(); void save(preferences); }}>
    <label><input type="checkbox" checked={preferences.emailEnabled}
      onChange={(event) => setPreferences({ ...preferences, emailEnabled: event.target.checked })} /> Email updates</label>
    <label><input type="checkbox" checked={preferences.pushEnabled}
      onChange={(event) => setPreferences({ ...preferences, pushEnabled: event.target.checked })} /> Push notifications</label>
    <button className="button primary" type="submit" disabled={busy}>{busy ? 'Saving…' : 'Save preferences'}</button>
    <p role="status">{note}</p>
  </form>;
}
