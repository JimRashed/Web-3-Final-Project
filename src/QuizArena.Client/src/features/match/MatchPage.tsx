import { useCallback, useEffect, useMemo, useState } from 'react';
import { useParams } from 'react-router-dom';
import { createClients } from '../../api/clients';
import { createGameplayApi, type AnswerOutcome, type CurrentQuestion, type MatchView } from '../../api/gameplay';
import { useSession } from '../../auth/AuthProvider';
import { Session, SessionRequiredError } from '../../auth/session';
import { RequestState } from '../../components/RequestState';
import { readConfig } from '../../config';

export function MatchPage() {
  const { matchId } = useParams();
  const { session, status } = useSession();

  if (!matchId) return <RequestState state="empty" title="No match selected"><p>Open a match from your events.</p></RequestState>;
  if (status !== 'authenticated') {
    return <RequestState
      state={status === 'loading' ? 'loading' : 'error'}
      error={status === 'loading' ? undefined : new SessionRequiredError()}
      onSignIn={status === 'unavailable' ? undefined : () => { void session.login(); }}
    />;
  }

  return <SessionMatch key={matchId} session={session} matchId={matchId} />;
}

function SessionMatch({ session, matchId }: { session: Session; matchId: string }) {
  const api = useMemo(() => createGameplayApi(createClients(readConfig(import.meta.env), session)), [session]);
  const [match, setMatch] = useState<MatchView>();
  const [question, setQuestion] = useState<CurrentQuestion>();
  const [outcome, setOutcome] = useState<AnswerOutcome>();
  const [error, setError] = useState<unknown>();
  const [busy, setBusy] = useState(false);

  const load = useCallback(async (signal: AbortSignal) => {
    const loaded = await api.getMatch(matchId, signal);
    if (signal.aborted) return;
    setMatch(loaded);
    setQuestion(loaded.status === 'ACTIVE' ? await api.currentQuestion(matchId, signal) : undefined);
  }, [api, matchId]);

  useEffect(() => {
    const controller = new AbortController();
    load(controller.signal).catch((failure: unknown) => {
      if (!controller.signal.aborted) setError(failure);
    });
    return () => controller.abort();
  }, [load]);

  async function submit(answer: string) {
    if (!question) return;
    setBusy(true);
    try {
      const result = await api.answer(matchId, question.questionId, answer, session.signal);
      setOutcome(result);
      await load(session.signal);
    } catch (failure) {
      setError(failure);
    } finally {
      setBusy(false);
    }
  }

  if (error) return <RequestState state="error" error={error} />;
  if (!match) return <RequestState state="loading" title="Loading the match…" />;

  return <section className="destination">
    <p className="eyebrow muted">One question at a time</p>
    <h1>Match</h1>
    <p className="score-line">You {match.playerAScore} — {match.playerBScore} Opponent</p>

    {match.status === 'FINISHED'
      ? <RequestState state="empty" title={match.draw ? 'It’s a draw' : match.winnerUserId ? 'Match finished' : 'Match finished'}>
        <p>{match.draw ? 'Both players scored the same.' : 'The final scores are shown above.'}</p>
      </RequestState>
      : question
        ? <div className="question-panel">
          <p className="question-index">Round {match.currentQuestionIndex + 1} of 5</p>
          <h2>{question.text}</h2>
          <ul className="answer-options">
            {question.options.map((option) => <li key={option}>
              <button className="button secondary" type="button" disabled={busy} onClick={() => { void submit(option); }}>{option}</button>
            </li>)}
          </ul>
          {outcome && <p role="status">{outcome.correct ? 'Correct — one point.' : 'Not this time.'}</p>}
        </div>
        : <RequestState state="loading" title="Waiting for the next question…" />}
  </section>;
}
