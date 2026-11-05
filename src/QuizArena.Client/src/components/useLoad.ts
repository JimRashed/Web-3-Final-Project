import { useCallback, useEffect, useState } from 'react';

export type LoadState<T> = { status: 'loading' } | { status: 'ready'; value: T } | { status: 'error'; error: unknown };
type Settled<T> = { load: unknown; attempt: number; state: Exclude<LoadState<T>, { status: 'loading' }> };

function isAbort(error: unknown) { return error instanceof DOMException && error.name === 'AbortError'; }

/**
 * Runs `load` whenever its identity changes (wrap it in useCallback) or `reload` is called. The previous
 * request is aborted, and a result counts only for the request that produced it, so anything older reads
 * as loading.
 */
export function useLoad<T>(load: (signal: AbortSignal) => Promise<T>): LoadState<T> & { reload: () => void } {
  const [settled, setSettled] = useState<Settled<T>>();
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    const controller = new AbortController();
    load(controller.signal)
      .then((value) => { if (!controller.signal.aborted) setSettled({ load, attempt, state: { status: 'ready', value } }); })
      .catch((error: unknown) => {
        if (!controller.signal.aborted && !isAbort(error)) setSettled({ load, attempt, state: { status: 'error', error } });
      });
    return () => controller.abort();
  }, [load, attempt]);

  const reload = useCallback(() => setAttempt((value) => value + 1), []);
  const current = settled && settled.load === load && settled.attempt === attempt ? settled.state : { status: 'loading' as const };
  return { ...current, reload };
}
