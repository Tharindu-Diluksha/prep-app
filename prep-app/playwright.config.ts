import { defineConfig, devices } from '@playwright/test';
import * as os from 'node:os';
import * as path from 'node:path';

// UI port: E2E_PORT (set by the orchestrator) or 4300 (not the dev default, so a running dev server isn't reused).
const uiPort = Number(process.env['E2E_PORT'] ?? 4300);
const baseURL = process.env['E2E_BASE_URL'] ?? `http://localhost:${uiPort}`;
// API port is baked into src/environments/environment.e2e.ts (apiBaseUrl), so keep both in sync.
const apiPort = Number(process.env['E2E_API_PORT'] ?? 5246);
const uiOrigin = new URL(baseURL).origin;
// Throwaway credentials file; tests always use unique emails, so leftovers from earlier runs are harmless.
const usersFile = path.join(os.tmpdir(), 'prep-e2e', 'users.json');

export default defineConfig({
  testDir: './e2e',
  testMatch: '**/*.e2e.ts',
  fullyParallel: false,
  workers: 1,
  retries: 0,
  timeout: 30_000,
  reporter: [['list']],
  use: {
    baseURL,
    trace: 'retain-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: [
    {
      command: `dotnet run --project ../prep-api --no-launch-profile --urls http://localhost:${apiPort}`,
      url: `http://localhost:${apiPort}/`,
      reuseExistingServer: false,
      timeout: 180_000,
      env: {
        ASPNETCORE_ENVIRONMENT: 'Production',
        'AllowedOrigins__0': uiOrigin,
        'Auth__UsersFilePath': usersFile,
      },
    },
    {
      command: `npx ng serve --configuration e2e --port ${uiPort}`,
      url: baseURL,
      reuseExistingServer: false,
      timeout: 180_000,
    },
  ],
});
