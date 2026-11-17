import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { environment } from '../../environments/environment';
import { StubPage, textOf } from '../testing/test-helpers';
import { Home } from './home';

describe('Home', () => {
  let harness: RouterTestingHarness;
  let http: HttpTestingController;
  let root: HTMLElement;
  const meUrl = () => `${environment.apiBaseUrl}/api/auth/me`;
  const logoutUrl = () => `${environment.apiBaseUrl}/api/auth/logout`;

  async function settle() {
    harness.detectChanges();
    await harness.fixture.whenStable();
    harness.detectChanges();
  }

  function logoutButton(): HTMLButtonElement {
    const btn = Array.from(root.querySelectorAll('button')).find((b) => /log\s?out/i.test(b.textContent ?? ''));
    if (!btn) {
      throw new Error('No "Log out" button');
    }
    return btn;
  }

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'home', component: Home },
          { path: 'login', component: StubPage },
        ]),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/home', Home);
    root = harness.routeNativeElement as HTMLElement;
  });

  afterEach(() => http.verify());

  it('AC-15 on init GETs /me with credentials and shows the user name on 200', async () => {
    const req = http.expectOne(meUrl());
    expect(req.request.method).toBe('GET');
    expect(req.request.withCredentials).toBe(true);

    req.flush({ name: 'Ada Lovelace', email: 'ada@example.com' });
    await settle();

    expect(textOf(root)).toContain('Ada Lovelace');
    expect(TestBed.inject(Router).url).toBe('/home');
  });

  it('AC-15 navigates to /login when /me returns 401', async () => {
    http.expectOne(meUrl()).flush({}, { status: 401, statusText: 'Unauthorized' });
    await settle();

    expect(TestBed.inject(Router).url).toBe('/login');
  });

  it('AC-15 shows a generic error and does not navigate when /me returns 500', async () => {
    http
      .expectOne(meUrl())
      .flush('System.Exception: secret stack trace', { status: 500, statusText: 'Server Error' });
    await settle();

    expect(TestBed.inject(Router).url).toBe('/home');
    expect(textOf(root)).toContain('Something went wrong. Please try again.');
    expect(textOf(root)).not.toContain('stack trace');
  });

  it('AC-14 clicking "Log out" POSTs to /logout and navigates to /login on 204', async () => {
    http.expectOne(meUrl()).flush({ name: 'Ada', email: 'ada@example.com' });
    await settle();

    logoutButton().click();
    const req = http.expectOne(logoutUrl());
    expect(req.request.method).toBe('POST');
    expect(req.request.withCredentials).toBe(true);

    req.flush(null, { status: 204, statusText: 'No Content' });
    await settle();

    expect(TestBed.inject(Router).url).toBe('/login');
  });

  it('AC-14 the Log out button is disabled while the request is in flight', async () => {
    http.expectOne(meUrl()).flush({ name: 'Ada', email: 'ada@example.com' });
    await settle();

    const button = logoutButton();
    button.click();
    harness.detectChanges();
    expect(button.disabled).toBe(true);
    button.click();

    http.expectOne(logoutUrl()).flush(null, { status: 204, statusText: 'No Content' });
    await settle();
  });
});
