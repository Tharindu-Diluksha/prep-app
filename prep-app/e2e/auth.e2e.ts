import { expect, test, type Page } from '@playwright/test';

const PASSWORD = 'correct-horse-battery';

function uniqueUser(label: string) {
  const id = `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
  return { name: `E2E ${label}`, email: `e2e-${label}-${id}@example.com`, password: PASSWORD };
}

async function signup(page: Page, user: { name: string; email: string; password: string }) {
  await page.goto('/signup');
  await page.getByLabel('Name').fill(user.name);
  await page.getByLabel('Email').fill(user.email);
  await page.getByLabel('Password').fill(user.password);
  await page.getByRole('button', { name: 'Sign up' }).click();
  await expect(page).toHaveURL(/\/login\?registered=1$/);
}

async function login(page: Page, email: string, password: string) {
  await page.getByLabel('Email').fill(email);
  await page.getByLabel('Password').fill(password);
  await page.getByRole('button', { name: 'Log in' }).click();
}

test.describe('Login and signup', () => {
  test('AC-1 root redirects to /login and the login page renders its controls', async ({ page }) => {
    await page.goto('/');
    await expect(page).toHaveURL(/\/login$/);
    await expect(page.getByLabel('Email')).toBeVisible();
    await expect(page.getByLabel('Password')).toHaveAttribute('type', 'password');
    await expect(page.getByRole('button', { name: 'Log in' })).toBeVisible();
    const link = page.getByRole('link', { name: 'Sign up' });
    await expect(link).toBeVisible();
    await link.click();
    await expect(page).toHaveURL(/\/signup$/);
  });

  test('AC-3 signup page renders name, email, password, submit and a link to /login', async ({ page }) => {
    await page.goto('/signup');
    await expect(page.getByLabel('Name')).toBeVisible();
    await expect(page.getByLabel('Email')).toBeVisible();
    await expect(page.getByLabel('Password')).toHaveAttribute('type', 'password');
    await expect(page.getByRole('button', { name: 'Sign up' })).toBeVisible();
    await page.getByRole('link', { name: 'Log in' }).click();
    await expect(page).toHaveURL(/\/login$/);
  });

  test('AC-7 signup shows inline errors and blocks submit while the form is invalid', async ({ page }) => {
    await page.goto('/signup');
    const submit = page.getByRole('button', { name: 'Sign up' });
    await expect(submit).toBeDisabled();

    await page.getByLabel('Name').fill('   ');
    await page.getByLabel('Name').blur();
    await expect(page.getByText('Name is required.')).toBeVisible();

    await page.getByLabel('Email').fill('not-an-email');
    await page.getByLabel('Email').blur();
    await expect(page.getByText('Enter a valid email address.')).toBeVisible();

    await page.getByLabel('Password').fill('short12');
    await page.getByLabel('Password').blur();
    await expect(page.getByText('Password must be at least 8 characters.')).toBeVisible();
    await expect(submit).toBeDisabled();

    await page.getByLabel('Name').fill('Valid Name');
    await page.getByLabel('Email').fill('valid@example.com');
    await page.getByLabel('Password').fill('long-enough-pw');
    await expect(submit).toBeEnabled();
  });

  test('AC-7 login blocks submit while the form is invalid', async ({ page }) => {
    await page.goto('/login');
    const submit = page.getByRole('button', { name: 'Log in' });
    await expect(submit).toBeDisabled();
    await page.getByLabel('Email').fill('not-an-email');
    await page.getByLabel('Email').blur();
    await expect(page.getByText('Enter a valid email address.')).toBeVisible();
    await expect(submit).toBeDisabled();
  });

  test('AC-4 valid signup shows the success message on /login and does not log the user in', async ({ page, context }) => {
    const user = uniqueUser('signup');
    await signup(page, user);
    await expect(page.getByText('Account created. You can now log in.')).toBeVisible();

    const cookies = await context.cookies();
    expect(cookies.find((c) => c.name === 'prep.session')).toBeUndefined();

    await page.goto('/home');
    await expect(page).toHaveURL(/\/login$/);
  });

  test('AC-2 AC-15 login lands on /home with the name, and the session survives a reload', async ({ page, context }) => {
    const user = uniqueUser('login');
    await signup(page, user);

    await login(page, user.email, user.password);
    await expect(page).toHaveURL(/\/home$/);
    await expect(page.getByRole('heading', { name: `Welcome, ${user.name}` })).toBeVisible();

    const session = (await context.cookies()).find((c) => c.name === 'prep.session');
    expect(session, 'session cookie is set').toBeDefined();
    expect(session!.httpOnly).toBe(true);
    expect(session!.sameSite).toBe('Lax');

    await page.reload();
    await expect(page).toHaveURL(/\/home$/);
    await expect(page.getByRole('heading', { name: `Welcome, ${user.name}` })).toBeVisible();
  });

  test('AC-14 AC-15 log out returns to /login and /home is then refused', async ({ page }) => {
    const user = uniqueUser('logout');
    await signup(page, user);
    await login(page, user.email, user.password);
    await expect(page.getByRole('heading', { name: `Welcome, ${user.name}` })).toBeVisible();

    await page.getByRole('button', { name: 'Log out' }).click();
    await expect(page).toHaveURL(/\/login$/);

    await page.goto('/home');
    await expect(page).toHaveURL(/\/login$/);
  });

  test('AC-15 opening /home without a session redirects to /login', async ({ page }) => {
    await page.goto('/home');
    await expect(page).toHaveURL(/\/login$/);
  });

  test('AC-12 wrong password shows a generic message and stays on /login', async ({ page, context }) => {
    const user = uniqueUser('wrongpw');
    await signup(page, user);

    await login(page, user.email, 'this-is-not-the-password');
    await expect(page.getByRole('alert')).toHaveText('Invalid email or password.');
    await expect(page).toHaveURL(/\/login/);
    expect((await context.cookies()).find((c) => c.name === 'prep.session')).toBeUndefined();
  });

  test('AC-12 duplicate signup (different case) shows the duplicate message', async ({ page }) => {
    const user = uniqueUser('dup');
    await signup(page, user);

    await page.goto('/signup');
    await page.getByLabel('Name').fill('Someone Else');
    await page.getByLabel('Email').fill(user.email.toUpperCase());
    await page.getByLabel('Password').fill(user.password);
    await page.getByRole('button', { name: 'Sign up' }).click();
    await expect(page.getByRole('alert')).toHaveText('An account with this email already exists.');
    await expect(page).toHaveURL(/\/signup$/);
  });

  test('AC-12 with the backend unreachable, login shows a friendly message and re-enables the button', async ({ page }) => {
    await page.route('**/api/auth/login', (route) => route.abort('connectionrefused'));
    await page.goto('/login');
    await page.getByLabel('Email').fill('nobody@example.com');
    await page.getByLabel('Password').fill(PASSWORD);
    const submit = page.getByRole('button', { name: 'Log in' });
    await submit.click();
    await expect(page.getByRole('alert')).toHaveText("Can't reach the server. Please try again later.");
    await expect(submit).toBeEnabled();
  });

  test('AC-12 the submit button is disabled while the login request is in flight', async ({ page }) => {
    let release!: () => void;
    const gate = new Promise<void>((resolve) => (release = resolve));
    let requests = 0;
    await page.route('**/api/auth/login', async (route) => {
      requests++;
      await gate;
      await route.continue();
    });
    await page.goto('/login');
    await page.getByLabel('Email').fill('nobody@example.com');
    await page.getByLabel('Password').fill(PASSWORD);
    const submit = page.getByRole('button', { name: /Logging in|Log in/ });
    await submit.click();
    await expect(submit).toBeDisabled();
    release();
    await expect(page.getByRole('alert')).toHaveText('Invalid email or password.');
    expect(requests).toBe(1);
  });
});
