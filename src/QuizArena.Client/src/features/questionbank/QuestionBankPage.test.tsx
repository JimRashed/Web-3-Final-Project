import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AuthProvider } from '../../auth/AuthProvider';
import { Session } from '../../auth/session';
import { adapterDouble, sessionDouble } from '../../test/doubles';
import { QuestionBankPage } from './QuestionBankPage';

const question = {
  id: '01234567-89ab-cdef-0123-456789abcdef',
  prompt: 'Which planet?',
  category: 'Astronomy',
  options: ['Mars', 'Venus', 'Earth', 'Jupiter'],
};

afterEach(() => vi.unstubAllGlobals());

async function mount(authenticated = true) {
  const result = await sessionDouble(authenticated);
  render(<AuthProvider session={result.session}><QuestionBankPage /></AuthProvider>);
  return result;
}

function fetchResponses(...values: Array<unknown | Response>) {
  const fetcher = vi.fn<typeof fetch>();
  values.forEach((value) => fetcher.mockResolvedValueOnce(value instanceof Response ? value : Response.json(value)));
  vi.stubGlobal('fetch', fetcher);
  return fetcher;
}

describe('Questions page', () => {
  it('asks for explicit sign-in and never fetches while anonymous', async () => {
    const fetcher = fetchResponses();
    const { adapter } = await mount(false);
    expect(screen.getByRole('heading', { level: 1, name: 'Questions' })).toBeVisible();
    expect(fetcher).not.toHaveBeenCalled();
    expect(adapter.login).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));
    await waitFor(() => expect(adapter.login).toHaveBeenCalledOnce());
  });
  it('shows loading and unavailable session states without fetching', async () => {
    const fetcher = fetchResponses();
    const adapter = adapterDouble();
    adapter.init = vi.fn(() => new Promise<boolean>(() => {}));
    const session = new Session(adapter, 'http://localhost:5173');
    void session.initialize();
    const view = render(<AuthProvider session={session}><QuestionBankPage /></AuthProvider>);
    expect(screen.getByText('Connecting to your session…')).toBeVisible();
    view.unmount();
    act(() => session.invalidate('unavailable'));
    render(<AuthProvider session={session}><QuestionBankPage /></AuthProvider>);
    expect(screen.getByRole('heading', { name: 'Sign-in unavailable' })).toBeVisible();
    expect(fetcher).not.toHaveBeenCalled();
  });
  it('starts at ten without a request, then previews only category, prompt and options', async () => {
    const fetcher = fetchResponses([{ ...question, correctAnswer: 'SECRET-answer' }]);
    await mount();
    expect(screen.getByRole('spinbutton', { name: 'Number of questions' })).toHaveValue(10);
    expect(screen.getByRole('heading', { name: 'Ready when you are' })).toBeVisible();
    expect(fetcher).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole('button', { name: 'Generate questions' }));
    await screen.findByRole('heading', { name: 'Which planet?' });
    expect(screen.getByText('Astronomy')).toBeVisible();
    for (const option of question.options) expect(screen.getByText(option)).toBeVisible();
    expect(screen.getByText('Showing 1 of up to 10 questions.')).toBeVisible();
    expect(screen.queryByText(/SECRET-answer/)).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Regenerate questions' })).toBeVisible();
    expect(String(fetcher.mock.calls[0][0])).toBe('http://localhost:5103/api/questions/generate?count=10');
  });
  it('uses the newly selected count for an explicit regeneration', async () => {
    const fetcher = fetchResponses([question], []);
    await mount();
    fireEvent.click(screen.getByRole('button', { name: 'Generate questions' }));
    await screen.findByRole('heading', { name: 'Which planet?' });
    fireEvent.change(screen.getByRole('spinbutton', { name: 'Number of questions' }), { target: { value: '1' } });
    fireEvent.click(screen.getByRole('button', { name: 'Regenerate questions' }));
    await screen.findByRole('heading', { name: 'No questions available' });
    expect(String(fetcher.mock.calls[1][0])).toBe('http://localhost:5103/api/questions/generate?count=1');
    expect(fetcher).toHaveBeenCalledTimes(2);
  });
  it('accepts the maximum count from the form', async () => {
    const fetcher = fetchResponses([]);
    await mount();
    fireEvent.change(screen.getByRole('spinbutton', { name: 'Number of questions' }), { target: { value: '50' } });
    fireEvent.click(screen.getByRole('button', { name: 'Generate questions' }));
    await screen.findByRole('heading', { name: 'No questions available' });
    expect(String(fetcher.mock.calls[0][0])).toBe('http://localhost:5103/api/questions/generate?count=50');
  });
  it.each(['', '0', '51', '1.5', '1e1'])('rejects invalid count %j locally and focuses its associated field', async (value) => {
    const fetcher = fetchResponses();
    await mount();
    const field = screen.getByRole('spinbutton', { name: 'Number of questions' });
    fireEvent.change(field, { target: { value } });
    fireEvent.click(screen.getByRole('button', { name: 'Generate questions' }));
    expect(field).toHaveFocus();
    expect(field).toHaveAttribute('aria-invalid', 'true');
    expect(screen.getByText('Choose a whole number from 1 to 50.')).toBeVisible();
    expect(fetcher).not.toHaveBeenCalled();
  });
  it('renders prompt and options as text without interpreting markup', async () => {
    const markup = '<img src=x onerror=alert(1)>';
    fetchResponses([{ ...question, prompt: markup, category: markup, options: [markup, 'B', 'C', 'D'] }]);
    await mount();
    fireEvent.click(screen.getByRole('button', { name: 'Generate questions' }));
    await screen.findByRole('heading', { name: markup });
    expect(screen.getAllByText(markup)).toHaveLength(3);
    expect(document.querySelector('img')).toBeNull();
  });
  it('shows a safe error for a malformed successful response', async () => {
    const fetcher = fetchResponses([{ ...question, category: ' ' }]);
    await mount();
    fireEvent.click(screen.getByRole('button', { name: 'Generate questions' }));
    await screen.findByRole('alert');
    expect(screen.getByText('Something went wrong. Please try again.')).toBeVisible();
    expect(screen.queryByText('Which planet?')).not.toBeInTheDocument();
    expect(fetcher).toHaveBeenCalledOnce();
  });
  it.each([400, 403, 404, 409, 503, 500])('shows safe %s recovery without raw upstream details or retry loop', async (status) => {
    const fetcher = fetchResponses(Response.json({ title: 'SECRET-title', detail: 'SECRET-detail', errors: { count: ['SECRET-field'] } }, { status }));
    const { session, adapter } = await mount();
    fireEvent.click(screen.getByRole('button', { name: 'Generate questions' }));
    await screen.findByRole('alert');
    expect(screen.queryByText(/SECRET/)).not.toBeInTheDocument();
    expect(fetcher).toHaveBeenCalledOnce();
    expect(session.getSnapshot().status).toBe('authenticated');
    expect(adapter.login).not.toHaveBeenCalled();
    if (status === 400) expect(screen.getByText('The service did not accept this count. Check 1 to 50 and try again.')).toBeVisible();
    if (status === 403) expect(screen.queryByRole('button', { name: 'Try again' })).not.toBeInTheDocument();
    else expect(screen.getByRole('button', { name: 'Try again' })).toBeVisible();
  });
  it('ends the session on 401, clears feature data and offers explicit sign-in', async () => {
    fetchResponses([question], new Response(null, { status: 401 }));
    const { session, adapter } = await mount();
    fireEvent.click(screen.getByRole('button', { name: 'Generate questions' }));
    await screen.findByRole('heading', { name: 'Which planet?' });
    fireEvent.click(screen.getByRole('button', { name: 'Regenerate questions' }));
    await screen.findByRole('button', { name: 'Sign in' });
    expect(screen.queryByText('Which planet?')).not.toBeInTheDocument();
    expect(session.getSnapshot().status).toBe('expired');
    expect(adapter.login).not.toHaveBeenCalled();
  });
  it('retries only on an explicit action after service failure', async () => {
    const fetcher = fetchResponses(new Response(null, { status: 503 }), [question]);
    await mount();
    fireEvent.click(screen.getByRole('button', { name: 'Generate questions' }));
    fireEvent.click(await screen.findByRole('button', { name: 'Try again' }));
    await screen.findByRole('heading', { name: 'Which planet?' });
    expect(fetcher).toHaveBeenCalledTimes(2);
  });
  it('offers safe network recovery and never renders the thrown message', async () => {
    const fetcher = vi.fn<typeof fetch>().mockRejectedValue(new TypeError('SECRET-network'));
    vi.stubGlobal('fetch', fetcher);
    await mount();
    fireEvent.click(screen.getByRole('button', { name: 'Generate questions' }));
    await screen.findByText('Unable to connect. Check your connection and try again.');
    expect(screen.queryByText(/SECRET/)).not.toBeInTheDocument();
    expect(fetcher).toHaveBeenCalledOnce();
  });
});
