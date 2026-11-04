import { useEffect, useMemo, useRef, useState } from 'react';
import { createClients } from '../../api/clients';
import { ApiError } from '../../api/httpClient';
import { createQuestionBankApi, validQuestionCount, type QuestionPreview } from '../../api/questionbank';
import { useSession } from '../../auth/AuthProvider';
import { Session, SessionRequiredError } from '../../auth/session';
import { RequestState } from '../../components/RequestState';
import { ValidatedField } from '../../components/ValidatedField';
import { readConfig } from '../../config';
import './QuestionBankPage.css';

const sessionKeys = new WeakMap<Session, number>();
let nextSessionKey = 0;
function keyFor(session: Session): number {
  if (!sessionKeys.has(session)) sessionKeys.set(session, ++nextSessionKey);
  return sessionKeys.get(session)!;
}
function isAbort(error: unknown): boolean {
  return error instanceof DOMException && error.name === 'AbortError';
}

export function QuestionBankPage() {
  const { session, status } = useSession();
  return <section className="destination questionbank-page">
    <p className="eyebrow muted">Browse the catalog</p><h1>Questions</h1>
    <p className="page-description">Preview questions and their options before you play. Answers stay private.</p>
    {status === 'authenticated' ? <SessionQuestionBank key={keyFor(session)} session={session} />
      : status === 'loading' ? <RequestState state="loading" title="Connecting to your session…" />
        : status === 'unavailable' ? <RequestState state="error" title="Sign-in unavailable"><p>Reload when sign-in is available again.</p></RequestState>
          : <RequestState state="error" error={new SessionRequiredError()} onSignIn={() => { void session.login(); }} />}
  </section>;
}

function SessionQuestionBank({ session }: { session: Session }) {
  const api = useMemo(() => createQuestionBankApi(createClients(readConfig(import.meta.env), session)), [session]);
  const [count, setCount] = useState('10');
  const [fieldError, setFieldError] = useState<string>();
  const [questions, setQuestions] = useState<QuestionPreview[]>();
  const [shownCount, setShownCount] = useState<number>();
  const [error, setError] = useState<unknown>();
  const [busy, setBusy] = useState(false);
  const alive = useRef(false);
  const generation = useRef(0);
  const guard = useRef(false);
  const request = useRef<AbortController | undefined>(undefined);

  useEffect(() => {
    alive.current = true;
    return () => {
      alive.current = false;
      generation.current += 1;
      request.current?.abort();
    };
  }, []);

  function current(id: number): boolean {
    return alive.current && generation.current === id && session.getSnapshot().status === 'authenticated';
  }

  function startRequest(): void {
    if (guard.current) return;
    const parsed = Number(count);
    if (!/^\d+$/.test(count) || !validQuestionCount(parsed)) {
      setFieldError('Choose a whole number from 1 to 50.');
      document.getElementById('question-count')?.focus();
      return;
    }
    guard.current = true;
    setBusy(true);
    setError(undefined);
    setFieldError(undefined);
    setQuestions(undefined);
    setShownCount(undefined);
    const controller = new AbortController();
    request.current = controller;
    const id = ++generation.current;
    void api.generate(parsed, controller.signal).then((result) => {
      if (!current(id)) return;
      setQuestions(result);
      setShownCount(parsed);
    }).catch((failure: unknown) => {
      if (!current(id) || isAbort(failure)) return;
      if (failure instanceof ApiError && failure.kind === 'validation') {
        setFieldError('The service did not accept this count. Check 1 to 50 and try again.');
      }
      setError(failure);
    }).finally(() => {
      if (!current(id)) return;
      guard.current = false;
      request.current = undefined;
      setBusy(false);
    });
  }

  function cancel(): void {
    if (!guard.current) return;
    generation.current += 1;
    request.current?.abort();
    request.current = undefined;
    guard.current = false;
    setBusy(false);
    setError(undefined);
  }

  return <>
    <form className="questionbank-form" onSubmit={(event) => { event.preventDefault(); startRequest(); }} noValidate>
      <ValidatedField id="question-count" label="Number of questions" help="Choose 1 to 50 questions." error={fieldError}
        type="number" min={1} max={50} step={1} required value={count} disabled={busy}
        onChange={(event) => { setCount(event.target.value); setFieldError(undefined); }} />
      <div className="questionbank-actions">
        <button className="button primary" type="submit" disabled={busy}>{questions === undefined ? 'Generate questions' : 'Regenerate questions'}</button>
        {busy && <button className="button secondary" type="button" onClick={cancel}>Cancel</button>}
      </div>
    </form>
    {busy ? <RequestState state="loading" title="Finding questions…" />
      : error ? <RequestState state="error" error={error} onRetry={startRequest} />
        : questions === undefined ? <RequestState state="empty" title="Ready when you are"><p>Choose a count and preview some questions.</p></RequestState>
          : questions.length === 0 ? <RequestState state="empty" title="No questions available"><p>Try again later when the catalog has questions.</p></RequestState>
            : <section className="questionbank-results" aria-label="Question previews">
              <p role="status">Showing {questions.length} of up to {shownCount} questions.</p>
              <ol className="questionbank-list">{questions.map((question) => <li key={question.id}>
                <article className="questionbank-card">
                  <p className="questionbank-category">{question.category}</p>
                  <h2>{question.prompt}</h2>
                  <ol className="questionbank-options">{question.options.map((option) => <li key={option}>{option}</li>)}</ol>
                </article>
              </li>)}</ol>
            </section>}
  </>;
}
