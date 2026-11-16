import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { environment } from '../../environments/environment';
import { StubPage, blurElement, submitButton, textOf, typeInto } from '../testing/test-helpers';
import { Login } from './login';

describe('Login', () => {
  let harness: RouterTestingHarness;
  let http: HttpTestingController;
  let root: HTMLElement;
  const loginUrl = () => `${environment.apiBaseUrl}/api/auth/login`;

  async function open(url = '/login') {
    harness = await RouterTestingHarness.create();
    await harness.navigateByUrl(url, Login);
    root = harness.routeNativeElement as HTMLElement;
  }

  async function settle() {
    harness.detectChanges();
    await harness.fixture.whenStable();
    harness.detectChanges();
  }

  function fillValid() {
    typeInto(root, 'input[type="email"]', 'ada@example.com');
    typeInto(root, 'input[type="password"]', 'password123');
    harness.detectChanges();
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'login', component: Login },
          { path: 'signup', component: StubPage },
          { path: 'home', component: StubPage },
        ]),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('AC-1 renders email, masked password, submit button and a link to /signup', async () => {
    await open();

    expect(root.querySelector('input[type="email"]')).toBeTruthy();
    expect(root.querySelector('input[type="password"]')).toBeTruthy();
    expect(root.querySelector('button[type="submit"]')).toBeTruthy();
    expect(root.querySelector('a[href="/signup"]')).toBeTruthy();
  });

  it('AC-1 uses autofill-friendly autocomplete attributes', async () => {
    await open();

    expect(root.querySelector('input[type="email"]')?.getAttribute('autocomplete')).toBe('email');
    expect(root.querySelector('input[type="password"]')?.getAttribute('autocomplete')).toBe('current-password');
  });

  it('AC-7 submit is disabled while the form is empty', async () => {
    await open();
    expect(submitButton(root).disabled).toBe(true);
  });

  it('AC-7 submit is disabled for a malformed email and enabled once valid', async () => {
    await open();
    typeInto(root, 'input[type="email"]', 'not-an-email');
    typeInto(root, 'input[type="password"]', 'password123');
    harness.detectChanges();
    expect(submitButton(root).disabled).toBe(true);

    typeInto(root, 'input[type="email"]', 'ada@example.com');
    harness.detectChanges();
    expect(submitButton(root).disabled).toBe(false);
  });

  it('AC-7 shows an inline email error only after the field is touched', async () => {
    await open();
    typeInto(root, 'input[type="email"]', 'not-an-email');
    harness.detectChanges();
    const before = textOf(root);

    blurElement(root, 'input[type="email"]');
    harness.detectChanges();

    expect(textOf(root).length).toBeGreaterThan(before.length);
    expect(textOf(root).toLowerCase()).toContain('email');
  });

  it('AC-2 a valid submit POSTs credentials to login and navigates to /home on 200', async () => {
    await open();
    fillValid();

    submitButton(root).click();
    const req = http.expectOne(loginUrl());
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ email: 'ada@example.com', password: 'password123' });
    expect(req.request.withCredentials).toBe(true);

    req.flush({ name: 'Ada', email: 'ada@example.com' });
    await settle();

    expect(TestBed.inject(Router).url).toBe('/home');
  });

  it('AC-12 disables the button while pending so a double click sends one request', async () => {
    await open();
    fillValid();

    const button = submitButton(root);
    button.click();
    harness.detectChanges();
    expect(button.disabled).toBe(true);
    button.click();

    const req = http.expectOne(loginUrl()); // expectOne fails if two requests were sent
    req.flush({ name: 'Ada', email: 'ada@example.com' });
    await settle();
  });

  it('AC-12 shows "Invalid email or password." on 401 and stays on /login', async () => {
    await open();
    fillValid();

    submitButton(root).click();
    http
      .expectOne(loginUrl())
      .flush({ title: 'Invalid email or password.' }, { status: 401, statusText: 'Unauthorized' });
    await settle();

    expect(textOf(root)).toContain('Invalid email or password.');
    expect(TestBed.inject(Router).url).toBe('/login');
    expect(submitButton(root).disabled).toBe(false);
  });

  it('AC-12 shows the unreachable message when the backend is down (status 0)', async () => {
    await open();
    fillValid();

    submitButton(root).click();
    http.expectOne(loginUrl()).error(new ProgressEvent('error'));
    await settle();

    expect(textOf(root)).toContain("Can't reach the server. Please try again later.");
  });

  it('AC-12 never renders the raw server response', async () => {
    await open();
    fillValid();

    submitButton(root).click();
    http
      .expectOne(loginUrl())
      .flush('System.Exception: secret stack trace', { status: 500, statusText: 'Server Error' });
    await settle();

    expect(textOf(root)).not.toContain('stack trace');
    expect(textOf(root)).toContain('Something went wrong. Please try again.');
  });

  it('AC-4 shows the account-created banner when opened with ?registered=1', async () => {
    await open('/login?registered=1');
    expect(textOf(root)).toContain('Account created. You can now log in.');
  });

  it('AC-4 does not show the banner without the query param', async () => {
    await open('/login');
    expect(textOf(root)).not.toContain('Account created');
  });
});
