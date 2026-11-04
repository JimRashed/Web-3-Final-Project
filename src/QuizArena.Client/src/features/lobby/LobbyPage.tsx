import { useCallback, useEffect, useMemo, useRef, useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { createClients } from '../../api/clients';
import { ApiError } from '../../api/httpClient';
import { createLobbyApi, isJoinCode, type LobbyResponse } from '../../api/lobby';
import { useSession } from '../../auth/AuthProvider';
import { Session, SessionRequiredError } from '../../auth/session';
import { RequestState } from '../../components/RequestState';
import { ValidatedField } from '../../components/ValidatedField';
import { readConfig } from '../../config';
import './LobbyPage.css';

type Operation = 'create' | 'join' | 'lock' | 'start' | 'get';
const sessionKeys = new WeakMap<Session, number>();
let nextSessionKey = 0;
function keyFor(session: Session): number {
  if (!sessionKeys.has(session)) sessionKeys.set(session, ++nextSessionKey);
  return sessionKeys.get(session)!;
}
function isAbort(error: unknown) { return error instanceof DOMException && error.name === 'AbortError'; }

export function LobbyPage() {
  const { session, status } = useSession();
  return <section className="destination lobby-page"><p className="eyebrow muted">Play together</p><h1>Lobby</h1>
    {status === 'authenticated' ? <SessionLobby key={keyFor(session)} session={session} />
      : status === 'loading' ? <RequestState state="loading" title="Connecting to your session…" />
        : status === 'unavailable' ? <RequestState state="error" title="Sign-in unavailable"><p>Reload when sign-in is available again.</p></RequestState>
          : <RequestState state="error" error={new SessionRequiredError()} onSignIn={() => { void session.login(); }} />}
  </section>;
}

function SessionLobby({ session }: { session: Session }) {
  const api = useMemo(() => createLobbyApi(createClients(readConfig(import.meta.env), session)), [session]);
  const navigate = useNavigate();
  const [lobby, setLobby] = useState<LobbyResponse>();
  const [code, setCode] = useState('');
  const [fieldError, setFieldError] = useState<string>();
  const [error, setError] = useState<unknown>();
  const [busy, setBusy] = useState<Operation>();
  const [pollPaused, setPollPaused] = useState(false);
  const [copyNote, setCopyNote] = useState('');
  const [retry, setRetry] = useState<{ operation: Operation; code: string }>();
  const alive = useRef(false);
  const generation = useRef(0);
  const guard = useRef(false);
  const timer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);
  const reading = useRef<AbortController | undefined>(undefined);
  const writing = useRef<AbortController | undefined>(undefined);

  const current = useCallback((id: number) => alive.current && generation.current === id && session.getSnapshot().status === 'authenticated', [session]);
  const stopPolling = useCallback(() => {
    clearTimeout(timer.current);
    reading.current?.abort();
    reading.current = undefined;
  }, []);
  const invalidateAll = useCallback(() => {
    generation.current++;
    stopPolling();
    writing.current?.abort();
  }, [stopPolling]);

  useEffect(() => {
    alive.current = true;
    return () => {
      alive.current = false;
      invalidateAll();
    };
  }, [invalidateAll]);

  const selectedCode = lobby?.joinCode;
  useEffect(() => {
    if (!selectedCode || busy || pollPaused) return;
    const id = generation.current;
    let stopped = false;
    const schedule = () => { if (!stopped) timer.current = setTimeout(() => { void refresh(); }, 2000); };
    const refresh = async () => {
      if (stopped || !current(id)) return;
      if (document.hidden) { schedule(); return; }
      const controller = new AbortController();
      reading.current = controller;
      try {
        const result = await api.get(selectedCode, controller.signal);
        if (!stopped && current(id)) { setLobby(result); schedule(); }
      } catch (failure) {
        if (stopped || !current(id) || isAbort(failure)) return;
        if (failure instanceof ApiError && failure.kind === 'unauthorized') { session.invalidate(); return; }
        setError(failure); setPollPaused(true); setRetry({ operation: 'get', code: selectedCode });
      }
    };
    schedule();
    return () => { stopped = true; stopPolling(); };
  }, [api, selectedCode, busy, pollPaused, current, session, stopPolling]);

  async function run(operation: Operation, selectedCode = '') {
    if (guard.current) return;
    guard.current = true;
    stopPolling();
    const id = ++generation.current;
    const controller = new AbortController();
    writing.current = controller;
    setBusy(operation); setError(undefined); setRetry(undefined); setCopyNote(''); setPollPaused(false);
    try {
      if (operation === 'start') {
        const result = await api.startMatch(selectedCode, controller.signal);
        if (current(id)) navigate(`/matches?matchId=${encodeURIComponent(result.matchId)}`);
      } else {
        const result = operation === 'create' ? await api.create(controller.signal)
          : await api[operation](selectedCode, controller.signal);
        if (current(id)) setLobby(result);
      }
    } catch (failure) {
      if (!current(id) || isAbort(failure)) return;
      if (failure instanceof SessionRequiredError || failure instanceof ApiError && failure.kind === 'unauthorized') { session.invalidate(); return; }
      setError(failure); setPollPaused(true); setRetry({ operation, code: selectedCode });
      if (failure instanceof ApiError && failure.kind === 'validation' && operation === 'join') setFieldError('Enter exactly six uppercase letters or digits.');
      if (failure instanceof ApiError && failure.kind === 'conflict' && lobby) {
        try {
          const latest = await api.get(lobby.joinCode, controller.signal);
          if (current(id)) setLobby(latest);
        } catch (refreshFailure) {
          if (current(id) && !isAbort(refreshFailure)) {
            if (refreshFailure instanceof ApiError && refreshFailure.kind === 'unauthorized') session.invalidate();
            else setError(refreshFailure);
          }
        }
      }
    } finally {
      if (writing.current === controller) { guard.current = false; writing.current = undefined; }
      if (current(id)) setBusy(undefined);
    }
  }

  function join(event: FormEvent) {
    event.preventDefault();
    if (guard.current) return;
    if (!isJoinCode(code)) {
      setFieldError('Enter exactly six uppercase letters or digits.');
      document.getElementById('lobby-join-code')?.focus();
      return;
    }
    setFieldError(undefined);
    void run('join', code);
  }
  async function copyCode() {
    if (!lobby) return;
    const id = generation.current;
    try {
      if (!navigator.clipboard?.writeText) throw new Error('Clipboard unavailable');
      await navigator.clipboard.writeText(lobby.joinCode);
      if (current(id)) setCopyNote('Join code copied.');
    } catch { if (current(id)) setCopyNote('Copy is unavailable. Select the join code and copy it manually.'); }
  }
  const retryable = error instanceof ApiError && ['unavailable', 'network', 'unexpected'].includes(error.kind);
  return <>
    <p className="page-description">Create a two-player lobby or join a friend with their code.</p>
    <div className="lobby-entry">
      <div><h2>Create a lobby</h2><p>You will be its host.</p><button className="button primary" disabled={!!busy} onClick={() => { void run('create'); }}>Create lobby</button></div>
      <form onSubmit={join} noValidate><h2>Join a lobby</h2><ValidatedField id="lobby-join-code" label="Join code" help="Six uppercase letters or digits; codes are case-sensitive." value={code} onChange={(event) => { setCode(event.target.value); setFieldError(undefined); }} error={fieldError} autoComplete="off" disabled={!!busy} /><button className="button secondary" disabled={!!busy} type="submit">Join lobby</button></form>
    </div>
    {busy && <p role="status">{busy === 'get' ? 'Refreshing lobby…' : 'Submitting…'}</p>}
    {error !== undefined && <RequestState state="error" error={error} onRetry={retryable && retry ? () => { void run(retry.operation, retry.code); } : undefined} />}
    {lobby ? <section className="lobby-room" aria-label="Selected lobby"><h2>Your lobby</h2><p>Join code: <strong className="lobby-code">{lobby.joinCode}</strong></p><button className="button secondary" onClick={() => { void copyCode(); }}>Copy code</button><p role="status">{copyNote}</p>
      <dl><div><dt>State</dt><dd>{lobby.status}</dd></div><div><dt>Players</dt><dd>{lobby.memberCount} of 2</dd></div><div><dt>Your role</dt><dd>{lobby.isHost ? 'You are the host.' : lobby.isMember ? 'You are a member.' : 'You are viewing this lobby as a nonmember.'}</dd></div></dl>
      {lobby.memberCount === 1 && <p>Waiting for a second player. Share the join code.</p>}
      {!lobby.isHost && <p>Only the host can lock this lobby and start a match.</p>}
      <div className="lobby-actions"><button className="button secondary" disabled={!!busy || !lobby.isHost || lobby.status !== 'Open' || lobby.memberCount !== 2} onClick={() => { void run('lock', lobby.joinCode); }}>Lock lobby</button><button className="button primary" disabled={!!busy || !lobby.isHost || lobby.status !== 'Locked' || lobby.memberCount !== 2} onClick={() => { void run('start', lobby.joinCode); }}>Start match</button></div>
      {lobby.isHost && lobby.status === 'Open' && <p>Lock the lobby after both players have joined, then start a match.</p>}
      {!lobby.isHost && lobby.status === 'Locked' && <p>Waiting for the host to start. Delivery may be delayed; <Link to="/notifications">check your notifications</Link> for an Open match action, or use a match link shared by the host.</p>}
    </section> : <RequestState state="empty" title="No lobby selected"><p>Create a lobby or enter a join code to begin.</p></RequestState>}
  </>;
}
