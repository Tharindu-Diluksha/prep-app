import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '../../environments/environment';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  let service: AuthService;
  let http: HttpTestingController;
  const base = () => `${environment.apiBaseUrl}/api/auth`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('AC-13 environment.apiBaseUrl points at the dev API', () => {
    expect(environment.apiBaseUrl).toBe('http://localhost:5146');
  });

  it('AC-13 signup POSTs the payload to /api/auth/signup with credentials', () => {
    const payload = { name: 'Ada', email: 'ada@example.com', password: 'password123' };
    service.signup(payload).subscribe();

    const req = http.expectOne(`${base()}/signup`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(payload);
    expect(req.request.withCredentials).toBe(true);
    req.flush({ name: 'Ada', email: 'ada@example.com' }, { status: 201, statusText: 'Created' });
  });

  it('AC-13 login POSTs the payload to /api/auth/login with credentials', () => {
    const payload = { email: 'ada@example.com', password: 'password123' };
    let result: unknown;
    service.login(payload).subscribe((u: unknown) => (result = u));

    const req = http.expectOne(`${base()}/login`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(payload);
    expect(req.request.withCredentials).toBe(true);
    req.flush({ name: 'Ada', email: 'ada@example.com' });
    expect(result).toEqual({ name: 'Ada', email: 'ada@example.com' });
  });

  it('AC-13 logout POSTs to /api/auth/logout with credentials', () => {
    service.logout().subscribe();

    const req = http.expectOne(`${base()}/logout`);
    expect(req.request.method).toBe('POST');
    expect(req.request.withCredentials).toBe(true);
    req.flush(null, { status: 204, statusText: 'No Content' });
  });

  it('AC-13 me GETs /api/auth/me with credentials and returns the user', () => {
    let result: unknown;
    service.me().subscribe((u: unknown) => (result = u));

    const req = http.expectOne(`${base()}/me`);
    expect(req.request.method).toBe('GET');
    expect(req.request.withCredentials).toBe(true);
    req.flush({ name: 'Ada', email: 'ada@example.com' });
    expect(result).toEqual({ name: 'Ada', email: 'ada@example.com' });
  });
});
