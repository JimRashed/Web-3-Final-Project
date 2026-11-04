import { useState } from 'react';
import { Link, NavLink, Route, Routes, useLocation, useParams, useSearchParams } from 'react-router-dom';
import { useSession } from '../auth/AuthProvider';
import { SessionRequiredError } from '../auth/session';
import { RequestState } from '../components/RequestState';
import { destinations } from './routes';
import { LobbyPage } from '../features/lobby/LobbyPage';
import { MatchPage } from '../features/match/MatchPage';
import './App.css';

function SessionActions() {
  const { session, status, displayName } = useSession();
  if (status === 'loading') return <span role="status" className="session-note">Connecting…</span>;
  return <div className="session-actions">
    {status === 'authenticated' ? <><span className="player-name">{displayName}</span><button className="button secondary" onClick={() => session.logout()}>Sign out</button></>
      : <button className="button primary" disabled={status === 'unavailable'} onClick={() => { void session.login(); }}>Sign in <span aria-hidden="true">↗</span></button>}
  </div>;
}

function Home() {
  const { status, session } = useSession();
  return <>
    <section className="hero">
      <div className="hero-copy"><p className="eyebrow"><span /> A little friendly competition</p><h1>Curiosity.<br />Meet competition.</h1><p className="hero-description">Bring what you know. Discover what you don’t. There’s always another great question waiting.</p>
        <Link className="button warm" to="/lobby">Explore the lobby <span aria-hidden="true">→</span></Link>
      </div>
      <div className="arena-art" aria-hidden="true"><div className="orbit orbit-one" /><div className="orbit orbit-two" /><span className="art-spark spark-one">✦</span><span className="art-spark spark-two">+</span><div className="question-card"><span className="card-label">THE NEXT QUESTION</span><strong>?</strong><span className="card-foot">YOURS TO ANSWER <span>↗</span></span></div><span className="tiny-pill">Think. Play. Repeat.</span></div>
    </section>
    <section className="explore" aria-labelledby="explore-heading"><div className="section-heading"><div><p className="eyebrow muted">Find your next move</p><h2 id="explore-heading">Step into the arena</h2></div><span className="section-hint">A good question changes everything.</span></div>
      <div className="explore-grid">{[destinations[0], destinations[1], destinations[5]].map((item, index) => <Link key={item.path} className={`explore-card explore-${index}`} to={item.path}><span className="card-number">0{index + 1}</span><span className="feature-symbol" aria-hidden="true">{item.symbol}</span><h3>{item.label}</h3><p>{item.description}</p><span className="card-arrow" aria-hidden="true">↗</span></Link>)}</div>
    </section>
    {status === 'anonymous' && <section className="join-note"><span aria-hidden="true">✦</span><p>A familiar name makes a better rival. <strong>Sign in to make it yours.</strong></p><button className="text-button" onClick={() => { void session.login(); }}>Join in →</button></section>}
  </>;
}

function Destination({ item }: { item: typeof destinations[number] }) {
  const { session, status } = useSession();
  return <section className="destination"><p className="eyebrow muted">Your arena</p><h1>{item.label}</h1><p className="page-description">{item.description}</p><div className="destination-panel">
    {status === 'authenticated' ? <RequestState state="empty" title={item.empty}><p>{item.detail}</p></RequestState>
      : status === 'loading' ? <RequestState state="loading" />
        : <RequestState state="error" error={new SessionRequiredError()} onSignIn={status === 'unavailable' ? undefined : () => { void session.login(); }} />}
  </div></section>;
}

function SelectedMatch() {
  const { matchId: routeMatchId } = useParams();
  const [search] = useSearchParams();
  const matchId = search.get('matchId') ?? routeMatchId;
  const { status, session } = useSession();
  const [copyNote, setCopyNote] = useState('');
  const matchUrl = new URL('/matches', window.location.origin);
  matchUrl.searchParams.set('matchId', matchId ?? '');
  const matchLink = matchUrl.href;
  if (status !== 'authenticated') return <RequestState state={status === 'loading' ? 'loading' : 'error'} error={new SessionRequiredError()} onSignIn={status === 'unavailable' ? undefined : () => { void session.login(); }} />;
  return <section className="destination"><h1>Selected match</h1><p>Returned match ID: {matchId}</p><p>Match details and participant verification are not implemented in this screen yet.</p><p><a href={matchLink}>{matchLink}</a></p><button className="button secondary" onClick={() => {
    if (!navigator.clipboard?.writeText) { setCopyNote('Select the link and copy it manually.'); return; }
    void navigator.clipboard.writeText(matchLink).then(() => setCopyNote('Match link copied.'), () => setCopyNote('Select the link and copy it manually.'));
  }}>Copy match link</button><p role="status">{copyNote}</p></section>;
}

export function App() {
  const { status } = useSession();
  const location = useLocation();
  const current = destinations.find((item) => item.path === location.pathname);
  return <div className="app-shell"><a className="skip-link" href="#main">Skip to content</a>
    <aside className="sidebar"><Link to="/" className="brand" aria-label="QuizArena home"><span className="brand-icon" aria-hidden="true">Q<span /></span><span>Quiz<span className="brand-light">Arena</span><small>STAY CURIOUS. PLAY TOGETHER.</small></span></Link>
      <nav aria-label="Main navigation"><p className="nav-caption">THE ARENA</p><NavLink to="/" end><span aria-hidden="true">⌂</span>Overview</NavLink>{destinations.slice(0, 4).map((item) => <NavLink key={item.path} to={item.path}><span aria-hidden="true">{item.symbol}</span>{item.label}</NavLink>)}<p className="nav-caption second-caption">YOUR CORNER</p>{destinations.slice(4).map((item) => <NavLink key={item.path} to={item.path}><span aria-hidden="true">{item.symbol}</span>{item.label}</NavLink>)}</nav>
      <div className="sidebar-footer"><span className="status-dot" /> ROOM FOR ONE MORE CURIOUS MIND</div>
    </aside>
    <div className="workspace"><header className="topbar"><p><span className="breadcrumb-brand">QuizArena</span><span className="breadcrumb-divider">/</span>{current?.label ?? 'Overview'}</p><SessionActions /></header>
      <main id="main" tabIndex={-1}>
        {status === 'expired' && <div role="status" className="notice">Your session ended. Sign in when you’re ready to continue.</div>}
        {status === 'unavailable' && <div role="alert" className="notice">Sign-in is temporarily unavailable. You can explore the arena and reload to reconnect.</div>}
        <Routes><Route path="/" element={<Home />} />{destinations.map((item) => <Route key={item.path} path={item.path} element={item.path === '/lobby' ? <LobbyPage /> : <Destination item={item} />} />)}<Route path="/matches" element={<SelectedMatch />} /><Route path="/matches/:matchId" element={<MatchPage />} /><Route path="/notifications" element={<Destination item={destinations.find((item) => item.path === '/events')!} />} /><Route path="/auth/callback" element={<RequestState state={status === 'loading' ? 'loading' : 'error'} title={status === 'loading' ? 'Completing sign-in…' : 'Sign-in could not be completed'}><p>Return to the overview to continue.</p><Link to="/">Go to overview</Link></RequestState>} /><Route path="*" element={<RequestState state="empty" title="A little off course"><p>That page could not be found.</p><Link to="/">Back to the arena →</Link></RequestState>} /></Routes>
      </main><footer className="page-footer"><span>QuizArena</span><span>Good questions. Great company.</span></footer>
    </div>
  </div>;
}
