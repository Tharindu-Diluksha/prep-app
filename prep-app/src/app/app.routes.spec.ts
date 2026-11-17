import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { routes } from './app.routes';
import { Home } from './home/home';
import { Login } from './login/login';
import { Signup } from './signup/signup';

describe('app routes', () => {
  let harness: RouterTestingHarness;
  let http: HttpTestingController;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [provideRouter(routes), provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
    harness = await RouterTestingHarness.create();
  });

  it('AC-1 the root path redirects to /login and renders the Login component', async () => {
    const component = await harness.navigateByUrl('/', Login);

    expect(component).toBeInstanceOf(Login);
    expect(TestBed.inject(Router).url).toBe('/login');
  });

  it('AC-1 /login resolves to Login', async () => {
    expect(await harness.navigateByUrl('/login', Login)).toBeInstanceOf(Login);
  });

  it('AC-3 /signup resolves to Signup', async () => {
    expect(await harness.navigateByUrl('/signup', Signup)).toBeInstanceOf(Signup);
  });

  it('AC-15 /home resolves to Home', async () => {
    expect(await harness.navigateByUrl('/home', Home)).toBeInstanceOf(Home);
    http.match(() => true); // Home requests /me on init; not under test here.
  });

  it('unknown paths fall back to /login', async () => {
    await harness.navigateByUrl('/does-not-exist', Login);
    expect(TestBed.inject(Router).url).toBe('/login');
  });
});
