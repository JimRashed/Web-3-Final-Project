import { act, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AuthProvider } from '../../auth/AuthProvider';
import type { Session } from '../../auth/session';
import { deferred, sessionDouble } from '../../test/doubles';
import { QuestionBankPage } from './QuestionBankPage';

const question = {
  id: '01234567-89ab-cdef-0123-456789abcdef',
  prompt: 'Fresh question',
  category: 'Science',
  options: ['A', 'B', 'C', 'D'],
};

afterEach(() => vi.unstubAllGlobals());

function view(session: Session) {
  return <AuthProvider session={session}><QuestionBankPage /></AuthProvider>;
}

describe('QuestionBank request lifetime', () => {
  it('guards duplicate submits synchronously and fixes the submitted count while pending', async () => {
    const pending = deferred<Response>();
    const fetcher = vi.fn<typeof fetch>().mockReturnValue(pending.promise);
    vi.stubGlobal('fetch', fetcher);
    const { session } = await sessionDouble();
    render(view(session));
    const field = screen.getByRole('spinbutton', { name: 'Number of questions' });
    const form = field.closest('form')!;
    await act(async () => { fireEvent.submit(form); fireEvent.submit(form); });
    expect(fetcher).toHaveBeenCalledOnce();
    expect(field).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Generate questions' })).toBeDisabled();
    expect(String(fetcher.mock.calls[0][0])).toContain('count=10');
    await act(async () => { pending.resolve(Response.json([question])); });
    expect(screen.getByText('Showing 1 of up to 10 questions.')).toBeVisible();
  });
  it('aborts on Cancel and ignores a late successful reply', async () => {
    const pending = deferred<Response>();
    const fetcher = vi.fn<typeof fetch>().mockReturnValue(pending.promise);
    vi.stubGlobal('fetch', fetcher);
    const { session } = await sessionDouble();
    render(view(session));
    fireEvent.click(screen.getByRole('button', { name: 'Generate questions' }));
    await vi.waitFor(() => expect(fetcher).toHaveBeenCalledOnce());
    fireEvent.click(screen.getByRole('button', { name: 'Cancel' }));
    expect(fetcher.mock.calls[0][1]?.signal?.aborted).toBe(true);
    await act(async () => { pending.resolve(Response.json([question])); });
    expect(screen.getByRole('heading', { name: 'Ready when you are' })).toBeVisible();
    expect(screen.queryByText('Fresh question')).not.toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });
  it('keeps a newer request pending when an older canceled request settles', async () => {
    const old = deferred<Response>();
    const newer = deferred<Response>();
    const fetcher = vi.fn<typeof fetch>().mockReturnValueOnce(old.promise).mockReturnValueOnce(newer.promise);
    vi.stubGlobal('fetch', fetcher);
    const { session } = await sessionDouble();
    render(view(session));
    fireEvent.click(screen.getByRole('button', { name: 'Generate questions' }));
    await vi.waitFor(() => expect(fetcher).toHaveBeenCalledOnce());
    fireEvent.click(screen.getByRole('button', { name: 'Cancel' }));
    fireEvent.click(screen.getByRole('button', { name: 'Generate questions' }));
    await vi.waitFor(() => expect(fetcher).toHaveBeenCalledTimes(2));
    await act(async () => { old.resolve(Response.json([{ ...question, prompt: 'Obsolete question' }])); });
    expect(screen.getByText('Finding questions…')).toBeVisible();
    expect(screen.getByRole('button', { name: 'Generate questions' })).toBeDisabled();
    expect(screen.queryByText('Obsolete question')).not.toBeInTheDocument();
    await act(async () => { newer.resolve(Response.json([question])); });
    expect(screen.getByText('Fresh question')).toBeVisible();
  });
  it('ignores a late failure from a canceled request while a newer request succeeds', async () => {
    const old = deferred<Response>();
    const newer = deferred<Response>();
    const fetcher = vi.fn<typeof fetch>().mockReturnValueOnce(old.promise).mockReturnValueOnce(newer.promise);
    vi.stubGlobal('fetch', fetcher);
    const { session } = await sessionDouble();
    render(view(session));
    fireEvent.click(screen.getByRole('button', { name: 'Generate questions' }));
    await vi.waitFor(() => expect(fetcher).toHaveBeenCalledOnce());
    fireEvent.click(screen.getByRole('button', { name: 'Cancel' }));
    fireEvent.click(screen.getByRole('button', { name: 'Generate questions' }));
    await vi.waitFor(() => expect(fetcher).toHaveBeenCalledTimes(2));
    await act(async () => { old.reject(new Error('obsolete SECRET failure')); });
    expect(screen.getByText('Finding questions…')).toBeVisible();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    await act(async () => { newer.resolve(Response.json([question])); });
    expect(screen.getByText('Fresh question')).toBeVisible();
    expect(screen.queryByText(/SECRET/)).not.toBeInTheDocument();
  });
  it('aborts on unmount and ignores a late failure', async () => {
    const pending = deferred<Response>();
    const fetcher = vi.fn<typeof fetch>().mockReturnValue(pending.promise);
    vi.stubGlobal('fetch', fetcher);
    const { session } = await sessionDouble();
    const rendered = render(view(session));
    fireEvent.click(screen.getByRole('button', { name: 'Generate questions' }));
    await vi.waitFor(() => expect(fetcher).toHaveBeenCalledOnce());
    rendered.unmount();
    expect(fetcher.mock.calls[0][1]?.signal?.aborted).toBe(true);
    await act(async () => { pending.reject(new Error('late SECRET')); });
  });
  it('clears prior results and aborts pending work when the session expires', async () => {
    const pending = deferred<Response>();
    const fetcher = vi.fn<typeof fetch>().mockResolvedValueOnce(Response.json([question])).mockReturnValueOnce(pending.promise);
    vi.stubGlobal('fetch', fetcher);
    const { session, adapter } = await sessionDouble();
    render(view(session));
    fireEvent.click(screen.getByRole('button', { name: 'Generate questions' }));
    await screen.findByText('Fresh question');
    fireEvent.click(screen.getByRole('button', { name: 'Regenerate questions' }));
    await vi.waitFor(() => expect(fetcher).toHaveBeenCalledTimes(2));
    act(() => session.invalidate());
    expect(fetcher.mock.calls[1][1]?.signal?.aborted).toBe(true);
    await act(async () => { pending.resolve(Response.json([question])); });
    expect(screen.getByRole('button', { name: 'Sign in' })).toBeVisible();
    expect(screen.queryByText('Fresh question')).not.toBeInTheDocument();
    expect(adapter.login).not.toHaveBeenCalled();
  });
  it('aborts and clears data when an authenticated session instance is replaced', async () => {
    const pending = deferred<Response>();
    const fetcher = vi.fn<typeof fetch>().mockResolvedValueOnce(Response.json([question])).mockReturnValueOnce(pending.promise);
    vi.stubGlobal('fetch', fetcher);
    const original = await sessionDouble();
    const rendered = render(view(original.session));
    fireEvent.click(screen.getByRole('button', { name: 'Generate questions' }));
    await screen.findByText('Fresh question');
    fireEvent.click(screen.getByRole('button', { name: 'Regenerate questions' }));
    await vi.waitFor(() => expect(fetcher).toHaveBeenCalledTimes(2));
    const replacement = await sessionDouble();
    rendered.rerender(view(replacement.session));
    expect(fetcher.mock.calls[1][1]?.signal?.aborted).toBe(true);
    await act(async () => { pending.resolve(Response.json([question])); });
    expect(screen.getByRole('heading', { name: 'Ready when you are' })).toBeVisible();
    expect(screen.queryByText('Fresh question')).not.toBeInTheDocument();
  });
});
