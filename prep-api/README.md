# prep-api

ASP.NET Core (.NET 9) minimal API for `prep-app`.

## Run and test

```sh
dotnet run --launch-profile http        # http://localhost:5146
dotnet test tests/PrepApi.Tests         # unit + integration tests
```

A bare `dotnet test` in this folder only builds the API: point it at `tests/PrepApi.Tests`.

## Endpoints

| Method | Path | Notes |
| ------ | ---- | ----- |
| GET | `/api/products` | Sample data |
| POST | `/api/auth/signup` | `{name, email, password}` → `201 {name, email}`, `409` duplicate email, `400` field errors. Does not sign in. |
| POST | `/api/auth/login` | `{email, password}` → `200 {name, email}` + session cookie, `401` generic on any bad credentials |
| POST | `/api/auth/logout` | `204`, clears the cookie (idempotent) |
| GET | `/api/auth/me` | `200 {name, email}` with a valid session, otherwise `401` |

## Configuration (`appsettings*.json` or env vars such as `Auth__SessionHours`)

| Key | Default | Notes |
| --- | ------- | ----- |
| `Auth:UsersFilePath` | `App_Data/users.json` | Relative to the content root. Created on first use. Git-ignored and excluded from build/publish output. |
| `Auth:SessionHours` | `8` | Absolute (non-sliding) session lifetime. Must be > 0 and ≤ 720; the app refuses to start otherwise. |
| `Auth:CookieName` | `prep.session` | HttpOnly, SameSite=Lax, Secure follows the request scheme. |
| `AllowedOrigins` | `http://localhost:4200` (Development) | CORS origins; credentials are allowed, so they must be explicit. |

A users file that is not valid JSON makes auth requests fail with 500 and is **never** overwritten: fix or remove it by hand.

## Security caveats (temporary auth, local development only)

- **CSRF**: the auth POSTs have no antiforgery tokens. `SameSite=Lax`, JSON-only bodies and the CORS allow-list reduce the risk but do not remove it.
- **No rate limiting or lockout** on login: no brute-force protection.
- **Same-site only**: `SameSite=Lax` cookies are not sent on cross-site XHR. If the SPA and API are deployed on different registrable domains, the session will not work without `SameSite=None; Secure` (needs HTTPS, out of scope).
- **Data Protection keys are not persisted or shared**: sessions end when the API restarts and do not work across multiple instances (e.g. App Service scale-out).
- **Single process only**: the users file is protected by an in-process lock; do not point two API instances at the same file.
- **Personal data**: the users file holds names and emails in plain text (passwords are salted PBKDF2 hashes). Treat it as personal data.
- Signup returns `409` for a registered email, which reveals that the account exists (accepted trade-off); login stays generic.
