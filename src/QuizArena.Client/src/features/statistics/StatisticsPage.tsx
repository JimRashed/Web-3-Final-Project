import { useCallback, useMemo, useState } from 'react';
import { createClients } from '../../api/clients';
import { ApiError } from '../../api/httpClient';
import { createStatisticsApi, historyPageSize, type MatchHistoryItem, type PlayerStats } from '../../api/statistics';
import { Session } from '../../auth/session';
import { RequestState } from '../../components/RequestState';
import { useLoad } from '../../components/useLoad';
import { readConfig } from '../../config';
import { SignedInOnly } from '../../components/SignedInOnly';
import './Statistics.css';

export function StatisticsPage() {
  return <section className="destination statistics-page">
    <p className="eyebrow muted">Your arena</p>
    <h1>Statistics</h1>
    <p className="page-description">Every round tells a story.</p>
    <SignedInOnly>{(session, subject) => <PlayerStatistics key={subject} session={session} subject={subject} />}</SignedInOnly>
  </section>;
}

function retryable(error: unknown) { return error instanceof ApiError && ['unavailable', 'network', 'unexpected'].includes(error.kind); }

function PlayerStatistics({ session, subject }: { session: Session; subject: string }) {
  const api = useMemo(() => createStatisticsApi(createClients(readConfig(import.meta.env), session)), [session]);
  const [page, setPage] = useState(1);
  const stats = useLoad(useCallback((signal: AbortSignal) => api.stats(subject, signal), [api, subject]));
  const history = useLoad(useCallback((signal: AbortSignal) => api.history(subject, page, signal), [api, subject, page]));

  return <>
    <section aria-labelledby="totals-heading" className="stats-panel">
      <h2 id="totals-heading">Totals</h2>
      {stats.status === 'loading' ? <RequestState state="loading" title="Loading your totals…" />
        : stats.status === 'error' ? <RequestState state="error" error={stats.error} onRetry={retryable(stats.error) ? stats.reload : undefined} />
          : <Totals stats={stats.value} />}
    </section>
    <section aria-labelledby="history-heading" className="stats-panel">
      <h2 id="history-heading">Match history</h2>
      {history.status === 'loading' ? <RequestState state="loading" title="Loading your matches…" />
        : history.status === 'error' ? <RequestState state="error" error={history.error} onRetry={retryable(history.error) ? history.reload : undefined} />
          : history.value.length === 0 && page === 1 ? <RequestState state="empty" title="No matches yet"><p>Finish a match and it will appear here.</p></RequestState>
            : <History items={history.value} page={page} onPage={setPage} />}
    </section>
  </>;
}

function Totals({ stats }: { stats: PlayerStats }) {
  if (stats.gamesPlayed === 0) return <RequestState state="empty" title="A fresh start"><p>Play your first match to start your record.</p></RequestState>;
  const rate = Math.round((stats.wins / stats.gamesPlayed) * 100);
  return <dl className="stats-totals">
    <div><dt>Games played</dt><dd>{stats.gamesPlayed}</dd></div>
    <div><dt>Wins</dt><dd>{stats.wins}</dd></div>
    <div><dt>Win rate</dt><dd>{rate}%</dd></div>
    <div><dt>Total score</dt><dd>{stats.totalScore}</dd></div>
  </dl>;
}

function History({ items, page, onPage }: { items: MatchHistoryItem[]; page: number; onPage: (page: number) => void }) {
  return <>
    {items.length === 0 ? <p>No more matches on this page.</p> : <table className="stats-table">
      <caption className="visually-hidden">Your matches, newest first</caption>
      <thead><tr><th scope="col">Played</th><th scope="col">Result</th><th scope="col">Score</th><th scope="col">Match</th></tr></thead>
      <tbody>{items.map((item) => <tr key={item.matchId}>
        <td><time dateTime={item.playedAt}>{new Date(item.playedAt).toLocaleString()}</time></td>
        <td>{item.won ? 'Won' : 'Not won'}</td>
        <td>{item.score} / 5</td>
        <td className="stats-id">{item.matchId}</td>
      </tr>)}</tbody>
    </table>}
    <nav className="stats-pager" aria-label="Match history pages">
      <button className="button secondary" disabled={page === 1} onClick={() => onPage(page - 1)}>Newer</button>
      <span>Page {page}</span>
      <button className="button secondary" disabled={items.length < historyPageSize} onClick={() => onPage(page + 1)}>Older</button>
    </nav>
  </>;
}
