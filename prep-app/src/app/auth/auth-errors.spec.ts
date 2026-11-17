import { HttpErrorResponse } from '@angular/common/http';
import { toUserMessage } from './auth-errors';

const SERVER_BODY = 'System.Exception: secret stack trace at Foo.Bar()';

function httpError(status: number, body: unknown = SERVER_BODY): HttpErrorResponse {
  return new HttpErrorResponse({ status, error: body, statusText: 'x' });
}

describe('toUserMessage', () => {
  it('AC-12 maps status 0 to the unreachable message', () => {
    expect(toUserMessage(httpError(0), 'login')).toBe("Can't reach the server. Please try again later.");
    expect(toUserMessage(httpError(0), 'signup')).toBe("Can't reach the server. Please try again later.");
  });

  it('AC-12 maps 401 on login to the invalid credentials message', () => {
    expect(toUserMessage(httpError(401), 'login')).toBe('Invalid email or password.');
  });

  it('AC-12 maps 409 to the duplicate account message', () => {
    expect(toUserMessage(httpError(409), 'signup')).toBe('An account with this email already exists.');
  });

  it('AC-12 maps 400 to the check-fields message', () => {
    expect(toUserMessage(httpError(400), 'signup')).toBe('Please check the highlighted fields.');
  });

  it('AC-12 maps 500 to the generic message', () => {
    expect(toUserMessage(httpError(500), 'login')).toBe('Something went wrong. Please try again.');
  });

  it('AC-12 maps a non-HTTP error to the generic message', () => {
    expect(toUserMessage(new Error('boom'), 'login')).toBe('Something went wrong. Please try again.');
    expect(toUserMessage('weird', 'signup')).toBe('Something went wrong. Please try again.');
    expect(toUserMessage(undefined, 'session')).toBe('Something went wrong. Please try again.');
  });

  it('AC-12 never includes the raw server body or stack trace', () => {
    for (const status of [0, 400, 401, 409, 500, 503]) {
      for (const ctx of ['login', 'signup', 'session'] as const) {
        const msg = toUserMessage(httpError(status), ctx);
        expect(msg).not.toContain('secret');
        expect(msg).not.toContain('stack trace');
        expect(msg).not.toContain('System.Exception');
      }
    }
    const withProblem = httpError(500, { title: 'Internal', detail: 'at Foo.Bar() line 12' });
    expect(toUserMessage(withProblem, 'login')).not.toContain('Foo.Bar');
  });
});
