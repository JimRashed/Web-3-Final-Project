import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useParams, useSearchParams } from 'react-router-dom';
import { createClients } from '../../api/clients';
import { createGameplayApi, type AnswerOutcome, type CurrentQuestion, type MatchView } from '../../api/gameplay';
import { ApiError } from '../../api/httpClient';
import { useSession } from '../../auth/AuthProvider';
import { Session, SessionRequiredError } from '../../auth/session';
import { RequestState } from '../../components/RequestState';
import { readConfig } from '../../config';

/** How often a player who has answered checks whether the opponent has answered too. */
export const waitingPollMs = 2000;

function isAbort(error: unknown) { return error instanceof DOMException && error.name === 'AbortError'; }
function retryable(error: unknown) { return error instanceof ApiError && ['unavailable', 'network', 'unexpected'].includes(error.kind); }

/** Serves both /matches?matchId=… (the link Lobby navigates to and players share) and /matches/:matchId. */
export function MatchPage() {
  const params = useParams();
  const [search] = useSearchParams();
  const matchId = params.matchId ?? search.get('matchId') ?? undefined;
  const { session, status, subject } = useSession();

  return <section className="destination">
    <p className="eyebrow muted">One question at a time</p>
    <h1>Match</h1>
    {!matchId ? <RequestState state="empty" title="No match selected"><p>Start a match from the lobby or open a match link.</p></RequestState>
      : status === 'authenticated' && subject ? <>
        <MatchLink matchId={matchId} />
        <SessionMatch key={matchId} session={session} subject={subject} matchId={matchId} />
      </>
        : status === 'loading' ? <RequestState state="loading" title="Connecting to your session…" />
          : <RequestState state="error" error={new SessionRequiredError()} onSignIn={status === 'unavailable' ? undefined : () => { void session.login(); }} />}
  </section>;
}

function MatchLink({ matchId }: { matchId: string }) {
  const [note, setNote] = useState('');
  const url = new URL('/matches', window.location.origin);
  url.searchParams.set('matchId', matchId);
  const link = url.href;
  return <div className="match-link">
    <p>Match ID: {matchId}</p>
    <p><a href={link}>{link}</a></p>
    <button className="button secondary" onClick={() => {
      if (!navigator.clipboard?.writeText) { setNote('Select the link and copy it manually.'); return; }
      void navigator.clipboard.writeText(link).then(() => setNote('Match link copied.'), () => setNote('Select the link and copy it manually.'));
    }}>Copy match link</button>
    <p role="status">{note}</p>
  </div>;
}

function SessionMatch({ session, subject, matchId }: { session: Session; subject: string; matchId: string }) {
  const api = useMemo(() => createGameplayApi(createClients(readConfig(import.meta.env), session)), [session]);
  const [match, setMatch] = useState<MatchView>();
  const [question, setQuestion] = useState<CurrentQuestion>();
  const [answered, setAnswered] = useState<string>();
  const [outcome, setOutcome] = useState<AnswerOutcome>();
  const [loadError, setLoadError] = useState<unknown>();
  const [answerError, setAnswerError] = useState<unknown>();
  const [busy, setBusy] = useState(false);
  const [attempt, setAttempt] = useState(0);
  const alive = useRef(true);
  useEffect(() => {
    alive.current = true;
    return () => { alive.current = false; };
  }, []);

  // Loads the match and, while it is active, the current question. Reading never advances the match.
  const fetchState = useCallback(async (signal: AbortSignal) => {
    const loaded = await api.getMatch(matchId, signal);
    const next = loaded.status === 'ACTIVE' ? await api.currentQuestion(matchId, signal) : undefined;
    return { loaded, next };
  }, [api, matchId]);

  const refresh = useCallback(async (signal: AbortSignal) => {
    try {
      const { loaded, next } = await fetchState(signal);
      if (signal.aborted || !alive.current) return;
      setMatch(loaded);
      setQuestion(next);
      setLoadError(undefined);
    } catch (failure) {
      if (!signal.aborted && alive.current && !isAbort(failure)) setLoadError(failure);
    }
  }, [fetchState]);

  useEffect(() => {
    const controller = new AbortController();
    void (async () => {
      try {
        const { loaded, next } = await fetchState(controller.signal);
        if (controller.signal.aborted) return;
        setMatch(loaded);
        setQuestion(next);
        setLoadError(undefined);
      } catch (failure) {
        if (!controller.signal.aborted && !isAbort(failure)) setLoadError(failure);
      }
    })();
    return () => controller.abort();
  }, [fetchState, attempt]);

  // After answering, wait for the opponent: poll until the round moves on or the match finishes.
  const waiting = !!answered && !!question && question.questionId === answered && match?.status === 'ACTIVE';
  useEffect(() => {
    if (!waiting) return;
    const controller = new AbortController();
    const timer = setInterval(() => {
      if (!document.hidden) void refresh(controller.signal);
    }, waitingPollMs);
    return () => { clearInterval(timer); controller.abort(); };
  }, [waiting, refresh]);

  async function submit(answer: string) {
    if (!question || busy) return;
    setBusy(true);
    setAnswerError(undefined);
    try {
      const result = await api.answer(matchId, question.questionId, answer, session.signal);
      if (!alive.current) return;
      setOutcome(result);
      setAnswered(question.questionId);
      await refresh(session.signal);
    } catch (failure) {
      if (!alive.current || isAbort(failure)) return;
      if (failure instanceof ApiError && failure.kind === 'conflict') {
        // Already answered, or the round moved on: show the current state instead of an error page.
        setAnswered(question.questionId);
        await refresh(session.signal);
      } else {
        setAnswerError(failure);
      }
    } finally {
      if (alive.current) setBusy(false);
    }
  }

  if (loadError !== undefined && !match) {
    return <RequestState state="error" error={loadError} onRetry={retryable(loadError) ? () => setAttempt((value) => value + 1) : undefined} />;
  }
  if (!match) return <RequestState state="loading" title="Loading the match…" />;

  const isA = match.playerAId === subject;
  const mine = isA ? match.playerAScore : match.playerBScore;
  const theirs = isA ? match.playerBScore : match.playerAScore;

  return <>
    <p className="score-line">You {mine} — {theirs} Opponent</p>
    {loadError !== undefined && <RequestState state="error" error={loadError} onRetry={retryable(loadError) ? () => setAttempt((value) => value + 1) : undefined} />}
    {match.status === 'FINISHED'
      ? <RequestState state="empty" title={match.draw ? 'It’s a draw' : match.winnerUserId === subject ? 'You won' : 'You lost'}>
        <p>Final score: you {mine}, opponent {theirs}.</p>
      </RequestState>
      : !question ? <RequestState state="loading" title="Loading the question…" />
        : <div className="question-panel">
          <p className="question-index">Round {match.currentQuestionIndex + 1} of 5</p>
          <h2>{question.text}</h2>
          {waiting
            ? <p role="status">{outcome?.correct ? 'Correct — one point. ' : outcome ? 'Not this time. ' : ''}Waiting for your opponent…</p>
            : <ul className="answer-options">
              {question.options.map((option, index) => <li key={index}>
                <button className="button secondary" type="button" disabled={busy} onClick={() => { void submit(option); }}>{option}</button>
              </li>)}
            </ul>}
          {answerError !== undefined && <RequestState state="error" title="Your answer was not sent" error={answerError}
            onRetry={retryable(answerError) ? () => setAnswerError(undefined) : undefined} />}
        </div>}
  </>;
}
