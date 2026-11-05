import React from 'react';
import ReactDOM from 'react-dom/client';
import Keycloak from 'keycloak-js';
import { BrowserRouter } from 'react-router-dom';
import { App } from './app/App';
import { AuthProvider } from './auth/AuthProvider';
import { Session } from './auth/session';
import { readConfig } from './config';
import { RequestState } from './components/RequestState';

const root = ReactDOM.createRoot(document.getElementById('root')!);
root.render(<RequestState state="loading" title="Opening the arena…" />);

async function start() {
  const config = readConfig(import.meta.env);
  const session = new Session(new Keycloak(config.oidc), window.location.origin);
  // Resolve callback handling before the router reads the URL; initialize once per page.
  await session.initialize();
  if (window.location.pathname === '/auth/callback' && session.getSnapshot().status === 'authenticated') {
    window.history.replaceState(null, '', '/');
  }
  root.render(<React.StrictMode><AuthProvider session={session}><BrowserRouter><App /></BrowserRouter></AuthProvider></React.StrictMode>);
}
void start().catch(() => root.render(<RequestState state="error" title="The arena could not open"><p>Check the application configuration and reload to reconnect.</p></RequestState>));
