import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { environment } from '../../environments/environment';
import { StubPage, blurElement, submitButton, textOf, typeInto } from '../testing/test-helpers';
import { Signup } from './signup';

const NAME = 'input[formControlName="name"], input[name="name"]';
const EMAIL = 'input[type="email"]';
const PASSWORD = 'input[type="password"]';

describe('Signup', () => {
  let harness: RouterTestingHarness;
  let http: HttpTestingController;
  let root: HTMLElement;
  const signupUrl = () => `${environment.apiBaseUrl}/api/auth/signup`;

  async function settle() {
    harness.detectChanges();
    await harness.fixture.whenStable();
    harness.detectChanges();
  }

  function fill(name: string, email: string, password: string) {
    typeInto(root, NAME, name);
    typeInto(root, EMAIL, email);
    typeInto(root, PASSWORD, password);
    harness.detectChanges();
  }

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'signup', component: Signup },
          { path: 'login', component: StubPage },
        ]),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/signup', Signup);
    root = harness.routeNativeElement as HTMLElement;
  });

  afterEach(() => http.verify());

  it('AC-3 renders name, email and password inputs, a submit button and a link to /login', () => {
    expect(root.querySelector(NAME)).toBeTruthy();
    expect(root.querySelector(EMAIL)).toBeTruthy();
    expect(root.querySelector(PASSWORD)).toBeTruthy();
    expect(root.querySelector('button[type="submit"]')).toBeTruthy();
    expect(root.querySelector('a[href="/login"]')).toBeTruthy();
  });

  it('AC-3 uses autofill-friendly autocomplete attributes', () => {
    expect(root.querySelector(EMAIL)?.getAttribute('autocomplete')).toBe('email');
    expect(root.querySelector(PASSWORD)?.getAttribute('autocomplete')).toBe('new-password');
  });

  it('AC-7 submit is disabled while the form is empty', () => {
    expect(submitButton(root).disabled).toBe(true);
  });

  it('AC-7 submit is disabled for a whitespace-only name', () => {
    fill('    ', 'ada@example.com', 'password123');
    expect(submitButton(root).disabled).toBe(true);
  });

  it('AC-7 submit is disabled for a malformed email', () => {
    fill('Ada', 'nope', 'password123');
    expect(submitButton(root).disabled).toBe(true);
  });

  it('AC-7 submit is disabled for a 7-character password and enabled at 8', () => {
    fill('Ada', 'ada@example.com', '1234567');
    expect(submitButton(root).disabled).toBe(true);

    typeInto(root, PASSWORD, '12345678');
    harness.detectChanges();
    expect(submitButton(root).disabled).toBe(false);
  });

  it('AC-7 submit is disabled for a name over 100 characters', () => {
    fill('a'.repeat(101), 'ada@example.com', 'password123');
    expect(submitButton(root).disabled).toBe(true);
  });

  it('AC-7 shows inline messages for each touched invalid field', () => {
    fill('   ', 'nope', 'short');
    const before = textOf(root);

    blurElement(root, NAME);
    blurElement(root, EMAIL);
    blurElement(root, PASSWORD);
    harness.detectChanges();

    const text = textOf(root).toLowerCase();
    expect(text.length).toBeGreaterThan(before.length);
    expect(text).toContain('name');
    expect(text).toContain('email');
    expect(text).toContain('8');
  });

  it('AC-4 a valid submit POSTs {name,email,password} and navigates to /login?registered=1 on 201', async () => {
    fill('Ada Lovelace', 'ada@example.com', 'password123');

    submitButton(root).click();
    const req = http.expectOne(signupUrl());
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      name: 'Ada Lovelace',
      email: 'ada@example.com',
      password: 'password123',
    });
    expect(req.request.withCredentials).toBe(true);

    req.flush({ name: 'Ada Lovelace', email: 'ada@example.com' }, { status: 201, statusText: 'Created' });
    await settle();

    expect(TestBed.inject(Router).url).toBe('/login?registered=1');
  });

  it('AC-12 shows the duplicate-account message on 409 and stays on /signup', async () => {
    fill('Ada', 'ada@example.com', 'password123');

    submitButton(root).click();
    http
      .expectOne(signupUrl())
      .flush({ title: 'An account with this email already exists.' }, { status: 409, statusText: 'Conflict' });
    await settle();

    expect(textOf(root)).toContain('An account with this email already exists.');
    expect(TestBed.inject(Router).url).toBe('/signup');
    expect(submitButton(root).disabled).toBe(false);
  });

  it('AC-12 shows the check-fields message on a server 400', async () => {
    fill('Ada', 'ada@example.com', 'password123');

    submitButton(root).click();
    http
      .expectOne(signupUrl())
      .flush({ errors: { email: ['bad'] } }, { status: 400, statusText: 'Bad Request' });
    await settle();

    expect(textOf(root)).toContain('Please check the highlighted fields.');
  });

  it('AC-12 shows the unreachable message when the backend is down', async () => {
    fill('Ada', 'ada@example.com', 'password123');

    submitButton(root).click();
    http.expectOne(signupUrl()).error(new ProgressEvent('error'));
    await settle();

    expect(textOf(root)).toContain("Can't reach the server. Please try again later.");
  });

  it('AC-12 disables the button while the request is in flight (double click sends one request)', async () => {
    fill('Ada', 'ada@example.com', 'password123');

    const button = submitButton(root);
    button.click();
    harness.detectChanges();
    expect(button.disabled).toBe(true);
    button.click();

    http.expectOne(signupUrl()).flush({}, { status: 201, statusText: 'Created' });
    await settle();
  });
});
