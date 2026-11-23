import { useCallback, useEffect, useMemo, useRef, useState, type ChangeEvent, type FormEvent } from 'react';
import { createClients } from '../../api/clients';
import { ApiError } from '../../api/httpClient';
import {
  createIdentityApi, displayNameError, maxDisplayNameLength, pictureFileError, pictureTypes, type Profile,
} from '../../api/identity';
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
function retryable(error: unknown) { return error instanceof ApiError && ['unavailable', 'network', 'unexpected'].includes(error.kind); }

function PlayerProfile({ session, subject }: { session: Session; subject: string }) {
  const api = useMemo(() => createIdentityApi(createClients(readConfig(import.meta.env), session)), [session]);
  const profile = useLoad(useCallback((signal: AbortSignal) => api.me(subject, signal), [api, subject]));
  const [saved, setSaved] = useState<Profile>();

  if (profile.status === 'loading') return <RequestState state="loading" title="Loading your profile…" />;
  if (profile.status === 'error') {
    return <RequestState state="error" error={profile.error} onRetry={retryable(profile.error) ? profile.reload : undefined} />;
  }

  const current = saved ?? profile.value;
  return <div className="profile-card">
    {current ? <p>Signed in as <strong>{current.displayName}</strong>.</p>
      : <p>You have not chosen a display name yet. Save one to create your profile.</p>}
    <ProfilePicture api={api} subject={subject} session={session} />
    <DisplayNameForm initial={current?.displayName ?? session.getSnapshot().displayName ?? ''}
      save={(name, signal) => api.save(subject, name, signal)} onSaved={setSaved} />
    <button className="button secondary" onClick={() => session.logout()}>Sign out</button>
  </div>;
}

type PictureState = 'loading' | 'ready' | 'empty' | 'error';

/**
 * The player's own picture. The bytes are fetched with the bearer token and rendered from a
 * short-lived object URL held in memory, so nothing cacheable or shareable is left on the device;
 * the URL is revoked when the picture is replaced and when this component goes away (sign-out).
 */
function ProfilePicture({ api, subject, session }: {
  api: ReturnType<typeof createIdentityApi>; subject: string; session: Session;
}) {
  const [state, setState] = useState<PictureState>('loading');
  const [url, setUrl] = useState<string>();
  const [error, setError] = useState<unknown>();
  const [note, setNote] = useState('');
  const [busy, setBusy] = useState(false);
  const [attempt, setAttempt] = useState(0);
  const objectUrl = useRef<string | undefined>(undefined);

  const release = useCallback(() => {
    if (objectUrl.current) {
      URL.revokeObjectURL(objectUrl.current);
      objectUrl.current = undefined;
    }
  }, []);

  // Sign-out unmounts this component: drop the local handle to the image bytes.
  useEffect(() => release, [release]);

  useEffect(() => {
    const controller = new AbortController();
    void (async () => {
      try {
        const bytes = await api.picture(controller.signal);
        if (controller.signal.aborted) return;
        release();
        if (!bytes) {
          setUrl(undefined);
          setState('empty');
          setError(undefined);
          return;
        }
        objectUrl.current = URL.createObjectURL(bytes);
        setUrl(objectUrl.current);
        setState('ready');
        setError(undefined);
      } catch (failure) {
        if (controller.signal.aborted || isAbort(failure)) return;
        setError(failure);
        setState('error');
      }
    })();
    return () => controller.abort();
  }, [api, attempt, release]);

  async function choose(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    event.target.value = '';
    if (!file || busy) return;

    const problem = pictureFileError(file);
    if (problem) {
      setNote(problem);
      return;
    }

    setNote('');
    setError(undefined);
    setBusy(true);
    try {
      await api.replacePicture(subject, file, session.signal);
      setNote('Picture saved.');
      setAttempt((value) => value + 1);
    } catch (failure) {
      if (isAbort(failure)) return;
      setError(failure);
      setState('error');
    } finally {
      setBusy(false);
    }
  }

  return <section className="profile-picture" aria-labelledby="profile-picture-heading">
    <h2 id="profile-picture-heading">Profile picture</h2>
    {state === 'loading' && <p className="muted" role="status">Loading your picture…</p>}
    {state === 'ready' && url && <img className="profile-picture-image" src={url} alt="Your profile picture" />}
    {state === 'empty' && <p className="muted">No picture yet. Choose a PNG, JPEG or WebP image up to 2 MB.</p>}
    {state === 'error' && error !== undefined &&
      <RequestState state="error" error={error} onRetry={retryable(error) ? () => setAttempt((value) => value + 1) : undefined} />}
    <label className="profile-picture-upload">
      <span>{busy ? 'Uploading…' : state === 'ready' ? 'Replace picture' : 'Choose picture'}</span>
      <input type="file" accept={pictureTypes.join(',')} disabled={busy} aria-label="Profile picture"
        onChange={(event) => { void choose(event); }} />
    </label>
    <p role="status">{note}</p>
  </section>;
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
