import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { AuthUser, LoginPayload, SignupPayload } from './auth.models';

/**
 * Calls the prep-api auth endpoints. Every request sends credentials so the browser
 * stores and replays the HTTP-only session cookie across origins (:4200 -> :5146).
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/auth`;
  private readonly options = { withCredentials: true };

  signup(payload: SignupPayload): Observable<AuthUser> {
    return this.http.post<AuthUser>(`${this.baseUrl}/signup`, payload, this.options);
  }

  login(payload: LoginPayload): Observable<AuthUser> {
    return this.http.post<AuthUser>(`${this.baseUrl}/login`, payload, this.options);
  }

  logout(): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/logout`, null, this.options);
  }

  me(): Observable<AuthUser> {
    return this.http.get<AuthUser>(`${this.baseUrl}/me`, this.options);
  }
}
