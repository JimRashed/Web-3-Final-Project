import { useCallback, useId, useMemo, useState } from 'react';
import { createClients } from '../../api/clients';
import { ApiError } from '../../api/httpClient';
import { createStatisticsApi, leaderboardSizes } from '../../api/statistics';
import { Session } from '../../auth/session';
import { RequestState } from '../../components/RequestState';
import { SignedInOnly } from '../../components/SignedInOnly';
import { useLoad } from '../../components/useLoad';
import { readConfig } from '../../config';
import './Statistics.css';

export function LeaderboardPage() {
  return <section className="destination statistics-page">
    <p className="eyebrow muted">Your arena</p>
    <h1>Leaderboard</h1>
    <p className="page-description">See where you stand.</p>
    <SignedInOnly>{(session, subject) => <Leaderboard key={subject} session={session} subject={subject} />}</SignedInOnly>
  </section>;
}

function Leaderboard({ session, subject }: { session: Session; subject: string }) {
  const api = useMemo(() => createStatisticsApi(createClients(readConfig(import.meta.env), session)), [session]);
  const [top, setTop] = useState<number>(leaderboardSizes[0]);
  const selectId = useId();
  const board = useLoad(useCallback((signal: AbortSignal) => api.leaderboard(top, signal), [api, top]));
  const retry = board.status === 'error' && board.error instanceof ApiError && ['unavailable', 'network', 'unexpected'].includes(board.error.kind);

  return <section className="stats-panel" aria-label="Leaderboard">
    <div className="stats-toolbar">
      <label htmlFor={selectId}>Show top</label>
      <select id={selectId} value={top} onChange={(event) => setTop(Number(event.target.value))}>
        {leaderboardSizes.map((size) => <option key={size} value={size}>{size}</option>)}
      </select>
    </div>
    {board.status === 'loading' ? <RequestState state="loading" title="Loading the leaderboard…" />
      : board.status === 'error' ? <RequestState state="error" error={board.error} onRetry={retry ? board.reload : undefined} />
        : board.value.length === 0 ? <RequestState state="empty" title="The board is waiting"><p>Every great streak starts with a first round.</p></RequestState>
          : <ol className="stats-leaders">{board.value.map((leader, index) => {
            const self = leader.userId === subject;
            return <li key={leader.userId} className={self ? 'is-self' : undefined} aria-current={self ? 'true' : undefined}>
              <span className="stats-rank">{index + 1}</span>
              <span className="stats-id">{self ? 'You' : leader.userId}</span>
              <span>{leader.totalScore} pts</span>
              <span>{leader.wins} {leader.wins === 1 ? 'win' : 'wins'}</span>
            </li>;
          })}</ol>}
  </section>;
}
