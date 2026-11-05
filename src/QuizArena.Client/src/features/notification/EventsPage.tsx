import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { createClients } from '../../api/clients';
import { createNotificationApi, type NotificationEvent } from '../../api/notification';
import { useSession } from '../../auth/AuthProvider';
import { Session, SessionRequiredError } from '../../auth/session';
import { RequestState } from '../../components/RequestState';
import { readConfig } from '../../config';

export function EventsPage() {
  const { session, status } = useSession();
  return <section className="destination">
    <p className="eyebrow muted">Stay in the loop</p>
    <h1>Events</h1>
    <p className="page-description">Match updates addressed to you.</p>
    {status === 'authenticated' ? <SessionEvents session={session} />
      : status === 'loading' ? <RequestState state="loading" title="Connecting to your session…" />
        : <RequestState state="error" error={new SessionRequiredError()} onSignIn={status === 'unavailable' ? undefined : () => { void session.login(); }} />}
  </section>;
}

function SessionEvents({ session }: { session: Session }) {
  const api = useMemo(() => createNotificationApi(createClients(readConfig(import.meta.env), session)), [session]);
  const [events, setEvents] = useState<NotificationEvent[]>();
  const [error, setError] = useState<unknown>();

  useEffect(() => {
    const controller = new AbortController();
    api.list(controller.signal)
      .then((loaded) => { if (!controller.signal.aborted) setEvents(loaded); })
      .catch((failure: unknown) => {
        if (!controller.signal.aborted && !(failure instanceof DOMException && failure.name === 'AbortError')) setError(failure);
      });
    return () => controller.abort();
  }, [api]);

  if (error) return <RequestState state="error" error={error} />;
  if (!events) return <RequestState state="loading" />;
  if (events.length === 0) {
    return <RequestState state="empty" title="You’re all caught up"><p>Your latest arena updates will appear here.</p></RequestState>;
  }

  return <ul className="event-list">
    {events.map((event) => <li key={event.eventId} className="event-item">
      <strong>{event.type === 'MATCH_CREATED' ? 'Match started' : 'Match finished'}</strong>
      <span>{event.matchId}</span>
      <time dateTime={event.createdAt}>{new Date(event.createdAt).toLocaleString()}</time>
      {/* In-app navigation keeps the in-memory session; a full page load would sign the player out. */}
      <Link to={`/matches?matchId=${encodeURIComponent(event.matchId)}`}>{event.type === 'MATCH_CREATED' ? 'Open match' : 'View result'}</Link>
    </li>)}
  </ul>;
}
