import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { toUserMessage } from '../auth/auth-errors';
import { AuthUser } from '../auth/auth.models';
import { AuthService } from '../auth/auth.service';

@Component({
  selector: 'app-home',
  templateUrl: './home.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Home {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly user = signal<AuthUser | null>(null);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly loggingOut = signal(false);

  constructor() {
    // Not a route guard (out of scope): the page itself sends unauthenticated visitors to /login.
    this.auth
      .me()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (user) => this.user.set(user),
        error: (err: unknown) => {
          if (err instanceof HttpErrorResponse && err.status === 401) {
            void this.router.navigateByUrl('/login');
            return;
          }
          this.errorMessage.set(toUserMessage(err, 'session'));
        },
      });
  }

  protected logout(): void {
    if (this.loggingOut()) {
      return;
    }
    this.loggingOut.set(true);
    this.errorMessage.set(null);

    this.auth
      .logout()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => void this.router.navigateByUrl('/login'),
        error: (err: unknown) => {
          this.errorMessage.set(toUserMessage(err, 'session'));
          this.loggingOut.set(false);
        },
      });
  }
}
