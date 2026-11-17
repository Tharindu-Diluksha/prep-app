import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { environment } from '../environments/environment';
import { routes } from './app.routes';
import { submitButton, textOf, typeInto } from './testing/test-helpers';

const NAME = 'input[formControlName="name"], input[name="name"]';
const EMAIL = 'input[type="email"]';
const PASSWORD = 'input[type="password"]';

/**
 * Integration: real routes + real components + real AuthService + real HttpClient pipeline.
 * Only the network is stubbed (HttpTestingController).
 */
describe('Auth flow (integration)', () => {
  let harness: RouterTestingHarness;
  let http: HttpTestingController;
  let router: Router;
  const api = (path: string) => `${environment.apiBaseUrl}/api/auth/${path}`;

  const root = () => harness.routeNativeElement as HTMLElement;

  async function settle() {
    harness.detectChanges();
    await harness.fixture.whenStable();
    harness.detectChanges();
  }

  function logoutButton(): HTMLButtonElement {
    const btn = Array.from(root().querySelectorAll('button')).find((b) =>
      /log\s?out/i.test(b.textContent ?? ''),
    );
    if (!btn) {
      throw new Error('No "Log out" button');
    }
    return btn;
  }

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [provideRouter(routes), provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    harness = await RouterTestingHarness.create();
  });

  afterEach(() => http.verify());

  it('AC-1 "/" redirects to /login and renders the login form', async () => {
    await harness.navigateByUrl('/');
    await settle();

    expect(router.url).toBe('/login');
    expect(root().querySelector(EMAIL)).toBeTruthy();
    expect(root().querySelector(PASSWORD)).toBeTruthy();
    expect(root().querySelector('a[href="/signup"]')).toBeTruthy();
  });

  it('AC-2 AC-15 AC-14 login -> /home shows the name via /me -> log out -> /login', async () => {
    await harness.navigateByUrl('/');
    await settle();

    typeInto(root(), EMAIL, 'ada@example.com');
    typeInto(root(), PASSWORD, 'password123');
    harness.detectChanges();
    submitButton(root()).click();

    const login = http.expectOne(api('login'));
    expect(login.request.method).toBe('POST');
    expect(login.request.withCredentials).toBe(true);
    expect(login.request.body).toEqual({ email: 'ada@example.com', password: 'password123' });
    login.flush({ name: 'Ada Lovelace', email: 'ada@example.com' });
    await settle();

    expect(router.url).toBe('/home');

    const me = http.expectOne(api('me'));
    expect(me.request.method).toBe('GET');
    expect(me.request.withCredentials).toBe(true);
    me.flush({ name: 'Ada Lovelace', email: 'ada@example.com' });
    await settle();
    expect(textOf(root())).toContain('Ada Lovelace');

    logoutButton().click();
    const logout = http.expectOne(api('logout'));
    expect(logout.request.method).toBe('POST');
    expect(logout.request.withCredentials).toBe(true);
    logout.flush(null, { status: 204, statusText: 'No Content' });
    await settle();

    expect(router.url).toBe('/login');
    expect(root().querySelector(EMAIL)).toBeTruthy();
  });

  it('AC-4 signup -> 201 -> /login?registered=1 shows the success banner and does not log in', async () => {
    await harness.navigateByUrl('/signup');
    await settle();

    typeInto(root(), NAME, 'Ada Lovelace');
    typeInto(root(), EMAIL, 'ada@example.com');
    typeInto(root(), PASSWORD, 'password123');
    harness.detectChanges();
    submitButton(root()).click();

    const signup = http.expectOne(api('signup'));
    expect(signup.request.method).toBe('POST');
    expect(signup.request.body).toEqual({
      name: 'Ada Lovelace',
      email: 'ada@example.com',
      password: 'password123',
    });
    signup.flush({ name: 'Ada Lovelace', email: 'ada@example.com' }, { status: 201, statusText: 'Created' });
    await settle();

    expect(router.url).toBe('/login?registered=1');
    expect(textOf(root())).toContain('Account created. You can now log in.');
    // Signup must not trigger a session lookup or login request.
    http.expectNone(api('login'));
    http.expectNone(api('me'));
  });

  it('AC-12 signup 409 shows the duplicate message and stays on /signup', async () => {
    await harness.navigateByUrl('/signup');
    await settle();

    typeInto(root(), NAME, 'Ada');
    typeInto(root(), EMAIL, 'ada@example.com');
    typeInto(root(), PASSWORD, 'password123');
    harness.detectChanges();
    submitButton(root()).click();

    http
      .expectOne(api('signup'))
      .flush({ title: 'An account with this email already exists.' }, { status: 409, statusText: 'Conflict' });
    await settle();

    expect(router.url).toBe('/signup');
    expect(textOf(root())).toContain('An account with this email already exists.');
    expect(submitButton(root()).disabled).toBe(false);
  });

  it('AC-12 login 401 shows the generic message, stays on /login, never leaks the server body', async () => {
    await harness.navigateByUrl('/login');
    await settle();

    typeInto(root(), EMAIL, 'ada@example.com');
    typeInto(root(), PASSWORD, 'wrong-password');
    harness.detectChanges();
    submitButton(root()).click();

    http
      .expectOne(api('login'))
      .flush('System.Exception: stack trace here', { status: 401, statusText: 'Unauthorized' });
    await settle();

    expect(router.url).toBe('/login');
    expect(textOf(root())).toContain('Invalid email or password.');
    expect(textOf(root())).not.toContain('stack trace');
  });

  it('AC-12 login with the backend unreachable (status 0) shows the unreachable message', async () => {
    await harness.navigateByUrl('/login');
    await settle();

    typeInto(root(), EMAIL, 'ada@example.com');
    typeInto(root(), PASSWORD, 'password123');
    harness.detectChanges();
    submitButton(root()).click();

    http.expectOne(api('login')).error(new ProgressEvent('error'), { status: 0 });
    await settle();

    expect(router.url).toBe('/login');
    expect(textOf(root())).toContain("Can't reach the server. Please try again later.");
  });

  it('AC-15 opening /home without a session (/me 401) lands on /login', async () => {
    await harness.navigateByUrl('/home');

    http.expectOne(api('me')).flush({}, { status: 401, statusText: 'Unauthorized' });
    await settle();

    expect(router.url).toBe('/login');
  });

  it('AC-15 after logout, going back to /home hits /me again and a 401 returns to /login', async () => {
    await harness.navigateByUrl('/home');
    http.expectOne(api('me')).flush({ name: 'Ada', email: 'ada@example.com' });
    await settle();

    logoutButton().click();
    http.expectOne(api('logout')).flush(null, { status: 204, statusText: 'No Content' });
    await settle();
    expect(router.url).toBe('/login');

    await harness.navigateByUrl('/home');
    http.expectOne(api('me')).flush({}, { status: 401, statusText: 'Unauthorized' });
    await settle();

    expect(router.url).toBe('/login');
  });

  it('AC-3 unknown paths fall back to /login', async () => {
    await harness.navigateByUrl('/does/not/exist');
    await settle();

    expect(router.url).toBe('/login');
  });
});
