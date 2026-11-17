import { HttpErrorResponse } from '@angular/common/http';

export type AuthErrorContext = 'login' | 'signup' | 'session';

/**
 * Maps any error to fixed, human-readable copy. The server body is never shown,
 * so stack traces or internal details cannot leak into the UI.
 */
export function toUserMessage(error: unknown, context: AuthErrorContext): string {
  if (error instanceof HttpErrorResponse) {
    if (error.status === 0) {
      return "Can't reach the server. Please try again later.";
    }
    if (error.status === 401 && context === 'login') {
      return 'Invalid email or password.';
    }
    if (error.status === 409) {
      return 'An account with this email already exists.';
    }
    if (error.status === 400) {
      return 'Please check the highlighted fields.';
    }
  }
  return 'Something went wrong. Please try again.';
}
