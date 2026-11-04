import { useCallback, useEffect, useMemo, useRef, useState, type FormEvent } from 'react';
import { createClients } from '../../api/clients';
import { ApiError } from '../../api/httpClient';
import { createIdentityApi, displayNameError, maxDisplayNameLength, type Profile } from '../../api/identity';
import { Session } from '../../auth/session';
import { RequestState } from '../../components/RequestState';
import { SignedInOnly } from '../../components/SignedInOnly';
import { useLoad } from '../../components/useLoad';
import { ValidatedField } from '../../components/ValidatedField';
import { readConfig } from '../../config';
import './Profile.css';

export function ProfilePage() {
  return <section className="destination">
    <p className="eyebrow muted">Your corner</p>
    <h1>Profile</h1>
    <p className="page-description">A name to remember. Sign-in is handled by the QuizArena identity provider; there is no QuizArena password.</p>
    <SignedInOnly>{(session, subject) => <PlayerProfile key={subject} session={session} subject={subject} />}</SignedInOnly>
  </section>;
}

function isAbort(error: unknown) { return error instanceof DOMException && error.name === 'AbortError'; }

function PlayerProfile({ session, subject }: { session: Session; subject: string }) {
  const api = useMemo(() => createIdentityApi(createClients(readConfig(import.meta.env), session)), [session]);
  const profile = useLoad(useCallback((signal: AbortSignal) => api.me(subject, signal), [api, subject]));
  const [saved, setSaved] = useState<Profile>();

  if (profile.status === 'loading') return <RequestState state="loading" title="Loading your profile…" />;
  if (profile.status === 'error') {
    const retry = profile.error instanceof ApiError && ['unavailable', 'network', 'unexpected'].includes(profile.error.kind);
    return <RequestState state="error" error={profile.error} onRetry={retry ? profile.reload : undefined} />;
  }

  const current = saved ?? profile.value;
  return <div className="profile-card">
    {current ? <p>Signed in as <strong>{current.displayName}</strong>.</p>
      : <p>You have not chosen a display name yet. Save one to create your profile.</p>}
    <DisplayNameForm initial={current?.displayName ?? session.getSnapshot().displayName ?? ''}
      save={(name, signal) => api.save(subject, name, signal)} onSaved={setSaved} />
    <button className="button secondary" onClick={() => session.logout()}>Sign out</button>
  </div>;
}

function DisplayNameForm({ initial, save, onSaved }: {
  initial: string;
  save: (name: string, signal: AbortSignal) => Promise<Profile>;
  onSaved: (profile: Profile) => void;
}) {
  const [name, setName] = useState(initial);
  const [fieldError, setFieldError] = useState<string>();
  const [error, setError] = useState<unknown>();
  const [busy, setBusy] = useState(false);
  const [note, setNote] = useState('');
  const pending = useRef<AbortController | undefined>(undefined);
  useEffect(() => () => pending.current?.abort(), []);

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (pending.current) return;
    const problem = displayNameError(name);
    if (problem) {
      setFieldError(problem);
      document.getElementById('profile-display-name')?.focus();
      return;
    }
    const controller = new AbortController();
    pending.current = controller;
    setBusy(true); setFieldError(undefined); setError(undefined); setNote('');
    try {
      const stored = await save(name, controller.signal);
      setName(stored.displayName);
      setNote('Display name saved.');
      onSaved(stored);
    } catch (failure) {
      if (isAbort(failure)) return;
      if (failure instanceof ApiError && failure.kind === 'validation') {
        setFieldError(failure.fields.displayName?.[0] ?? failure.fields.DisplayName?.[0] ?? 'Check the display name and try again.');
      } else {
        setError(failure);
      }
    } finally {
      pending.current = undefined;
      setBusy(false);
    }
  }

  return <form onSubmit={(event) => { void submit(event); }} noValidate>
    <ValidatedField id="profile-display-name" label="Display name" help={`Shown to other players. Up to ${maxDisplayNameLength} characters.`}
      value={name} onChange={(event) => { setName(event.target.value); setFieldError(undefined); setNote(''); }}
      error={fieldError} autoComplete="nickname" maxLength={maxDisplayNameLength + 20} disabled={busy} />
    <button className="button primary" type="submit" disabled={busy}>{busy ? 'Saving…' : 'Save display name'}</button>
    <p role="status">{note}</p>
    {error !== undefined && <RequestState state="error" error={error} />}
  </form>;
}
